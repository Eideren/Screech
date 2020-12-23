namespace Screech
{
	public class Content
	{
		public string Format;
		public object?[] Arguments;
		public override string ToString() => string.Format( Format, Arguments );
	}
}