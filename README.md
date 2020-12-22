# Screech

A tool to build narrations for games, [ink](https://github.com/inkle/ink)'s simpler, more concise and friendly cousin. Similar to yarn spinner, twine and the likes.

The syntax for this one is more barebones than those cited above but operates entirely within c# by exploiting the FormattableString c# feature.
This provides you with type safety, access to all of your project's variables and logic without requiring explicit commands, 
c# operation and logic, pull from and expose fields and properties, etc.

Most suited for games which require the narrative and game sim to talk to each other.

## Example
```cs
using System;
using Screech;
using static System.Console;
using static Screech.Script;

bool checkedPhone = false;
bool pastTime = false;
string clothing = "trousers";

FormattableString content = @$"
The alarm next to your bed rings.
> Look at the time 
	The clock reads {s(() => DateTime.Now.Hour % 12)}{s(() => DateTime.Now.Hour > 12 ? "pm" : "am")}
	Half of the day's already gone, might as well go back to sleep ... {If(()=>DateTime.Now.Hour > 13)}
		Who am I kidding, you can't afford such luxury right now
		{_(()=>pastTime=true)}
	Mmmh, you could do with a quick social media fix{s(()=>pastTime ? " though" : "")}, that might stir you out of your lethargy
	> Yay
		-> Phone
	> Nay
> You would rather not

You rise from your bed, snatch your phone, put on a {s(()=>clothing)} and move towards the bathroom

You might have a couple of seconds to check your phone {If(()=>checkedPhone == false)}
	> Eh, might as well
		While pulling your phone out to unlock it your foot walks right into a thick, cold liquid
		Wrenching your foot out of it, you slip and tumble down the stairs in front of you
		Struggling to keep your eyes open and look at the source of this surface,
		the only thing you can discern before loosing sight is the surface of your walls entirely covered by one single eye, looking straight at you ... 
		<-
	> Nah
Congrats, you didn't die !

=== Phone ===
{_(()=>checkedPhone=true)}
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



var reader = new Reader(Parse(content, WriteLine/*outputs parsing issues straight to console*/));
while (reader.MoveNext())
{
	if (reader.IsChoice == false)
    {
	    // This is the point at which all functions are called and resolved into a string,
	    // you can insert your objects within a script and inspect reader.Current[ 0 ].GetArguments() to retrieve them and use them
	    var s = reader.Current[ 0 ].ToString();
	    if(string.IsNullOrWhiteSpace(s))
		    continue;
	    WriteLine(s);
	    ReadKey(true);
    }
    else
    {
	    foreach( var formattableString in reader.Current )
	    {
		    // This is the point at which all functions are called and resolved to string
		    var evaluated = formattableString.ToString();
		    WriteLine($">{evaluated}");
	    }
	    int selection;
	    do
	    {
		    WriteLine($" Enter a number between {0} and {reader.Current.Length - 1}");
	    } while ( int.TryParse( ReadLine(), out selection ) && selection < 0 || selection >= reader.Current.Length );
	    reader.Choose( selection );
    }
}
```