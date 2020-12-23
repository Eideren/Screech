// ReSharper disable CheckNamespace
namespace Screech.Nodes
	// ReSharper restore CheckNamespace
{
	using System;
	using System.Collections.Generic;



	public abstract class Node {}

    public abstract class NodeTree : Node
    {
	    public List<Node>? Children { get; internal set; }
    }

    public class Line : NodeTree
    {
	    public Content Content { get; internal set; } = null!;
	    public override string ToString() => Content.Format;
    }

    public class Scope : NodeTree
    {
	    public string Name { get; internal set; } = null!;
        public override string ToString() => $"== {Name} ==";
    }

    public class GoTo : Node
    {
	    public Scope Destination { get; internal set; } = null!;
	    public override string ToString() => $"-> {Destination.Name}";
    }

    public class Return : Node
    {
	    public override string ToString() => "<-";
    }

    public class Choice : NodeTree
    {
	    public Content Content { get; internal set; } = null!;
	    public override string ToString() => $"> {Content.Format}";
    }

    public class CloseChoice : Node
    {
	    public override string ToString() => "<";
    }

    public class Comment : Node
    {
	    public FormattableString Text { get; internal set; } = null!;
	    public override string ToString() => $"// {Text.Format}";
    }
}