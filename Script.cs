namespace Screech
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using System.Text;
	using System.Text.RegularExpressions;
	using Nodes;



	[InterpolatedStringHandler]
	public class Script
	{
		static readonly LineSeparatorMarkerClass LineSeparatorMarker = new();
		static readonly ArgMarkerClass ArgMarker = new();
		
		public readonly List<object> Format = new();
		public readonly List<(object argument, int alignment, string? format)> Arguments = new();
		
		public Script()
		{
			
		}
		
		/// <summary>
		/// Constructor called through the 'InterpolatedStringHandler' system
		/// </summary>
		public Script( int literalLength, int formattedCount )
		{
			Arguments.Capacity = formattedCount;
			Format.Capacity = Arguments.Capacity + formattedCount + 2/*at most this many literals if they are at the start and end of the format*/;
		}

		/// <summary>
		/// Called through the <see cref="InterpolatedStringHandlerAttribute"/> system to append strings
		/// </summary>
		public bool AppendLiteral( string value )
		{
			Format.Add(value);
			return true;
		}
		
		/// <summary>
		/// Called through the <see cref="InterpolatedStringHandlerAttribute"/> system to append a returning function
		/// </summary>
		public bool AppendFormatted<T>( Func<T> del, int alignment = 0, string? format = null )
		{
			Format.Add(ArgMarker);
			Arguments.Add((new FuncWrapper<T>(del, format == "run"), alignment, format));
			return true;
		}
		
		/// <summary>
		/// Called through the <see cref="InterpolatedStringHandlerAttribute"/> system to append a function
		/// </summary>
		public bool AppendFormatted( Action del, int alignment = 0, string? format = null )
		{
			Format.Add(ArgMarker);
			Arguments.Add((del, alignment, format));
			return true;
		}
		
		/// <summary>
		/// Wrap your special arguments inside of a <see cref="CustomArgument"/> object to pass them to the script,
		/// you can then read them without the wrapper through <see cref="LineContent"/>.
		/// </summary>
		/// <remarks>
		/// This is kind of a pain but ensures callers don't misuse the interpolation feature by passing in variables instead of functions,
		/// variables' values would return its state at creation of the script instead of retrieving it through a function during line reading
		/// which would return the variable's state at that exact time.
		/// </remarks>
		public bool AppendFormatted( CustomArgument customArg, int alignment = 0, string? format = null )
		{
			Format.Add(ArgMarker);
			Arguments.Add((customArg.Arg, alignment, format));
			return true;
		}

		/// <summary>
		/// Given a <see cref="Script"/>, returns its content in text-only format,
		/// the same format that <see cref="Localize"/>'s second and <see cref="BuildScript"/>'s third parameter uses.
		/// </summary>
		public static string ToLocalizableFormat( Script source )
		{
			int argCount = 0;
			StringBuilder sb = new();
			foreach( object obj in source.Format )
			{
				if( ReferenceEquals( obj, ArgMarker ) )
					sb.Append( '{' ).Append( argCount++ ).Append( '}' );
				else
					sb.Append( obj );
			}

			return sb.ToString();
		}

		/// <summary>
		/// Substitute the text inside of <paramref name="initialScript"/> with the given <paramref name="localizedText"/>
		/// </summary>
		/// <param name="initialScript">The script containing all necessary arguments</param>
		/// <param name="localizedText">
		/// Provide the localized text here,
		/// the format of which can be seen by sending the <paramref name="initialScript"/> through <see cref="ToLocalizableFormat"/>
		/// </param>
		public static Script Localize( Script initialScript, string localizedText )
		{
			var outScript = new Script();
			
			// Build script from 'localizedText' text and use arguments in 'baseFormat'
			var splits = Regex.Matches( localizedText, @"\{\d*\}", RegexOptions.CultureInvariant );
			Match m;
			int lastCharIndex = 0;
			for( int i = 0; 
				i < splits.Count && (m = splits[i]).Success; 
				i++, lastCharIndex = m.Index+m.Length )
			{
				int strLength = m.Index - lastCharIndex;
				if(strLength > 0)
					outScript.Format.Add( localizedText.Substring( lastCharIndex, strLength ) );

				var argIndex = int.Parse( m.Value.Substring( 1, m.Value.Length - 1 ) );
				outScript.Format.Add( ArgMarker );
				outScript.Arguments.Add( initialScript.Arguments[ argIndex ] );
			}

			{
				int strLength = localizedText.Length - lastCharIndex;
				if(strLength > 0)
					outScript.Format.Add( localizedText.Substring( lastCharIndex, strLength ) );
			}
			return outScript;
		}

		/// <summary>
		/// Builds the script into a state-machine like tree which can then be read/consumed through a 'foreach' or in a custom way by the caller
		/// </summary>
		/// <param name="source">The script to build</param>
		/// <param name="issues">Any issues occuring while parsing the script will be sent here</param>
		/// <param name="currentLocaleTextSubstitution">
		/// If the given script must be localized provide the localized text here,
		/// the format of which can be seen by sending the <paramref name="source"/> through <see cref="ToLocalizableFormat"/>
		/// </param>
		/// <param name="includeLineContentInIssue"> Should issues sent to the <paramref name="issues"/> function contain the content of the line </param>
		/// <param name="stripComments">Should comments be stripped from the data returned by this function </param>
		public static PassageData BuildScript( Script source, Action<Issue> issues, string currentLocaleTextSubstitution = "", bool includeLineContentInIssue = true, bool stripComments = true )
		{
			if( currentLocaleTextSubstitution != "" )
				source = Localize( source, currentLocaleTextSubstitution );

			var format = source.Format.ToList();
			var allArguments = source.Arguments.ToArray();
			
			// Split string per line
			for( int i = format.Count - 1; i >= 0; i-- )
			{
				if( format[ i ] is string == false )
					continue;
				
				var str = ((string)format[ i ]).Replace("\r", null);
				if( str.Contains( '\n' ) == false )
				{
					format[ i ] = str;
					continue;
				}
				
				format.RemoveAt( i );
				int insertionOffset = 0;
				int lStart = 0;
				for (int lEnd = str.IndexOf('\n', 0); 
					lEnd != -1; 
					lStart = lEnd+1, lEnd = str.IndexOf('\n', lStart))
				{
					if(lEnd > lStart)
						format.Insert(i+insertionOffset++, str.Substring(lStart, lEnd-lStart));
					format.Insert(i+insertionOffset++, LineSeparatorMarker);
				}
				if(str.Length > lStart)
					format.Insert(i+insertionOffset++, str.Substring(lStart, str.Length - lStart));
			}
			
			var root = new PassageData("Root");

			string? incrementRuler = null;
	        
			var lines = new List<string>();
	        var passages = new Dictionary<string, PassageData>();
	        var goTos = new List<(int line, GoToData goTo, string destination)>();
	        var stack = new Stack<NodeTree>();
	        stack.Push( root );
	        
	        // Per line cache
	        var sb = new StringBuilder();
	        var args = new List<object>();
	        var thenClauses = new List<Func<bool>>();
	        int argumentOffset = 0;
	        // Go line per line
	        for( int lnStart = 0, lnEnd = NextSeparator(format, 0); 
		        lnStart < format.Count;
		        lnStart = lnEnd+1, lnEnd = NextSeparator(format, lnStart))
	        {
		        sb.Clear();
		        args.Clear();
		        thenClauses.Clear();

		        // Retrieve arguments and text content for this line
		        for( int i = lnStart; i < lnEnd; i++ )
		        {
			        if( format[ i ] is string str )
				        sb.Append( str );
			        else if( format[ i ] == ArgMarker )
			        {
				        (var argument, int alignment, string? formatSpec) = allArguments[ argumentOffset++ ];
				        if( argument is FuncWrapper<bool> wrapper && wrapper.RunOnly == false )
				        {
					        thenClauses.Add( wrapper.Func );
				        }
				        else
				        {
					        args.Add( argument );
					        sb.Append( "{" );
					        sb.Append( args.Count - 1 );
					        if( alignment != 0 )
						        sb.Append( "," ).Append( alignment );
					        if( formatSpec != null )
						        sb.Append( ":" ).Append( formatSpec );
					        sb.Append( "}" );
				        }
			        }
		        }
		        
		        string line = sb.ToString();
		        lines.Add(line);
		        int lineIndex = lines.Count - 1;

		        var lineOrNull = includeLineContentInIssue ? line : null;
		        
		        // Find and fix line depth
                int lineDepth = 0;
                Match match;
                if( (match = Regex.Match(line, @"^(?'increment'\s+)")).Success)
                {
	                var incrementation = match.Groups[ "increment" ].Value;
                    incrementRuler ??= incrementation; // first incremented line defines the ruler

	                var isTab = incrementRuler[0] == '\t';
	                // Try to save bad incrementation by best fitting based on default of 4 spaces for 1 tab
	                string cleanIncr = incrementation.Replace( isTab ? "    " : "\t", incrementRuler );
	                if(isTab)
		                cleanIncr = cleanIncr.Replace( " ", null );
	                
	                int div = cleanIncr.Length / incrementRuler.Length;
	                lineDepth += div;

	                if( cleanIncr != incrementation )
	                {
		                int spaceCount = incrementation.Replace( "\t", null ).Length;
		                int tabsCount = incrementation.Replace( " ", null ).Length;
		                string containDebugText;
		                if( tabsCount > 0 && spaceCount > 0 )
			                containDebugText = $"{tabsCount} tabs and {spaceCount} spaces";
		                else if( tabsCount > 0 )
			                containDebugText = $"{tabsCount} tabs";
		                else
			                containDebugText = $"{spaceCount} spaces";
			            issues( new MixedIndentation( lineOrNull, lineIndex, $"Expected {cleanIncr.Length} {(isTab ? "tabs" : "spaces")} but line starts with {containDebugText}" ) );
	                }

	                if (lineDepth > stack.Count)
	                {
		                issues( new UnexpectedIndentation( lineOrNull, lineIndex, $"Expected a depth of {stack.Count} at most, this one sits at {lineDepth}" ) );
		                lineDepth = stack.Count;
	                }
	                else if (cleanIncr.Length % incrementRuler.Length != 0)
		                issues( new UnexpectedIndentation( lineOrNull, lineIndex, $"Expected {div * incrementRuler.Length} or {(div+1) * incrementRuler.Length} {(isTab ? "tabs" : "spaces")} but line contains {cleanIncr.Length}" ) );
                }

                // Return to the right stack point when current depth is less than previous lines depth
                while (lineDepth < stack.Count-1)
	                stack.Pop();
                
                // Create new stack depth as this one is deeper than previous lines
	            if (lineDepth == stack.Count)
	            {
		            var parentParent = stack.Peek();
		            var parent = parentParent.Children.Count > 0 ? parentParent.Children[^1] : null;
		            if( parent is NodeTree tree && (parent is LineData == false || parent is LineData parentLine && parentLine.VisibilityTests.Length > 0 ) )
			            stack.Push( tree );
		            else if( parent is LineData )
			            issues( new UnexpectedIndentation( lineOrNull, lineIndex, "Expected a function returning bool on line above this one because of increased indentation" ) );
		            else // null or other type
			            issues( new UnexpectedIndentation( lineOrNull, lineIndex, "Indentation too deep or invalid parent line" ) );
	            }

	            // Passage
	            if ((match = Regex.Match(line, @"^\s*=+\s*(?'content'[^=]*)")).Success)
                {
                    var content = match.Groups["content"].Value;
                    if (string.IsNullOrWhiteSpace(content) == false)
                    {
	                    var name = content.Trim();
	                    var passage = new PassageData(name);
	                    passages.Add( name, passage );
	                    
	                    while (stack.Count != 0)
		                    stack.Pop();
	                    stack.Push(passage);
                    }
                    else
                    {
	                    issues( new TokenEmpty( lineOrNull, lineIndex, "Passage must be named" ) );
                    }
                }
	            // Go to passage
                else if ((match = Regex.Match(line, @"^\s*->\s*(?'content'.*)")).Success)
                {
	                var content = match.Groups["content"].Value;
	                if (string.IsNullOrWhiteSpace(content) == false)
	                {
		                var destination = content.Trim();
		                var g = new GoToData();
		                goTos.Add( (lineIndex, g, destination) );
		                stack.Peek().Children.Add( g );
	                }
	                else
	                {
		                issues( new TokenEmpty( lineOrNull, lineIndex, "GoTo must have a destination" ) );
	                }
                }
	            // Return from passage
                else if ((match = Regex.Match(line, @"^\s*<-\s*(?'content'.*)")).Success)
                {
	                var content = match.Groups["content"].Value;
	                if (string.IsNullOrWhiteSpace(content))
	                {
		                stack.Peek().Children.Add( new ReturnToken() );
	                }
	                else
	                {
		                issues( new TokenNonEmpty( lineOrNull, lineIndex, "Return must not have any content on the same line" ) );
	                }
                }
	            // Choice
                else if ((match = Regex.Match(line, @"^\s*>\s*(?'content'.*)")).Success)
                {
	                var contentFormat = match.Groups["content"].Value.Trim();
	                var choice = new ChoiceData( thenClauses.ToArray(), new LineContent( contentFormat, args.ToArray() ) );
	                stack.Peek().Children.Add( choice );
                }
	            // Force close choice
                else if ((match = Regex.Match(line, @"^\s*<\s*(?'content'.*)")).Success)
                {
	                var content = match.Groups["content"].Value;
	                if (string.IsNullOrWhiteSpace(content))
	                {
		                stack.Peek().Children.Add( new CloseChoiceToken() );
	                }
	                else
	                {
		                issues( new TokenNonEmpty( lineOrNull, lineIndex, "Force close choice must not have any content on the same line" ) );
	                }
                }
	            // Comment
                else if ((match = Regex.Match(line, @"^\s*//\s*(?'content'.*)")).Success)
                {
	                if( stripComments )
		                continue;
	                
	                var content = match.Groups["content"].Value;
	                stack.Peek().Children.Add( new CommentData( new LineContent( content, args.ToArray() ) ) );
                }
	            // Standard line
                else
                {
                    match = Regex.Match(line, @"^\s*(?'content'.*)");
                    var content = match.Groups["content"].Value;
                    if (string.IsNullOrWhiteSpace(content))
	                    continue;
                    
                    stack.Peek().Children.Add( new LineData( thenClauses.ToArray(), new LineContent( content, args.ToArray() ) ) );
                }
	        }

			// Set gotos destinations
	        foreach (var(line, goTo, dest) in goTos)
	        {
		        if (passages.TryGetValue(dest, out var passageDest))
			        goTo.Destination = passageDest;
		        else
			        issues( new UnknownPassage( includeLineContentInIssue ? lines[line] : null, line, $"Could not find passage '{dest}' in script" ) );
	        }

	        TrimLists(root);
	        foreach (var kvp in passages)
		        TrimLists(kvp.Value);
	        
	        static void TrimLists( NodeTree t )
	        {
		        if (t.Children.Count == 0) 
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
		
		static int NextSeparator(List<object> format, int startingIndex)
		{
			for( ; startingIndex < format.Count; startingIndex++ )
			{
				if( ReferenceEquals( format[ startingIndex ], LineSeparatorMarker ) )
					return startingIndex;
			}

			return startingIndex;
		}
		
		public interface FuncWrapper
		{
			/// <summary> When true the function will be invoked but the returned value won't be part of the formatted string </summary>
			public bool RunOnly{ get; }
			/// <summary> Invoke function and return value as an object </summary>
			public object? Invoke();
		}
		
		/// <summary>
		/// An object containing a <see cref="Func{T}"/> that was part of a <see cref="Script"/>, used to retrieve the returned value as a boxed object
		/// </summary>
		public record FuncWrapper<T>(Func<T> Func, bool RunOnly) : FuncWrapper
		{
			/// <summary> Invoke function and return value as an object </summary>
			public object? Invoke() => Func();
		}
		
		class LineSeparatorMarkerClass {}
		
		/// <summary> Used in <see cref="Script.Format"/>, object which specifies that an argument of this script should be placed here </summary>
		class ArgMarkerClass {}
	}
}