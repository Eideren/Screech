namespace Screech
{
	using Nodes;



	/// <summary>
	/// Wrap your special arguments inside of a <see cref="CustomArgument"/> object to pass them to the script,
	/// you can then read them without the wrapper through <see cref="LineContent"/>.
	/// </summary>
	/// <remarks>
	/// This is kind of a pain but ensures callers don't misuse the interpolation feature by passing in variables instead of functions,
	/// variables' values would return its state at creation of the script instead of retrieving it through a function during line reading
	/// which would return the variable's state at that exact time.
	/// </remarks>
	public record CustomArgument(object Arg);
}