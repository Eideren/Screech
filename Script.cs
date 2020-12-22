namespace Screech
{
	using System;
	using System.Collections.Generic;
	using System.Runtime.CompilerServices;
	using System.Text;
	using System.Text.RegularExpressions;
	using Nodes;
	
	public delegate bool ShowWhen();
    
	public static class Script
	{
		public class Insertion
		{
			readonly Func<object?> _onInsert;
			public Insertion( Func<object?> onInsertParam ) => _onInsert = onInsertParam;
			public override string ToString() => _onInsert()?.ToString() ?? "";
		}



		public class Nop
		{
			readonly Action _nop;
			public Nop( Action nopParam ) => _nop = nopParam;
			
			public override string ToString()
			{
				_nop();
				return null;
			}
		}



		public static ShowWhen If( ShowWhen sw ) => sw;
		public static Insertion s( Func<object?> p ) => new(p);
		public static Nop _( Action a ) => new(a);
	    
	    public static Scope Parse(FormattableString input, Action<Issue> issues, bool includeLineContentInIssue = true, bool stripComments = true)
	    {
		    var root = new Scope{ Name = "Root" };
		    
	        // interpolated pattern looks like so: {index[,alignment][:formatString]}
	        // It's fairly lenient so regex to match the same behavior is slightly more complex than it needs to be
	        // Full version to extract alignment and format string, not necessary right now
	        // "(?<!{){(?'content'(?'index'\\d+)\\s*(,\\s*(?'aliDir'-|)(?'alignment'\\d+)){0,1}\\s*(:(?'format'\\w*)){0,1}\\s*)}(?!})"
	        
	        // Remove whitespace within formatting brackets to avoid weird behavior when splitting by new line
	        var format = input.Format.Replace("\r", null); // this character fuck everything up and they don't exist on non-windows
	        var whiteSpaceMatch = new List<(int start, int length)>();
	        foreach (Match match in Regex.Matches(format, 
		        /* Select string interpolations and create groups for unnecessary whitespace */
	            @"(?<!{){\d+(?'1'\s*)(,(?'2'\s*)(-|)\d+|)(?'3'\s*)(:\w*|)(?'4'\s*)}(?!})", 
	            RegexOptions.ExplicitCapture | RegexOptions.Multiline))
	        {
	            if (match.Length == 0)
	                continue;
	            
	            TryIncludeGroup(match.Groups["1"], whiteSpaceMatch);
	            TryIncludeGroup(match.Groups["2"], whiteSpaceMatch);
	            TryIncludeGroup(match.Groups["3"], whiteSpaceMatch);
	            TryIncludeGroup(match.Groups["4"], whiteSpaceMatch);
	        }

	        static void TryIncludeGroup( Group g, List<(int start, int length)> buff )
	        {
		        if( g.Length > 0 )
			        buff.Add( ( g.Index, g.Length ) );
	        }

	        var sb = new StringBuilder(format);
	        for (int i = whiteSpaceMatch.Count - 1; i >= 0; i--)
	        {
	            var (s, l) = whiteSpaceMatch[i];
	            sb.Remove(s, l);
	        }

	        
	        
	        string? incrementRuler = null;
	        int argsOffset = 0;
	        
	        
	        var scopes = new Dictionary<string, Scope>();
	        var goTos = new List<(int line, GoTo goTo, string destination)>();
	        var stack = new Stack<NodeTree>();
	        stack.Push( root );

	        var lines = sb.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
	        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
	        {
	            sb.Clear();
	            sb.Append(lines[lineIndex]);
	            
	            object[] args;
	            string line;
	            
	            { // Make argument index local to line
	                int argsOffsetStart = argsOffset;
	                var indices = new List<(int start, int length, string content)>();
	                foreach (Match match in Regex.Matches(lines[lineIndex], 
	                    @"(?<!{){(?'index'\d+)(,(-|)\d+|)(:\w*|)}(?!})",
	                    RegexOptions.ExplicitCapture | RegexOptions.Multiline))
	                {
	                    if (match.Length == 0)
	                        continue;
	            
	                    var content = match.Groups["index"];
	                    indices.Add( ( content.Index, content.Length, (argsOffset - argsOffsetStart).ToString() ) );
	                    argsOffset++;
	                }

	                args = new object[argsOffset - argsOffsetStart];
	                for (int i = 0; i < args.Length; i++)
	                    args[i] = input.GetArgument(argsOffsetStart + i) ?? throw new NullReferenceException();
	                
	                for (int i = indices.Count - 1; i >= 0; i--)
	                {
	                    (int start, int length, string content) = indices[i];
	                    sb.Remove(start, length);
	                    sb.Insert(start, content);
	                }

	                line = sb.ToString();
	            }

	            var lineOrNull = includeLineContentInIssue ? line : null;
	            
	            {
	                Match match = Regex.Match(line, @"(?'increment'\s*)");
	                var incrementation = match.Groups["increment"].Value;

	                int depth = 0;                
	                if (incrementation.Length > 0)
	                {
	                    incrementRuler ??= incrementation; // first incremented line defines the ruler

		                var isTab = incrementRuler[0] == '\t';
		                string newIncr;
		                // Try to save bad incrementation by best fitting based on default of 4 spaces for 1 tab
		                if(isTab)
			                newIncr = incrementation.Replace("    ", incrementRuler).Replace( " ", null );
		                else 
			                newIncr = incrementation.Replace("\t", incrementRuler);
		                
		                int div = newIncr.Length / incrementRuler.Length;
		                depth += div;
		                if (newIncr != incrementation)
			                issues( new MixedIndentation( lineOrNull, lineIndex, $"Expected {(isTab ? "tabs" : "spaces")} but file contains mixed, this specific line contains {(isTab ? "spaces" : "tabs")}" ) );
		                
		                if (depth > stack.Count)
		                {
			                issues( new UnexpectedIndentation( lineOrNull, lineIndex, $"Expected a depth of {stack.Count} at most, this one sits at {depth}" ) );
			                depth = stack.Count;
		                }
		                else if (newIncr.Length % incrementRuler.Length != 0)
			                issues( new UnexpectedIndentation( lineOrNull, lineIndex, $"Expected {div * incrementRuler.Length} or {(div+1) * incrementRuler.Length} {(isTab ? "tabs" : "spaces")} but line contains {newIncr.Length}" ) );
	                }

	                while (depth < stack.Count-1)
		                stack.Pop();
	                
		            if (depth == stack.Count)
		            {
			            var parentParent = stack.Peek();
			            var parent = parentParent.Children?[^1] ?? null;
			            if( parent is NodeTree tree )
			            {
				            bool valid = true;
				            if (tree is Line l)
				            {
					            valid = false;
					            for (int i = 0; i < l.Content.ArgumentCount; i++)
					            {
						            if (l.Content.GetArgument(i) is ShowWhen)
						            {
							            valid = true;
							            break;
						            }
					            }
				            }

				            if (valid)
					            stack.Push( tree );
				            else
					            issues( new UnexpectedIndentation( lineOrNull, lineIndex, $"Expected a '{typeof(ShowWhen)}' on parent line" ) );
			            }
			            else // null or other type
			            {
				            issues( new UnexpectedIndentation( lineOrNull, lineIndex, "Indentation too deep or invalid parent line" ) );
			            }
		            }

		            if ((match = Regex.Match(line, @"\s*=+\s*(?'content'[^=]*)")).Success) // Scope
	                {
	                    var content = match.Groups["content"].Value;
	                    if (string.IsNullOrWhiteSpace(content) == false)
	                    {
		                    var name = content.Trim();
		                    var scope = new Scope{ Name = name };
		                    scopes.Add( name, scope );
		                    
		                    while (stack.Count != 0)
			                    stack.Pop();
		                    stack.Push(scope);
	                    }
	                    else
	                    {
		                    issues( new TokenEmpty( lineOrNull, lineIndex, "Scope must be named" ) );
	                    }
	                }
	                else if ((match = Regex.Match(line, @"\s*->\s*(?'content'.*)")).Success) // Go to scope
	                {
		                var content = match.Groups["content"].Value;
		                if (string.IsNullOrWhiteSpace(content) == false)
		                {
			                var destination = content.Trim();
			                var g = new GoTo();
			                goTos.Add( (lineIndex, g, destination) );
			                (stack.Peek().Children ??= new List<Node>()).Add( g );
		                }
		                else
		                {
			                issues( new TokenEmpty( lineOrNull, lineIndex, "GoTo must have a destination" ) );
		                }
	                }
	                else if ((match = Regex.Match(line, @"\s*<-\s*(?'content'.*)")).Success) // Return from scope
	                {
		                var content = match.Groups["content"].Value;
		                if (string.IsNullOrWhiteSpace(content))
		                {
			                (stack.Peek().Children ??= new List<Node>()).Add( new Return() );
		                }
		                else
		                {
			                issues( new TokenNonEmpty( lineOrNull, lineIndex, "Return must not have any content on the same line" ) );
		                }
	                }
	                else if ((match = Regex.Match(line, @"\s*>\s*(?'content'.*)")).Success) // Choice
	                {
		                var contentFormat = match.Groups["content"].Value.Trim();
		                var choice = new Choice{ Content = FormattableStringFactory.Create( contentFormat, args ) };
		                (stack.Peek().Children ??= new List<Node>()).Add( choice );
	                }
	                else if ((match = Regex.Match(line, @"\s*<\s*(?'content'.*)")).Success) // Force close choice
	                {
		                var content = match.Groups["content"].Value;
		                if (string.IsNullOrWhiteSpace(content))
		                {
			                (stack.Peek().Children ??= new List<Node>()).Add( new CloseChoice() );
		                }
		                else
		                {
			                issues( new TokenNonEmpty( lineOrNull, lineIndex, "Force close choice must not have any content on the same line" ) );
		                }
	                }
	                else if ((match = Regex.Match(line, @"\s*//\s*(?'content'.*)")).Success) // Comment
	                {
		                if( stripComments )
			                continue;
		                
		                var content = match.Groups["content"].Value;
		                (stack.Peek().Children ??= new List<Node>()).Add( new Comment{ Text = FormattableStringFactory.Create( content, args ) } );
	                }
	                else // Standard line
	                {
	                    match = Regex.Match(line, @"\s*(?'content'.*)");
	                    var content = match.Groups["content"].Value;
	                    if (string.IsNullOrWhiteSpace(content))
		                    continue;
	                    (stack.Peek().Children ??= new List<Node>()).Add( new Line{ Content = FormattableStringFactory.Create( content.Trim(), args ) } );
	                }
	            }
	        }


	        foreach (var(line, goTo, dest) in goTos)
	        {
		        if (scopes.TryGetValue(dest, out var scopeDest))
			        goTo.Destination = scopeDest;
		        else
			        issues( new UnknownScope( includeLineContentInIssue ? lines[line] : null, line, $"Could not find scope '{dest}' in file" ) );
	        }

	        TrimLists(root);
	        foreach (var kvp in scopes)
		        TrimLists(kvp.Value);
	        
	        static void TrimLists( NodeTree t )
	        {
		        if (t.Children == null) 
			        return;
		        
		        t.Children.TrimExcess();
		        foreach (Node child in t.Children)
		        {
			        if(child is NodeTree st)
				        TrimLists( st );
		        }
	        }

	        return root;
	    }
    }
    
    

	public abstract class Issue
	{
		public string Text{ get; }
		public int SourceLine{ get; }
		public string? LineContent { get; }

		protected Issue( string? sourceLine, int sourceLineNumber, string text )
		{
			Text = $"{GetType().Name}: {text}";
			SourceLine = sourceLineNumber;
			LineContent = sourceLine;
		}

		public override string ToString() => $"'{Text}' @ {SourceLine}{(LineContent != null ? $" => '{LineContent}'" : null)}";
	}



	public abstract class Error : Issue
	{
		protected Error( string? sourceLine, int sourceLineNumber, string text ) : base( sourceLine, sourceLineNumber, text )
		{
		}
	}



	public class UnexpectedIndentation : Error
	{
		public UnexpectedIndentation( string? sourceLine, int sourceLineNumber, string text ) : base( sourceLine, sourceLineNumber, text )
		{
			
		}
	}



	public class MixedIndentation : Issue
	{
		public MixedIndentation( string? sourceLine, int sourceLineNumber, string text ) : base( sourceLine, sourceLineNumber, text )
		{
			
		}
	}
	
	
	
	public class TokenEmpty : Error
	{
		public TokenEmpty( string? sourceLine, int sourceLineNumber, string text ) : base( sourceLine, sourceLineNumber, text )
		{
		}
	}
	
	
	
	public class UnknownScope : Error
	{
		public UnknownScope( string? sourceLine, int sourceLineNumber, string text ) : base( sourceLine, sourceLineNumber, text )
		{
		}
	}
	
	
	
	public class TokenNonEmpty : Issue
	{
		public TokenNonEmpty( string? sourceLine, int sourceLineNumber, string text ) : base( sourceLine, sourceLineNumber, text )
		{
		}
	}
}