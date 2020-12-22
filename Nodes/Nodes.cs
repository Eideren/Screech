namespace Screech.Nodes
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
	    public FormattableString Content { get; internal set; } = null!;
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
	    public FormattableString Content { get; internal set; } = null!;
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