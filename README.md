# Andraste - The "native" C# Modding Framework

> Britain/Celtic goddess of war, symbolizing invincibility

The Andraste Modding Framework aims to be a solid base for those writing an
in-process modding framework for native (x86, 32bit) Windows applications (Games).

It is mostly the result of generalizing code that I would have written
specifically for one game. Releasing it may help others to quickly re-use
functionality as well as maybe contributing and reviewing decisions made here.

## The Host Library
The host is responsible for launching/injecting the payload into the game and
communicating with it. Now due to a few circumstances, it's not actually as simple as that:
- Andraste's GUI shall support loading/launching from arbitrary paths (i.e. specific
versions and distributions that live in folders outside of the actual GUI), but EasyHook
will always start the CLR with a `PATH` relative to the DLL that initiated the injection.
This would mean, when the GUI was capable of launching, we couldn't load arbitrary distributions.
- Since we want to support multiple versions and potentially even game specific frameworks,
that may extend the communication between the game and the host, it'd be hard to support
all from an application that has the primary goal of providing a user interface.
- The idea hence is to split launching between the actual launcher component (`Andraste.Launcher.exe`), that is
part of the Andraste distribution, and the actual UI (TUI/CLI, GUI). Thus, the launcher
does not worry about any API stability as it can natively talk to the payload,
it's just the UI<->Launcher interface that needs to be stable.
- The "TUI"/CLI will most likely be that exact `Andraste.Launcher.exe`, that the UI also uses, as I don't see any 
point in adding yet another CLI/TUI layer with a more simple argument interface.
- This, at the time of writing this ;), sounds like a good idea, especially when
imagining using the generic Andraste GUI (or a CLI when launched via steam et al)
while still leaving the framework distributor the choice of implementation.
Some games may not like being launched directly, so the Launcher can implement
specific workarounds, too.
- This also leaves room for launching multiple profiles from the same UI concurrently,
where every launcher monitors the relevant PID

Thus, this library contains everything that is needed to build the actual Launchers, but
the CLI will heavily rely on this and basically be a no-op, as the CLI Launcher will have
to have the full potential and the GUI will just call the CLI Launcher that is unique to
each andraste distribution.