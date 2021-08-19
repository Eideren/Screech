namespace Screech
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Diagnostics.CodeAnalysis;
	using Nodes;
	
	
	
	public class Reader : IEnumerator<ReaderLine>
	{
		/// <summary>
		/// The current line, changes every time <see cref="MoveNext"/> is called, invalid if <see cref="MoveNext"/> hasn't been called yet
		/// </summary>
		public ReaderLine Current{ get; private set; }
		
		/// <summary>
		/// Whether this reader provides or skip past comments if they have been kept when building the script
		/// </summary>
		public bool ReadComments{ get; set; }
		
		readonly Stack<(NodeTree tree, int currentIndex)> _stack = new();
		
		object IEnumerator.Current => Current;

		public Reader( NodeTree root, bool readComments = false )
		{
			_stack.Push( ( root, 0 ) );
			ReadComments = readComments;
		}
		
		/// <summary>
		/// Forces the reader to continue reading from here,
		/// once we're done reading the provided node and it returns
		/// we'll continue wherever we left off before calling this function
		/// </summary>
		public virtual void ForceGoTo( NodeTree node )
		{
			_stack.Push( ( node, 0 ) );
		}
		
		/// <summary>
		/// Go to the next line, if the previous one was a choice and you didn't
		/// call <see cref="ReaderLine.Choose(int)"/> before, the reader will skip
		/// past those choices.
		/// </summary>
		public virtual bool MoveNext()
		{
			do
			{
				if( _stack.Count == 0 )
					return false;

				if( _stack.Peek().currentIndex >= (_stack.Peek().tree.Children?.Count ?? 0) )
				{
					if( _stack.Peek().tree is PassageData )
						return false;

					_stack.Pop();
					continue;
				}

				Node currentToken; 
				{
					(NodeTree tree, int currentIndex) = _stack.Peek();
					currentToken = tree.Children[ currentIndex ];
					if( currentToken is PassageData == false )
					{
						_stack.Pop();
						_stack.Push( ( tree, currentIndex + 1 ) );
					}
				}

				switch( currentToken )
				{
					case LineData l:
					{
						if( PassVisibilityTest( l.VisibilityTests ) )
						{
							if(l.Children.Count > 0)
								_stack.Push( ( l, 0 ) );
							
							Current = new ReaderLine( l, this );
							return true;
						}

						break;
					}
					case CommentData c:
					{
						if( ReadComments )
						{
							Current = new ReaderLine( c, this );
							return true;
						}

						break;
					}
					case ChoiceData c:
					{
						( NodeTree tree, int currentIndex ) = _stack.Peek();
						List<ChoiceData> choices = new List<ChoiceData>();
						
						for( currentIndex -= 1; 
							currentIndex < tree.Children.Count && tree.Children[ currentIndex ] is ChoiceData choiceData;
							currentIndex++ )
						{
							choices.Add(choiceData);
						}

						// Move stack head after choices, will be changed once/if user picks one of these choices
						_stack.Pop();
						_stack.Push( ( tree, currentIndex ) );

						for( int i = choices.Count - 1; i >= 0; i-- )
						{
							if ( PassVisibilityTest( choices[ i ].VisibilityTests ) == false )
								choices.RemoveAt( i );
						}

						if( choices.Count != 0 )
						{
							Current = new ReaderLine( c, this, choices );
							return true;
						}

						break;
					}
					case GoToData gt:
					{
						_stack.Push( ( gt.Destination, 0 ) );
						break;
					}
					case ReturnToken _:
					{
						// Pop stack until we're just out of this scope
						NodeTree n;
						do
						{
							if( _stack.Count == 0 )
								return false;

							n = _stack.Peek().tree;
							_stack.Pop();
						} while( n is PassageData == false );

						break;
					}
					case PassageData _: return false;
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
		
		protected virtual bool PassVisibilityTest( Func<bool>[] tests )
		{
			foreach( var test in tests )
			{
				if( test() == false )
					return false;
			}

			return true;
		}

		void IDisposable.Dispose(){}
	}
	
	
	
	public struct ReaderLine
	{
		Node _current;
		Reader _reader;
		List<ChoiceData>? _choices;
		
		public ReaderLine( Node Current, Reader Reader, List<ChoiceData>? Choices = null )
		{
			_current = Current;
			_reader = Reader;
			_choices = Choices;
		}


		/// <summary> Is this a non-choice line </summary>
		public bool IsLine( [MaybeNullWhen(false)] out LineContent line )
		{
			if( _current is LineData ld )
			{
				line = ld.Content;
				return true;
			}
			else if( _current is CommentData cd )
			{
				line = cd.Content;
				return true;
			}

			line = null;
			return false;
		}



		/// <summary>
		/// If you do not call <see cref="Choose(int)"/> before <see cref="Reader.MoveNext"/>,
		/// the reader will skip past those choices.
		/// </summary>
		public bool IsChoice( [MaybeNullWhen(false)] out LineContent[] lines )
		{
			if( _current is ChoiceData )
			{
				lines = new LineContent[ _choices!.Count ];
				for( int i = 0; i < _choices.Count; i++ )
					lines[ i ] = _choices[ i ].Content;
				return true;
			}

			lines = null;
			return false;
		}



		/// <summary>
		/// If you do not call this before <see cref="Reader.MoveNext"/>, the reader will skip past those choices
		/// </summary>
		public void Choose( LineContent content )
		{
			if( _current is ChoiceData )
			{
				for( int i = 0; i < _choices!.Count; i++ )
				{
					if( ReferenceEquals( _choices[ i ].Content, content ) )
					{
						Choose( i );
						return;
					}
				}
				throw new ArgumentException( "Argument did not match available choices" );
			}
			else
			{
				throw new InvalidOperationException( $"{nameof(Choose)} cannot be called on {_current.GetType()}" );
			}
		}



		/// <summary>
		/// If you do not call this before <see cref="Reader.MoveNext"/>, the reader will skip past those choices
		/// </summary>
		public void Choose( int index )
		{
			if( _current is ChoiceData )
			{
				_reader.ForceGoTo( _choices![ index ] );
			}
			else
			{
				throw new InvalidOperationException( $"{nameof(Choose)} cannot be called on {_current.GetType()}" );
			}
		}
	}
}