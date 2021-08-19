namespace Screech.Nodes
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Linq;



	public abstract record Node;

	public abstract record NodeTree : Node
	{
		public List<Node> Children { get; } = new List<Node>();
	}

	public record PassageData(string Name) : NodeTree, IEnumerable<ReaderLine>
	{
		public override string ToString() => $"== {Name} ==";
		public IEnumerator<ReaderLine> GetEnumerator() => new Reader( this );
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	public record GoToData : Node
	{
		public PassageData Destination{ get; set; } = null!;
		public override string ToString() => $"-> {Destination.Name}";
	}

	public record ReturnToken : Node
	{
		public override string ToString() => "<-";
	}

	public record LineData(Func<bool>[] VisibilityTests, LineContent Content) : NodeTree
	{
		public override string ToString() => Content.Format;
	}

	public record CommentData(LineContent Content) : Node
	{
		public override string ToString() => $"// {Content.Format}";
	}

	public record ChoiceData(Func<bool>[] VisibilityTests, LineContent Content) : NodeTree
	{
		public override string ToString() => $"> {Content.Format}";
	}

	public record CloseChoiceToken : Node
	{
		public override string ToString() => "<";
	}

	public record LineContent(string Format, object[] Args)
	{
		/// <summary> Invoke functions, format returned values with the format string and return the result </summary>
		public string EvaluateAndFormat( IFormatProvider? provider = null )
		{
			object?[] cpy = Args.ToArray();
			for( int i = 0; i < cpy.Length; i++ )
			{
				while( cpy[ i ] is Script.FuncWrapper fWrapper )
				{
					var obj = fWrapper.Invoke();
					cpy[ i ] = fWrapper.RunOnly ? "" : obj;
				}

				if( cpy[ i ] is Action a )
				{
					a();
					cpy[ i ] = "";
				}
			}

			return string.Format( provider, Format, cpy );
		}
	}
}