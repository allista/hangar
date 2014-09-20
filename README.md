#Hangar#

##_store your vessels until they are needed_##

***
###WARNING: this is still BETA. There should be bugs. 
While fixing them and implementing new features I'll try as hard as I can to maintain backward compatibility, _but I can't guarantee it_. So if you plan to use it in your main game, **backup your saves**.

***
###!!! v1.2.0 WARNING WARNING WARNING v1.2.0 !!!

**This update may break your saves, BUT all breaks are easily fixable.**

To **safely** install the update, do the following:

1. **If** your savegame contains *landed* **Rover Lander** *with opened doors*,
    * switch to it **before** upgrading and **close the doors**.
2. Delete the old version of the mod before installing this one.
_You may keep the config.xml to save positions of GUI windows, though._
3. Install the new version.
4. **If** your savegame contains:
    * Any ship that includes **S4-S3 or S4-S2 adapters**:
        * install the [**Deprecated Parts Addon**](https://github.com/allista/hangar/raw/master/DeprecatedParts/DeprecatedPartsAddon.zip), then recover such ship and rebuild it in editor using new Universal Stack Adapter
    * Any ship that includes *small* **Inline Hangar** or *small* **Ground Hangar**, you should either:
        * install the [**Deprecated Parts Addon**](https://github.com/allista/hangar/raw/master/DeprecatedParts/DeprecatedPartsAddon.zip), then recover such ship and rebuild it in editor,
        * or open the savegame file in any text editor, find the corresponding part (**InlineHangar1** or **Hangar1**), find the **HangarPartResizer** module and multiply the value of the **size** parameter by 2 ([see the HOWTO](http://imgur.com/crqY6jM) for details).
5. After that, **if** you have installed the **Deprecated Parts Addon**, uninstall it.
***

###[ChangeLog](https://github.com/allista/hangar/blob/development/ChangeLog.md) - **read it carefully every time before installing a new version!**###

###Known Issues###
* Hangars:
    * **In editor** *Inline* Hangars allow to store vessels that are actually bigger then hangar's internal compartment, so when such vessels are launched they collide with the hangar and explode. The problem is not trivial and I'm working on it, but for now **be careful when storing something that fits tightly** in these hangars and make test launches. Sorry for the inconvenience.
    * If you try to store a vessel that has some **resizable** parts **in editor**, calculated vessel's dimensions are incorrect. It may result in the storing of a vessel that does not really fit, as well as the other way around.
* GUI:
    * Dropdown lists show vertical scrollbars when there are too many items. But due to the implemented click-through prevention mechanism the scrollbars cannot be moved by mouse cursor; use mouse wheel instead. _And curse Unity3D for the poor GUI API._
* Rovers:
    * Rovers stored **in editor** have somewhat smaller dimensions due to inactive suspension of the wheels. So if you pack several rovers **tightly** into a hangar, and than launch one of them, the launched rover sometimes cannot be stored again into that same hangar with the "No room ..." message. Again: it's no bug, calculations are performed correctly, the rover's just got bigger.

###[See what's comming](https://github.com/allista/hangar/milestones)###

###NOTE:###
**Before using a hangar, study the list of modules that are integrated into it _(RMB on part's icon)_.** Many of the hangars have plenty of modules (like batteries, command modules, fuel tanks, etc.) to reduce part count. Don't worry, all is balanced by weight and cost, no cheating.
***

##Introduction##

Have you ever wanted to launch 5 satellites in one go? You did so? Then how about 10?
Does your maintenance ship orbit Kerbal without work for the third month with that lonely Kerberonaut on board?
Do you wish to build a giant carrier filled with scouts, landing modules and so on, but _with the part count below a hundred?_
Are you tired of the rovers standing here and there now that your colony is fully functional?

Our hangars is the answer to all these questions and to many more! Using a hangar you can store any vessel indefinitely, safe from the harsh conditions of open space or dusty moons. There you can refill it and change its crew. You can also pack some vessels into the hangar right at KSC and launch them to orbit. You can even live in some hangars alongside with your ship or rover! Clean your orbit, colony and CPU from the burden of precious but rarely needed vessels, use AT Industries(TM) Hangars now!

###Features###

* Hangars are fit for any application: 
    * small light and cheap as well as huge, packed with all needed modules
    * **most may be rescaled** to the needed size and proportions **via tweakables** (mass, volume and cost are changed accordingly)
* There are several types of hangars:
    * **In-line hangars** (simple and habitable) for spaceships 
    * **Ground hangars** (simple and habitable) for colonies
    * **Rover Lander** hangar that has all needed modules and fuel to autonomously land on a planet or moon, bringing some rovers along the way
    * **Spaceport** that combines a huge hangar with a cockpit; the Spaceport has only a single stack node at its bottom and meant to be used instead of a cockpit
* In-line hangars are equipped with internal docking port for easy targeting. If the hangar is inactive, this port may be used for normal docking
* Ground hangars have anchoring modules for comfort use on low-gravity worlds and integrated probe cores with antennas for autonomous operation
* Crew and resources can be transferred between a vessel with a hangar and stored vessels
* Smart internal machinery ensures optimal filling of a hangar and mass distribution, while preventing attempts to store objects that do not fit in
* A hangar can be filled with vessels at construction time (NOTE: a vessel with a filled hangar will stutter for a second or two upon launch; that's normal)
* An asteroid can also be stored in a hangar. If it fits, of course.
* Interface:
    * Hangars are controlled with a dedicated GUI
    * For the vessels that do not have any hangars the GUI shows their volume and dimensions. In Editor there's also an option to display arrows that indicate vessel's orientation, which is helpful in rover design.
    * A vessel can have multiple hangars. Provided GUI allows easy switching between them by highlighting the hangar that is currently selected
* In addition, several other parts are provided:
    * Powerfull 5-way RCS thrusters for Spaceport,
    * Square Heatshild with space for engines for Rover Lander. Especially helpful if you're playing with DeadlyReentry,
    * Two resizable Radial-to-Stack adapters, one with a single stack node and an aerodynamic cap, the other with two symmetrical stack nodes,
    * Resizable Station Hub which is analogous to the HubMax Multi-Point Connector, except that its radial nodes are placed more apart to accommodate parts that are wider than their attach nodes. 
    * Universal Stack Adapter, which has separate tweakable sizes of all stack nodes and thus may be used to join any two stack parts, rescaled or not.

##Requirements##

* Hangar uses [KSPAPIExtensions](http://forum.kerbalspaceprogram.com/threads/81496) by [swamp_ig](http://forum.kerbalspaceprogram.com/members/100707-swamp_ig). This plugin is bundled with the Hangar as it should be.
* The [ModuleManager](http://forum.kerbalspaceprogram.com/threads/55219) is required if you are using the DeprecatedPartsAddon, or if you want to get the enhancements of the supported modes (see below).

##Recommended mods##

There are many great mods out there that I love and use myself. But the one mode that I strongly recommend to use with the Hangar to improve game experience is the [**Extraplanetary Launchpads**](http://forum.kerbalspaceprogram.com/threads/59545) by [Taniwha](https://github.com/taniwha-qf). For one thing: big ground hangars are not suitable as parts for vessel construction and are too heavy to launch anyway. So the only meaningful way to use them is to build them on site.

Also if you want to avoid many problems when building a rover that you plan to store inside a hangar, I strongly recommend to use the [Select Root](http://forum.kerbalspaceprogram.com/threads/43208) and [Editor Extensions](http://forum.kerbalspaceprogram.com/threads/38768).

##Supported mods##

Hangar supports [KSP Addon Version Checker](http://forum.kerbalspaceprogram.com/threads/79745-0-24-2-KSP-AVC-Add-on-Version-Checker-Plugin-1-0-4-KSP-AVC-Online). 

And some functionality is added to hangars if the following mods are installed:

* [TAC Life Support **beta**](http://forum.kerbalspaceprogram.com/threads/40667?p=1281444&viewfull=1#post1281444) adds life support resources to inhabitable hangars,
* [RemoteTech2](http://forum.kerbalspaceprogram.com/threads/83305) adds RT antennas and SPUs to controllable hangars,
* [Deadly Reentry](http://forum.kerbalspaceprogram.com/threads/54954) adds integrated heatshield to lander hangars,
* [Extraplanetary Launchpads](http://forum.kerbalspaceprogram.com/threads/59545) adds a new Heavy Recycler model that fits the style of hangars.

##Usage details##

###Hangars in general###

All hangars are parts and thus may be added to the vessel at construction. Hangars have gate(s) which may be open or closed; in addition, internal machinery of a hangar may be deactivated or activated again.
All controls and information about a hangar are located in the dedicated GUI window that may be summoned through the context menu of any hangar (menu entry "Show Controls") or through the Toolbar button.

####Storing a vessel###

#####Normal operation###
Inside a hangar near the docking port there is a region of space controlled by the machinery. Every object intersecting with that region is automatically proccessed by the hangar and is stored if:

* the hangar is activated and its gates are opened
* a vessel fits into the hangar and the hangar has enough free space inside
* the relative speed of the hangar and the vessel is less than 1 m/s and the vessel is not accelerated

Otherwise an on-screen message is displayed explaining the conditions that were not met.

#####Store a vessel during ship construction###
Select "Edit contents" entry in hangar's context menu to summon vessel selection window. There you select the type of a vessels to choose from (VAB, SPH or Subassemblies) and push the "Select Vessel" button. All stored vessels appear in the same window in a list below. To remove stored vessel from the hangar push the "X" button corresponding the that vessel. To completely clear the hangar push "Clear" button. The vessel should also fit into the hangar which should also have enough free space. If a hangar with some vessels already stored is resized and there's not enough room for all the vessels anymore, some vessels are removed from the hangar to free enough space while maintaining optimal filling.

Mass and cost of stored vessels are added to that of the hangar.

####Launching a vessel###

A vessel can be launched from a hangar if:

* the hangar is activated and its gates are opened
* the hangar is not accelerated, does not rotate, move over the surface or fly in atmosphere
* nothing is docked to the internal docking port of the hangar

Otherwise an on-screen message is displayed explaining the conditions that were not met.

To launch storred vessel first select a hagar (if the vessel has several) and a vessel in that hangar from corresponding dropdown lists in the plugin's window. After that resources and/or crew may be transferred. Then perform the launch by pressing "Launch Vessel" button.

###Ground hangars###

Despite being parts, ground hangars are meant to be used as separate self-sufficient colony buildings. They have an additional context menu entry "Attach anchor". It allows to pin the hangar to the ground, provided it is landed and not moving faster than 1 m/s.

###Spaceport###

The Spaceport is meant to be used as a command module of a big ship. It has 10 crew cabins without IVA and 4 command seats in the C&C located in the observation dome. Several modules specially designed to match the requirements of that huge part are integrated:

* radioisotopic generator 
* reaction wheel
* central computer with a data transceiver
* monopropellent tank
* electric batteries

###Rover Lander###

This hangar is a small lander on its own. It has:

* liquid fuel / oxydizer fuel tanks
* monopropellent tanks
* reaction wheel
* integrated probe core
* electric batteries
* 4 sides to mount radial engines or RCS thrusters, and
* 4 bottom nodes for stack engines
* 4 panels acting like hangar doors and landing legs at the same time! No suspension though.

Add four radial or stack engines to the mix, RCS thrusters if you need them, solar panels or a generator, a docking port or a decoupler to couple with the rest of a mission ship, a rover inside... and off you go!

##Possible use cases##

###Launch a satellite network###

Launching a network of small satellites was never easier: just pack them into an in-line hangar, build a ship around it and launch. In orbit launch a satellite, change its orbit as desired; repeat. The benefit of this solution is that your satellites may be as small and simple as possible, carrying just enough fuel for orbit correction. No struts, no complex carrier designs. Just one hangar.

###Orbital station###

If you're planning to build a station that acts as a hub for many small operations, a big hangar (or even the Spaceport) is a good choice to include in this plan.

####Docks for smaller ships###

With a hangar you can spare the station a dozen of docking ports, not to mention the headache of frequent docking maneuvers. It also enables you to store rarely used ships clearing the orbit.

####Fast crew transfer with orbit-to-orbit shuttles###

One station is not enough? Then include a hangar into each of your stations and crew transfers between them become fast and easy. Just use the simplest shuttles with enough fuel to go from one to another and refill them after the trip.

###Exploration ship###

What is better for science: a series of unmanned probes, or a full-scale mission carrying light scouts and landers packed with scientific equipment, that is able to process all the data on site? If you prefer the latter, use a hangar. It will automatically balance the payload and provide the ease of refilling of scientific vessels.

###Bringing a rover to the moon or other planet###

If you want to get your rovers to other planet or moon easily, without complex vessel designs for its transportation, use Rover Lander. Bring it with you, as a part of a ship with a rover in its belly. Undock it, and... land! Just like that.

###Rover storage for colony###

When establishing a colony rovers are often needed. They help to find a good spot, move colony modules around, tug a not-so-perfectly landed supply ship... But as colony grows and matures they become less and less used. Some of them may be disassembled for spare parts, but some are better to have around that one time when something suddenly goes wrong. To preserve them better while clearing the area use ground hangar, so when the need arise they were filled, fixed and fully operational.

###And so on...###

#Acknowledgements#

First of, I want to thank my beloved wife for her support and understanding. This work takes much time...

I also want to thank:

* [Taniwha](https://github.com/taniwha-qf) for inspiration and advice.
* [DragonEG](http://forum.kerbalspaceprogram.com/members/20077-DragonEG) for helping me to fix the friction problem.

And here're the mods which sources provided me with an understanding of how KSP API works. And with working solutions in some cases. In no particular order:

* [Extraplanetary Launchpads](http://forum.kerbalspaceprogram.com/threads/59545)
* [Kerbal Attach System](http://forum.kerbalspaceprogram.com/threads/53134-Kerbal-Attachment-System-%28KAS%29-0-4-7-Pipes-as-fuel-lines-and-even-fewer-explosions!)
* [Procedural Fairings](http://forum.kerbalspaceprogram.com/threads/39512)
* [MechJeb2](http://forum.kerbalspaceprogram.com/threads/12384)
* [Kethane](http://forum.kerbalspaceprogram.com/threads/23979)
* [Fusebox](http://forum.kerbalspaceprogram.com/threads/50077-0-23-5-Fusebox-electric-charge-tracker-and-build-helper-1-0-released-12-07-14)
* [CrewManifest](http://forum.kerbalspaceprogram.com/threads/60936)
* [TweakScale](http://forum.kerbalspaceprogram.com/threads/80234)
* [Kerbal Alarm Clock](http://forum.kerbalspaceprogram.com/threads/24786)
* [RealChutes](http://forum.kerbalspaceprogram.com/threads/57988)
* [RemoteTech2](http://forum.kerbalspaceprogram.com/threads/83305)
* [ProceduralParts](http://forum.kerbalspaceprogram.com/threads/70676-WIP-Procedural-Parts-The-next-phase-of-Stretchy-SRBs)

***

<a rel="license" href="http://creativecommons.org/licenses/by/4.0/"><img alt="Creative Commons License" style="border-width:0" src="http://i.creativecommons.org/l/by/4.0/88x31.png" /></a><br />This work is licensed under a <a rel="license" href="http://creativecommons.org/licenses/by/4.0/">Creative Commons Attribution 4.0 International License</a>.