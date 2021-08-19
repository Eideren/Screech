namespace Screech
{
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
	
	
	
	public class UnknownPassage : Error
	{
		public UnknownPassage( string? sourceLine, int sourceLineNumber, string text ) : base( sourceLine, sourceLineNumber, text )
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