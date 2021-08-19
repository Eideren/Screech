# Screech

A tool to build narration for games, [ink](https://github.com/inkle/ink)'s simpler, 
more concise and friendly cousin. Similar to yarn spinner, twine and the likes.

The syntax for this one is more bare-bones than those cited above but operates entirely 
within c# by exploiting the 
'[InterpolatedStringHandler](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-10.0/improved-interpolated-strings)' 
.Net6 feature.

This quirk provides you with type safety, access to all of your project's variables and 
logic without having to parse things, build commands and all of the other boilerplate 
stuff those require to setup. 

Most suited for games which require the narrative and game sim to talk to each other.


For a version which doesn't require .Net6 or above to work, select the 'old' git branch.


## Example
```cs
using System;
using static System.Console;
using Screech;

bool phoneChecked = false;
bool pastTime = false;
string clothes = "trousers";

// This will only build under .Net6 and above, IDEs not up to date will show an error on this line but should build fine.
Script scriptSource = @$"
The alarm next to your bed rings.
> Look at the time 
	The clock reads {()=>DateTime.Now.Hour % 12 + (DateTime.Now.Hour > 12 ? "pm":"am")}
	Half of the day's already gone, might as well go back to sleep ... {()=>DateTime.Now.Hour>13}
		Who are you kidding, you can't afford such luxury right now
		{()=>pastTime=true:run}
	Mmmh, you could do with a quick social media fix{()=>(pastTime ? " though" : "")}, that might stir you out of your lethargy
	> Yay
		-> Phone
	> Nay
> You would rather not

You rise from your bed, snatch your phone, put on your {()=>clothes} and move towards the bathroom

You might have a couple of seconds to check your phone {()=>phoneChecked == false}
	> Eh, might as well
		While pulling your phone out to unlock it your foot walks right into a thick, cold liquid
		Wrenching your foot out of it, you slip and tumble down the stairs in front of you
		Struggling to keep your eyes open and look at the source of this surface,
		the only thing you can discern before loosing sight is the surface of your walls entirely covered by one single eye, looking straight at you ... 
		<-
	> Nah
Congrats, you didn't die !

=== Phone ===
{()=>phoneChecked=true:run}
What should you do
> Check the latest news on the 'vid
	You scroll through articles and fall into the comment section
	How could they be so stupid !?
	You feel your anger rising, your heart thumping, your blood vessels flooded with pressure.
	You died.
	An aneurysm.
> Actually I'd rather not
	<-
";

var mainPassage = Script.BuildScript(scriptSource, WriteLine /*write issues to console*/);
foreach (var line in mainPassage)
{
	if (line.IsLine(out var lineContent))
	{
		// 'lineContent' contains the format and object arguments passed to this line,
		// you can inspect and change them however you want before resolving content to string
		
		// This is the point at which all functions are called and resolved into a string
		// Each time this function is called, it will call the functions contained as well !
		var asString = lineContent.EvaluateAndFormat();
		WriteLine(asString);
		ReadKey(true);
	}
	else if(line.IsChoice(out var choices))
	{
		foreach(var choiceContent in choices)
		{
			// 'choiceContent' is the same as above 'lineContent'
			var evaluated = choiceContent.EvaluateAndFormat();
			WriteLine($">{evaluated}");
		}
		int selection;
		do
		{
			WriteLine($" Enter a number between {0} and {choices.Length - 1}");
		} while (int.TryParse(ReadLine(), out selection) && selection < 0 || selection >= choices.Length);
		line.Choose(selection);
	}
}
```

## Spec

### ``// xyz``
A comment line, ignored by default when reading text

### ``= XYZ =``
Marks the start of a passage, a passage is a point of the script where 
you can jump to using the ``-> XYZ`` command.
Execution will not fall from one passage to another without this command, it will stop instead.

### ``-> XYZ``
Will continue reading from the first line in passage ``XYZ``.

### ``<-``
Will exit this passage and return to the point where we jumped from.

### ``>``
Marks choices, this line and all subsequent ``>`` line on the same incrementation level 
will be presented together when reading.

### ``<``
Marks the end of a series of choices, not required unless you are 
doing two different series of choices without text to split them appart:
```
> Choice a1
> Choice a2
<
> Choice b1
> Choice b2
```
Will first present a1 and a2, and as a second set of choices b1 and b2.
Without that character all four of them would be presented at the same time.

### ``{() => xyz}``
This is a c# lambda. When reading a line, this is evaluated, transformed into a string 
and inserted into the text at that position.

``{() => true}``

If the lambda returns a bool it will not be inserted into the line, instead, 
before reading the line that lambda will be called and if it returns false this line and all
line below at a higher incrementation level will be skipped. Think of this one as an ``if`` statement

``{() => xyz:run}``

Contrary to the above, adding ':run' ensures this function runs but does not 
insert its result into the text.

### ``{SomeParameterlessFunction}``
Note the missing ``()``, this would run that function when the line is read,
if it has any return value and as long as you don't add ':run' it will also be
inserted into the text.

### ``{new CustomArgument(xyz)}``
Provides a way to introduce custom behavior when reading lines, you could loop 
through the arguments and retrieve this one to affect the line in a custom way in your engine.

I encourage you to create shortcuts for it, ex:
```cs
public static object BoldMarker = new object();
public static CustomArgument Bold = new CustomArgument(BoldMarker);

foreach (var line in Script.BuildScript($"{Bold}Some bold text", ...))
	if (line.IsLine(out var lineContent))
		if(lineContent.Contains(BoldMarker))
			MakeTextBold();
```
