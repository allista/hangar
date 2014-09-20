[SIZE=5][B]Hangar[/B] - [B][I]store your vessels until they are needed[/I][/B][/SIZE]

[COLOR=#FF0000][SIZE=4][B]WARNING: this is still BETA. There should be bugs.[/B][/SIZE][/COLOR]
While fixing them and implementing new features I'll try as hard as I can to maintain backward compatibility, [I]but I can't guarantee it[/I]. So if you plan to use it in your main game, [SIZE=5][COLOR=#FF0000][B]backup your saves[/B][/COLOR][/SIZE].

[COLOR=#FF0000][SIZE=5][b]!!! v1.2.0 WARNING WARNING WARNING v1.2.0 !!![/b][/COLOR][/SIZE]

[SIZE=3][b]This update may break your saves, BUT all breaks are easily fixable.[/b][/SIZE]

To [b]safely[/b] install the update, do the following:
[list=1]
[*][b]If[/b] your savegame contains [i]landed[/i] [b]Rover Lander[/b] [i]with opened doors[/i],[list]
[*]switch to it [b]before[/b] upgrading and [b]close the doors[/b].
[/list]
[*]Delete the old version of the mod before installing this one.
[i]You may keep the config.xml to save positions of GUI windows, though.[/i]
[*]Install the new version.
[*][b]If[/b] your savegame contains:[list]
[*]Any ship that includes [b]S4-S3 or S4-S2 adapters[/b]:[list]
[*]install the [url=https://github.com/allista/hangar/raw/master/DeprecatedParts/DeprecatedPartsAddon.zip][b]Deprecated Parts Addon[/b][/url], then recover such ship and rebuild it in editor using new Universal Stack Adapter
[/list]
[*]Any ship that includes [i]small[/i] [b]Inline Hangar[/b] or [i]small[/i] [b]Ground Hangar[/b], you should either:[list]
[*]install the [url=https://github.com/allista/hangar/raw/master/DeprecatedParts/DeprecatedPartsAddon.zip][b]Deprecated Parts Addon[/b][/url], then recover such ship and rebuild it in editor,
[*]or open the savegame file in any text editor, find the corresponding part ([b]InlineHangar1[/b] or [b]Hangar1[/b]), find the [b]HangarPartResizer[/b] module and multiply the value of the [b]size[/b] parameter by 2 ([url=http://imgur.com/crqY6jM]see the HOWTO[/url] for details).
[/list]
[/list]
[*]After that, [b]if[/b] you have installed the [b]Deprecated Parts Addon[/b], uninstall it.
[/list]
[SIZE=3][b][url=https://github.com/allista/hangar/blob/development/ChangeLog.md]ChangeLog[/url] - [b]read it carefully every time before installing a new version![/b][/b][/SIZE]

[SIZE=3][B]Known Issues[/B][/SIZE]
[list]
[*]Hangars:[list]
[*][b]In editor[/b] [i]Inline[/i] Hangars allow to store vessels that are actually bigger then hangar's internal compartment, so when such vessels are launched they collide with the hangar and explode. The problem is not trivial and I'm working on it, but for now [b]be careful when storing something that fits tightly[/b] in these hangars and make test launches. Sorry for the inconvenience.
[*]If you try to store a vessel that has some [b]resizable[/b] parts [b]in editor[/b], calculated vessel's dimensions are incorrect. It may result in the storing of a vessel that does not really fit, as well as the other way around.
[/list]
[*]GUI:[list]
[*]Dropdown lists show vertical scrollbars when there are too many items. But due to the implemented click-through prevention mechanism the scrollbars cannot be moved by mouse cursor; use mouse wheel instead. [i]And curse Unity3D for the poor GUI API.[/i]
[/list]
[*]Rovers:[list]
[*]Rovers stored [b]in editor[/b] have somewhat smaller dimensions due to inactive suspension of the wheels. So if you pack several rovers [b]tightly[/b] into a hangar, and than launch one of them, the launched rover sometimes cannot be stored again into that same hangar with the "No room ..." message. Again: it's no bug, calculations are performed correctly, the rover's just got bigger.
[/list]
[/list]
[SIZE=3][B][URL="https://github.com/allista/hangar/milestones"]See what's comming[/URL][/B][/SIZE]

[SIZE=3][B][COLOR=#006400]NOTE:[/COLOR] Before using a hangar, study the list of modules that are integrated into it (RMB on part's icon)[/B]. Many of the hangars have plenty of modules (like batteries, command modules, fuel tanks, etc.) to reduce part count. Don't worry, all is balanced by weight and cost, no cheating.[/SIZE] 

[SIZE=3][B]Introduction[/B][/SIZE]

Have you ever wanted to launch 5 satellites in one go? You did so? Then how about 10? Does your maintenance ship orbit Kerbal without work for the third month with that lonely Kerberonaut on board? Do you wish to build a giant carrier filled with scouts, landing modules and so on, but [I]with the part count below a hundred?[/I]Are you tired of the rovers standing here and there now that your colony is fully functional?

Our hangars is the answer to all these questions and to many more! Using a hangar you can store any vessel indefinitely, safe from the harsh conditions of open space or dusty moons. There you can refill it and change its crew. You can also pack some vessels into the hangar right at KSC and launch them to orbit. You can even live in some hangars alongside with your ship or rover! Clean your orbit, colony and CPU from the burden of precious but rarely needed vessels, use AT Industries(TM) Hangars now!

[imgur]Z916l[/imgur]

[SIZE=3][B]Downloads:[/B][/SIZE]
All releases, addons and packs, as well as the source code are published on [b]GitHub[/b] and may be download from there:
[URL="https://github.com/allista/hangar/releases"][b]Releases with their change-logs[/b][/URL].
[url=https://github.com/allista/hangar/raw/master/DeprecatedParts/DeprecatedPartsAddon.zip][b]Deprecated Parts Addon[/b][/url].
[URL="https://github.com/allista/hangar/raw/master/DesaturatedTexturePack/DesaturatedTexturePack.zip"][b]Desaturated Texture Pack[/b][/URL] [i]is now officially maintained[/i].
[URL="https://github.com/allista/hangar/tree/master"][b]Source code[/b][/URL].

[SIZE=3][B]Features[/B][/SIZE]
[LIST]
[*]Hangars are fit for any application:
[LIST]
[*]small light and cheap as well as huge, packed with all needed modules 
[*][B]most may be rescaled[/B] to the needed size and proportions [B]via tweakables[/B] (mass, volume and cost are changed accordingly) 
[/LIST]
[*] There are several types of hangars:
[LIST]
[*][B]In-line hangars[/B] (simple and habitable) for spaceships 
[*][B]Ground hangars[/B] (simple and habitable) for colonies 
[*][B]Rover Lander[/B] hangar that has all needed modules and fuel to autonomously land on a planet or moon, bringing some rovers along the way 
[*]there's also the [B]Spaceport[/B] that combines a huge hangar with a cockpit; as such, the Spaceport has only a single stack node at its bottom 
[/LIST]
[*]In-line hangars are equipped with internal docking port for easy targeting. If the hangar is inactive, this port may be used for normal docking 
[*]Ground hangars have anchoring modules for comfort use on low-gravity worlds and integrated probe cores with antennas for autonomous operation 
[*]Crew and resources can be transferred between a vessel with a hangar and stored vessels 
[*]Smart internal machinery ensures optimal filling of a hangar and mass distribution, while preventing attempts to store objects that do not fit in 
[*]A hangar can be filled with vessels at construction time (NOTE: a vessel with a filled hangar will stutter for a second or two upon launch; that's normal) 
[*]An asteroid can also be stored in a hangar. If it fits, of course. Interface:
[LIST]
[*]Hangars are controlled with a dedicated GUI 
[*]For the vessels that do not have any hangars the GUI shows their volume and dimensions. In Editor there's also an option to display arrows that indicate vessel's orientation, which is helpful in rover design. 
[*]A vessel can have multiple hangars. Provided GUI allows easy switching between them by highlighting the hangar that is currently selected 
[/LIST]
[*] In addition, several other parts are provided:
[LIST]
[*]Powerfull 5-way RCS thrusters for Spaceport,
[*]Square Heatshild with space for engines for Rover Lander. Especially helpful if you're playing with DeadlyReentry,
[*]Two resizable Radial-to-Stack adapters, one with a single stack node and an aerodynamic cap, the other with two symmetrical stack nodes,
[*]Resizable Station Hub which is analogous to the HubMax Multi-Point Connector, except that its radial nodes are placed more apart to accommodate parts that are wider than their attach nodes. 
[*]Universal Stack Adapter, which has separate tweakable sizes of all stack nodes and thus may be used to join any two stack parts, rescaled or not.
[/LIST]
[/LIST]
[SIZE=3][B]Requirements[/B][/SIZE]
[LIST]
[*]Hangar uses [URL="http://forum.kerbalspaceprogram.com/threads/81496"]KSPAPIExtensions[/URL] by [URL="http://forum.kerbalspaceprogram.com/members/100707-swamp_ig"]swamp_ig[/URL]. This plugin is bundled with the Hangar as it should be. 
[*]The [url=http://forum.kerbalspaceprogram.com/threads/55219]ModuleManager[/url] is required if you are using the DeprecatedPartsAddon, or if you want to get the enhancements of the supported modes (see below).
[/LIST]
[SIZE=3][B]Recommended mods[/B][/SIZE]
There are many great mods out there that I love and use myself. But the one mode that I strongly recommend to use with the Hangar to improve game experience is the [URL="http://forum.kerbalspaceprogram.com/threads/59545"][B]Extraplanetary Launchpads[/B][/URL] by [URL="https://github.com/taniwha-qf"]Taniwha[/URL]. For one thing: big ground hangars are not suitable as parts for vessel construction and are too heavy to launch anyway. So the only meaningful way to use them is to build them on site.
Also if you want to avoid many problems when building a rover that you  plan to store inside a hangar, I strongly recommend to use the [URL="http://forum.kerbalspaceprogram.com/threads/43208"]Select Root[/URL] and [URL="http://forum.kerbalspaceprogram.com/threads/38768"]Editor Extensions[/URL].

[SIZE=3][B]Supported mods[/B][/SIZE] 
Hangar supports [URL="http://forum.kerbalspaceprogram.com/threads/79745-0-24-2-KSP-AVC-Add-on-Version-Checker-Plugin-1-0-4-KSP-AVC-Online"]KSP Addon Version Checker[/URL]. 
And some functionality is added to hangars if the following mods are installed:
[LIST]
[*][URL="http://forum.kerbalspaceprogram.com/threads/40667?p=1281444&viewfull=1#post1281444"]TAC Life Support [B]beta[/B][/URL] adds life support resources to inhabitable hangars 
[*][URL="http://forum.kerbalspaceprogram.com/threads/83305"]RemoteTech2[/URL] adds RT antennas and SPUs to controllable hangars 
[*][URL="http://forum.kerbalspaceprogram.com/threads/54954"]Deadly Reentry[/URL] adds integrated heatshield to lander hangars 
[*][url=http://forum.kerbalspaceprogram.com/threads/59545]Extraplanetary Launchpads[/url] adds a new Heavy Recycler model that fits the style of hangars.
[/LIST]
[SIZE=3][B]Usage details[/B][/SIZE]

[SIZE=2][B]Hangars in general[/B][/SIZE]
All hangars are parts and thus may be added to the vessel at construction. Hangars have gate(s) which may be open or closed; in addition, internal machinery of a hangar may be deactivated or activated again.All controls and information about a hangar are located in the dedicated GUI window that may be summoned through the context menu of any hangar (menu entry "Show Controls") or through the Toolbar button.

[B]Storing a vessel[/B]

[B]Normal operation[/B]
Inside a hangar near the docking port there is a region of space controlled by the machinery. Every object intersecting with that region is automatically proccessed by the hangar and is stored if:
[LIST]
[*]the hangar is activated and its gates are opened 
[*]a vessel fits into the hangar and the hangar has enough free space inside 
[*]the relative speed of the hangar and the vessel is less than 1 m/s and the vessel is not accelerated 
[/LIST]
Otherwise an on-screen message is displayed explaining the conditions that were not met.

[B]Store a vessel during ship construction[/B]
Select "Edit contents" entry in hangar's context menu to summon vessel selection window. There you select the type of a vessels to choose from (VAB, SPH or Subassemblies) and push the "Select Vessel" button. All stored vessels appear in the same window in a list below. To remove stored vessel from the hangar push the "X" button corresponding the that vessel. To completely clear the hangar push "Clear" button. The vessel should also fit into the hangar which should also have enough free space. If a hangar with some vessels already stored is resized and there's not enough room for all the vessels anymore, some vessels are removed from the hangar to free enough space while maintaining optimal filling.

Mass and cost of stored vessels are added to that of the hangar.

[B]Launching a vessel[/B]
A vessel can be launched from a hangar if:
[LIST]
[*]the hangar is activated and its gates are opened 
[*]the hangar is not accelerated, does not rotate, move over the surface or fly in atmosphere 
[*]nothing is docked to the internal docking port of the hangar 
[/LIST]
Otherwise an on-screen message is displayed explaining the conditions that were not met.

To launch storred vessel first select a hagar (if the vessel has several) and a vessel in that hangar from corresponding dropdown lists in the plugin's window. After that resources and/or crew may be transferred. Then perform the launch by pressing "Launch Vessel" button.

[SIZE=2][B]Ground hangars[/B][/SIZE]
Despite being parts, ground hangars are meant to be used as separate self-sufficient colony buildings. They have an additional context menu entry "Attach anchor". It allows to pin the hangar to the ground, provided it is landed and not moving faster than 1 m/s.

[SIZE=2][B]Spaceport[/B][/SIZE]
The Spaceport is meant to be used as a command module of a big ship. It has 10 crew cabins without IVA and 4 command seats in the C&C located in the observation dome. Several modules specially designed to match the requirements of that huge part are integrated:
[LIST]
[*]radioisotopic generator 
[*]reaction wheel 
[*]central computer with a data transceiver 
[*]monopropellent tank 
[*]electric batteries 
[/LIST]
[SIZE=2][B]Rover Lander[/B][/SIZE]
This hangar is a small lander on its own. It has:
[LIST]
[*]liquid fuel / oxydizer fuel tanks 
[*]monopropellent tanks 
[*]reaction wheel 
[*]integrated probe core 
[*]electric batteries 
[*]4 sides to mount radial engines or RCS thrusters, and 
[*]4 bottom nodes for stack engines 
[*]4 panels acting like hangar doors and landing legs at the same time! No suspension though. 
[/LIST]
Add four radial or stack engines to the mix, RCS thrusters if you need them, solar panels or a generator, a docking port or a decoupler to couple with the rest of a mission ship, a rover inside... and off you go!

[SIZE=3][B]Possible use cases[/B][/SIZE]

[SIZE=2][B]Launch a satellite network[/B][/SIZE]
Launching a network of small satellites was never easier: just pack them into an in-line hangar, build a ship around it and launch. In orbit launch a satellite, change its orbit as desired; repeat. The benefit of this solution is that your satellites may be as small and simple as possible, carrying just enough fuel for orbit correction. No struts, no complex carrier designs. Just one hangar.

[SIZE=2][B]Orbital station[/B][/SIZE]
If you're planning to build a station that acts as a hub for many small operations, a big hangar (or even the Spaceport) is a good choice to include in this plan.

[B]Docks for smaller ships[/B]
With a hangar you can spare the station a dozen of docking ports, not to mention the headache of frequent docking maneuvers. It also enables you to store rarely used ships clearing the orbit.

[B]Fast crew transfer with orbit-to-orbit shuttles[/B]
One station is not enough? Then include a hangar into each of your stations and crew transfers between them become fast and easy. Just use the simplest shuttles with enough fuel to go from one to another and refill them after the trip.

[SIZE=2][B]Exploration ship[/B][/SIZE]
What is better for science: a series of unmanned probes, or a full-scale mission carrying light scouts and landers packed with scientific equipment, that is able to process all the data on site? If you prefer the latter, use a hangar. It will automatically balance the payload and provide the ease of refilling of scientific vessels.

[SIZE=2][B]Bringing a rover to the moon or other planet[/B][/SIZE]
If you want to get your rovers to other planet or moon easily, without complex vessel designs for its transportation, use Rover Lander. Bring it with you, as a part of a ship with a rover in its belly. Undock it, and... land! Just like that. 

[SIZE=2][B]Rover storage for colony[/B][/SIZE]
When establishing a colony rovers are often needed. They help to find a good spot, move colony modules around, tug a not-so-perfectly landed supply ship... But as colony grows and matures they become less and less used. Some of them may be disassembled for spare parts, but some are better to have around that one time when something suddenly goes wrong. To preserve them better while clearing the area use ground hangar, so when the need arise they were filled, fixed and fully operational.

[B]And so on...[/B]

[SIZE=3][B]Acknowledgements[/B][/SIZE]
First of, I want to thank my beloved wife for her support and understanding. This work takes much time... 

I also want to thank:
[LIST]
[*] [URL="https://github.com/taniwha-qf"]Taniwha[/URL] for inspiration and advice. 
[*] [URL="http://forum.kerbalspaceprogram.com/members/20077-DragonEG"]DragonEG[/URL] for helping me to fix the friction problem. 
[/LIST]
And here're the mods which sources provided me with an understanding of how KSP API works. And with working solutions in some cases. In no particular order:
[LIST]
[*][URL="http://forum.kerbalspaceprogram.com/threads/59545"]Extraplanetary Launchpads[/URL] 
[*][URL="http://forum.kerbalspaceprogram.com/threads/53134-Kerbal-Attachment-System-%28KAS%29-0-4-7-Pipes-as-fuel-lines-and-even-fewer-explosions!"]Kerbal Attach System[/URL] 
[*][URL="http://forum.kerbalspaceprogram.com/threads/39512"]Procedural Fairings[/URL] 
[*][URL="http://forum.kerbalspaceprogram.com/threads/12384"]MechJeb2[/URL] 
[*][URL="http://forum.kerbalspaceprogram.com/threads/23979"]Kethane[/URL] 
[*][URL="http://forum.kerbalspaceprogram.com/threads/50077-0-23-5-Fusebox-electric-charge-tracker-and-build-helper-1-0-released-12-07-14"]Fusebox[/URL] 
[*][URL="http://forum.kerbalspaceprogram.com/threads/60936"]CrewManifest[/URL] 
[*][URL="http://forum.kerbalspaceprogram.com/threads/80234"]TweakScale[/URL] 
[*][URL="http://forum.kerbalspaceprogram.com/threads/24786"]Kerbal Alarm Clock[/URL] 
[*][URL="http://forum.kerbalspaceprogram.com/threads/57988"]RealChutes[/URL] 
[*][URL="http://forum.kerbalspaceprogram.com/threads/83305"]RemoteTech2[/URL]
[*][url=http://forum.kerbalspaceprogram.com/threads/70676-WIP-Procedural-Parts-The-next-phase-of-Stretchy-SRBs]ProceduralParts[/url]
[/LIST]

[URL="http://creativecommons.org/licenses/by/4.0/"][IMG]http://i.creativecommons.org/l/by/4.0/88x31.png[/IMG][/URL]
This work is licensed under a [URL="http://creativecommons.org/licenses/by/4.0/"]Creative Commons Attribution 4.0 International License[/URL].