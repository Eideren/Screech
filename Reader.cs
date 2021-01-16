namespace Screech
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Diagnostics.CodeAnalysis;
	using System.Text;
	using System.Text.RegularExpressions;
	using Nodes;
	
	
	
    public class Reader : IEnumerator<Content[]>
	{
		readonly Stack<(NodeTree tree, int currentIndex)> _stack = new();
		readonly List<(Choice choice, Content content)> _choices = new();

		public Content[] Current { get; private set; } = new Content[0];
		public bool IsChoice = false;

		object IEnumerator.Current => Current;
		
		
		public Reader( NodeTree root ) => _stack.Push( ( root, 0 ) );
		
		public virtual void Choose( int indexChosen )
		{
			_stack.Push( ( _choices[ indexChosen ].choice, 0 ) );
			_choices.Clear();
		}

		bool ShouldShow( Content fs, [MaybeNullWhen(false)]out Content fsFiltered )
		{
			// Scan for method
			bool hasDelegate = false;
			foreach( var argument in fs.Arguments )
			{
				if (argument is ShowWhen)
				{
					hasDelegate = true;
					break;
				}
			}

			if (hasDelegate == false)
			{
				fsFiltered = fs;
				return true;
			}

			// Test method to find this line's visibility and replace references to that method in format string
			var show = false;

			var toRemove = new List<(int s, int l)>();

			foreach (Match match in Regex.Matches(fs.Format, 
				@"(?<!{){(?'index'\d+)(,(-|)\d+|)(:\w*|)}(?!})",
				RegexOptions.ExplicitCapture | RegexOptions.Multiline))
			{
				if (match.Length == 0)
					continue;
            
				var content = match.Groups["index"];
				ref var arg = ref fs.Arguments[ int.Parse( content.Value ) ];
				if (arg is ShowWhen sw)
				{
					arg = null;
					toRemove.Add( (match.Index, match.Length) );
					show |= sw();
				}
			}

			if (show == false)
			{
				fsFiltered = null;
				return false;
			}

			// Remove reference in the right order to avoid additional operations
			toRemove.Sort((a, b) => a.s.CompareTo(b.s));
			var sb = new StringBuilder(fs.Format);
			for (int i = toRemove.Count - 1; i >= 0; i--)
			{
				(int s, int l) = toRemove[i];
				sb.Remove(s, l);
			}
			
			fsFiltered = new Content{ Format = sb.ToString(), Arguments = fs.Arguments };
			return true;
		}

		public virtual bool MoveNext()
		{
			if( _choices.Count > 0 )
				throw new InvalidOperationException( $"Cannot call {nameof(MoveNext)} after a choice without calling {nameof(Choose)} beforehand" );
			
			IsChoice = false;
			do
			{
				if( _stack.Count == 0 )
					return false;

				if( _stack.Peek().currentIndex >= (_stack.Peek().tree.Children?.Count ?? 0) )
				{
					if( _stack.Peek().tree is Scope )
						return false;

					_stack.Pop();
					continue;
				}

				Node currentToken; 
				{
					(NodeTree tree, int currentIndex) = _stack.Peek();
					currentToken = tree.Children![ currentIndex ];
					if( currentToken is Scope == false )
					{
						_stack.Pop();
						_stack.Push( ( tree, currentIndex + 1 ) );
					}
				}

				switch( currentToken )
				{
					case Line l:
					{
						if (ShouldShow(l.Content, out var filteredContent))
						{
							if(l.Children?.Count > 0)
								_stack.Push( ( l, 0 ) );
							
							if (string.IsNullOrWhiteSpace(filteredContent.Format) == false)
							{
								Current = new[] {filteredContent};
								return true;
							}
						}

						break;
					}
					case Choice c:
					{
						IsChoice = true;
						_choices.Add( (c, null!) );
						( NodeTree tree, int currentIndex ) = _stack.Peek();
						for( ; currentIndex < tree.Children?.Count && tree.Children[ currentIndex ] is Choice otherC; currentIndex++ )
							_choices.Add( (otherC, null!) );
						
						_stack.Pop();
						_stack.Push( ( tree, currentIndex ) );

						for( int i = _choices.Count - 1; i >= 0; i-- )
						{
							var choice = _choices[i].choice;
							if (ShouldShow(choice.Content, out var filteredContent))
								_choices[ i ] = (choice, filteredContent);
							else
								_choices.RemoveAt( i );
						}
						
						if( _choices.Count == 0 )
							continue;

						Current = new Content[_choices.Count];
						for (int i = 0; i < _choices.Count; i++)
							Current[i] = _choices[i].Item2;
						return true;
					}
					case GoTo gt:
					{
						_stack.Push( ( gt.Destination, 0 ) );
						continue;
					}
					case Return _:
					{
						// Pop stack until we're just out of this scope
						NodeTree n;
						do
						{
							if( _stack.Count == 0 )
								return false;

							n = _stack.Peek().tree;
							_stack.Pop();
						} while( n is Scope == false );

						continue;
					}
					case Scope _: return false;
					case Comment _: continue;
				}
			} while( true );
		}

		public virtual void Reset()
		{
			while (_stack.Count > 1)
				_stack.Pop();
			var v = _stack.Pop();
			v.currentIndex = 0;
			_stack.Push( v );
		}

		void IDisposable.Dispose(){}
	}
}