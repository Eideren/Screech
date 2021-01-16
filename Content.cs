namespace Screech
{
	using System;



	public class Content
	{
		public string Format = "";
		public object?[] Arguments = Array.Empty<object?>();
		public override string ToString() => string.Format( Format, Arguments );
	}
}