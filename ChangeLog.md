###Delete the old version of the mod before installing a new one.
_You may keep the config.xml to save positions of GUI windows._

***

###ChangeLog###

* **v2.3.1.1**
    * Second fix for the problem with vessels launched from orbits around distant planets. I hope the issue is gone for good now.

* v2.3.1
    * Fixed the problem with vessels launched from orbits around distant planets.
    * Fixed the problem with the scale of cloned/mirrored parts.
    * Added Radial Hangar in an optional package.

* v2.3.0.2
    * A hotfix for the Components tank type by [**Thorbane**](http://forum.kerbalspaceprogram.com/members/78247-Thorbane).

* v2.3.0
    * Added **Fairing Hangar** which could be used to encapsulate upper stage to reduce part count. It is activated through staging, automatically changes its crew capacity to accommodate the crew of the upper stage, and transfers maneuver nodes and control state when the upper stage is launched.
    * Added **Science Recovery** to hangars: if you recover a vessel with a hangar, inside which another vessel with collected science data is stored, this science data is also recovered.
    * [**Thorbane**](http://forum.kerbalspaceprogram.com/members/78247-Thorbane) added **support of USI** (MKS/OKS, FFT, Karbonite, Karbonite+) **and initial support of KSPI to Switchable Tanks**.
    * Different **Tank Types have different cost** per volume now.
    * Reworked vessel launch framework: works faster, no more scene switching (black screen).
    * Rebalanced integrated reaction wheels. Their torque and mass change less when they're resized.
    * Fixed several bugs in Switchable Tanks, including the clone/mirror bug. The drawback, though, is that in editor you can only change tank types and resources through the Tank Manager interface now. Thanks to [Thorbane](http://forum.kerbalspaceprogram.com/members/78247-Thorbane) for the report!
    * Fixed the problem with resized heat-shields.

* v2.2.1
    * Found a workaround for hangar-triggers' behavior in editor: **surface-attachable parts do not snap to triggers anymore**, so you can use use any hangar as a cargo bay (including converted stock bays themselves). Thanks again to [**Errol**](http://forum.kerbalspaceprogram.com/members/121831-Errol) for pointing me to this issue.
    * Added available volume display to Hangar Tank Manager. Now the tank editor's window title looks like `"Available Volume: 34m3 of 117m3"`.
    * Fixed several minor bugs and corrected all (I hope) spelling errors.

* v2.2.0
    * **Spaceplane Hangars**:
        * Converted stock cargo bays into hangars with limited functionality: no resource transfer, in-editor storage only, single vessel only; they're basically fairings.
        * Added full-featured heavy hangar that matches Mk3 parts.
        * Added several aux parts for spaceplanes:
            * Surface-attachable structural tail to offset control surfaces.
            * An airbrake.
            * Engines to haul the Mk3 monster: radial SABRE engine, heavy-duty radial rocket engine and radial high-bypass turbofan.
            * Radial electric propeller and turboshaft electric generator for hover-craft builds. I recommend to use them with [Throttle Controlled Avionics](https://kerbalstuff.com/mod/510/Throttle%20Controlled%20Avionics%20-%20Continued) mod that I also maintain.
            * *Note: the turbofan and propeller have additional pressure curves that decrease their thrust at high altitudes. This is not as sophisticated as AJE, but still adds a good measure of realism without additional dependencies.*
    * **Texture overhaul**:
        * Retextured everything (*except ground hangars*) in a grey-metallic style, closer to stock look&feel.
        * Merged/removed 10 big textures and normal maps to decrease memory footprint.
    * **General changes**:
        * Added **automatic ship positioning**: stock Cargo Bays, Inline Hangars and the Spaceport do not require stored vessel to be oriented in any special way anymore. If a vessel fits in some orientation it is stored and launched in this orientation. This addresses the problem with storing some very short and wide designs. Thanks to [**Errol**](http://forum.kerbalspaceprogram.com/members/121831-Errol) for pointing me to this issue.
        * **Changed behavior of resource converters** (including Asteroid Drill):
            * Conversion Rate now has inertia and changes with finite speed.
            * Some converters generate heat (proportional to current rate).
            * When the rate is below Minimum Rate no conversion is performed, but the energy is still consumed (startup phase). This is particularly evident in Mobile Smelter, which starts its conversion at >70% only.
        * **To Rover Lander** and MK3 Hangar **added a slider** (part menu) **that controls hangar doors**: the less the percentage, the less the doors are opened. It is useful to decrease inclination of the ramp when there's some space underneath the hangar.
        * Added **Unfit vessels list** to hangar content editor window to help in hangar fitting for the payload.
        * Resource Containers cost more with each additional tank inside them.
        * Added every part as a test subjects. Plenty of **contracts from AT Idustries**!
        * FAR/NEAR users: all hangars are now classified as **FAR Cargo Bays**.
        * [**Kerbas-ad-astra**](https://github.com/Kerbas-ad-astra) added config for **AntennaRange** and corrected some spell errors I made. Thanks!
        * Made several performance improvements using [ksp-devtools](https://github.com/angavrilov/ksp-devtools).
        * Fixed numerous bugs.

* v2.0.2.2 - CKAN compatibility
    * Reverted local copying of KAE .dll
    * Hopefully fixed the issue with Hangar.dll loading prior to KSPAPIExtensions.dll. Now it should load even without the local copy of KAE.
    
* v2.0.2
    * Added custom **part filter** by function for hangars.
    * Fixed in-editor check of a stored vessel's dimensions on hangar resize. Hangars again remove stored vessels if they don't fit anymore.
    * Fixed _(I hope)_ the problem with invalid biomes of vessels launched from a hangar.
    * Updated KSPAPIExtensions.dll to the latest (1.7.2.2) version
    * Various small fixes.

* v2.0.1
    * **Compatible with KSP-0.90.** 
    * _But incompatible with KSP-0.25._
    * Added custom configuration for Advanced Texture Manager.
    * **Removed VAB/SPH/Subassembly choice** from the Hangar Contents Editor window. Unfortunately 0.90 API does not allow to set the directory for the Craft Browser window anymore. So now only VAB and SPH lists are available; **it's impossible to store Subassemblies in a hangar.**
    * Fixed several bugs resulted from the 0.90 API changes.
    * Fixed internal docking port checks in Inline Hangars and Spaceport.
    * Improved memory usage (a little):
        * Unified docking port models for Inline Hangars, Spaceport and Resizable Docking Port.
            * **NOTE:** to allow crew transfer the Inline Hangar now has **size 1** docking port, not size 0.
        * Some textures were merged, and some resized.
        

* v2.0.0
    * Lowered drag of the Habitable Inline Hangar a little (rounded ends should count for something).
    * Wrote a proper [**documentation**](https://github.com/allista/hangar/wiki).
    
* v2.0.0-beta.1
	* Fixed mass calculation on resize of a hangar that has some vessels stored in editor.
	* Corrected definition of resources: no need to install Extraplanetary Launchpads for converters to work.
	* Fixed the bug that caused a part with configurable switchable tanks to lose tanks configuration on "Revert to VAB/SPH".
	* Prefilled switchable tanks do not fill themselves on each resource switch anymore.
	* **Inflatable Hangars now allow to store *only one vessel at a time*.**
	* Rebalanced masses: 
		* Inline Hangars, Ground Hangars, Spaceport and Rover Lander are little lighter now,
		* **But** Hangar Extensions are heavier due to the machinery that allows to transfer vessels between them.
		* Mobile Smelter also became heavier as its hull is made of steel to withstand the heating.
	* Reduced drag of the Procedural Adapter.
	* Added more info for Passage, Storage and Hangars modules in part library.
	* Parts with HangarStorage module now always reset their base mass to the value from part.cfg.

* v2.0.0-beta.0

	* I has rewrote the core implementation of Hangar module to allow for new features to be added. It should **not** break any saves by itself, but there's so much new in the code that some nasty bugs are inevitably there at this point.
	* Three major features are added: 
		* **Hangar Extensions**
			* These are parts that do not have docking space with doors to accept vessels, but they can store vessels inside and if you _connect them to a hangar correctly_, they extend its storage capabilities. The key thing is: each extension and _some_ of the hangars (for now only the small inline hangar supports this) has their stack attach nodes marked as _Hangar Passages_ (see part description in part library); a ship may only pass from one part to another through a connected pair of such nods, and only if their size allows. So by connecting extensions through Hangar Passages you may build an extensive and complex storage space with only a single hangar to accept and launch vessels. Provided Radial Adapters and Docking Ports (but not the Station Hub) also support this mode, so you may even add storage space by docking. Or directly transfer stored vessel from one carrier to another without launching it first.
		* **Asteroid Hangars**
			* Yes. This is what it sounds to be: you can now build a hangar _inside_ an asteroid. *But* it is not an easy task to do. Aside from a _big_ asteroid you need to:
				1. attach the **Asteroid Hatch** to the asteroid. The Hatch is a special grapple device that may be fixed to the asteroid permanently. It should also be equipped with the special docking port which is provided as well.
				2. dock to The Hatch with the **Asteroid Drill** and _slowly_ burrow a hole in the asteroid large enough to hold ships
				3. undock the Drill and dock the **Asteroid Gateway** -- a part that enables you to accept vessels inside the asteroid and launch them back.
			* **NOTE**: this framework **is not compatible** with the USI Asteroid Recycling Technologies. Both mods **may** be installed at the same time, but **you cannot use the same asteroid** both as a hangar and by ART machinery. I've wrote some dirty protection code preventing this from happening and you are free to test it, but in the stable release I would encourage users to use an asteroid with one of the mods only. Besides, as I will explain below, an asteroid Hangar has more or less the capabilities as the ART-utilized asteroid.
		* **Switchable Resource Containers and Converters** which are needed for the Asteroid Hangars
			* The challenge of creating a hangar inside an asteroid is even more complex, as digging produces grinded Rock resource which you should dispose of or convert: Rock->Ore+Silicates; Ore->Metal+Slag. And while Slag is a waste product and Silicates are only usable if you are playing with MKS/OKS, the Metal may be used to **build resource containers inside the asteroid**. Each container has a type that defines which resources it may hold. If a tank is empty its resource may be switched within the type. This functionality is available through the part menu of the **Asteroid Hatch**.
			* Also, several parts with the same capability are provided and may be used to build cargo vessels of broad specialization. **In Editor** these parts may also be partitioned into several sub-containers of different types.

* 1.3.0
    * **Recompiled for KSP 0.25.0**
    * Added **Inflatable Ground Hangars**. They are very light, cheap and tough. You can drop them from orbit using smallest thrusters and parachutes, then fix them to the ground with the anchor, inflate them and store rovers inside. If they're not needed you may deflate them and pack again. The downside, though, is that you can't use resource or crew transfer inside of these.
    * Added **Small Square Heatshield** for Inflatable Hangars.
    * Added a **solar panel on the roof** of the Small Ground Hangar.
    * Rearranged size limits, hangars and other parts on the Tech Tree. [**See the corresponding picture**](http://i.imgur.com/oYhZyHG.png). Unfortunately, introduction of new hangars often requires this to maintain in-game balance.
    * Added **dummy parts to TechTree nodes** corresponding to resize limits changes and RemoteTech upgrades (if you have it installed).
    * **Fixed entry costs of all parts**. Now they cost, and cost a lot.
    * Fixed Heavy Recycler not appearing on the TechTree.
    * Heavily reworked mass and cost calculations. As a result, **most hangars are much lighter** now.
    * Made some remodeling:
        * Standard ground hangars now have much thinner walls and doors,
        * Rover Lander has a better looking top node and additional set of reinforcement ribs on the side doors. These prevent the door from sometimes passing through the ground.
    * The drag of cube-shaped hangars and heatshields was increased.
    * The **maximum temperatures of all parts were decreased drastically** and are closer to the values from DeadlyReentry. Honestly, how can aluminum can endure 3400C when Al melts at 660C?!
    * If you use DeadlyReentry: the amount of Ablative Shielding on heatshields is now increases only as area, ignoring aspect changes.
    * Updated support of **TAC LS to version 0.10**. Note, that this version is not compatible with version 0.9.*, so you have to update TAC LS if you use it with the Hangar.
    * Made all **hangars Inactive by default**.
    * Fixed the strength of the stack nodes of resizable part not being updated on resize.
    * Fixed one of the rare occurring bugs with Procedural Adapter which caused some attached parts to be missing and the whole ship-construct to break the game.
    * [**See the list of changes and bugfixes**](https://github.com/allista/hangar/issues?q=milestone%3A%22Inflatable+Ground+Hangars%22+is%3Aclosed) and [commit history](https://github.com/allista/hangar/commits/master) for more...

* v1.2.1
    * Added an option to launch vessels from a hangar **in orbit** with a slight push. Launching small probes that don't have RCS is much simpler now.
        * _When a vessel is pushed out of the hangar or is stored at non-zero relative velocity, **the hangar itself receives a push** to conserve impulse of the system._
    * Added **+1** button to Hangar Contents Editor window to easily add multiple identical vessels in Editor.
    * Changed center of mass of Inhabitable hangars so that it is closer to geometric center of the parts.
    * Inline hangars and Spaceport (_the hangars with non-box-shaped hangar space_) now refuse to store vessels that fit by dimensions **but** may actually collide with hangar walls when launched.
    * Active hangars and opening/closing hangar doors **consume electric charge now**.
    * Opened hangar doors now increase **Drag** of hangars. In case of the Rover Lander this increase (as expected from its geometry) is considerable.
    * Rebalanced parts' masses and costs. Inter alia: Rover Lander and Inline Hangars became lighter and Station Hub does not gain so much weight when resized.
    * Reworked rover wheels friction workaround a little. It uses ModuleManager now, so _without MM your rovers will slide on a hangar floor_.
    * Fixed Heavy Recycler cost. As well as cost calculations of all resizable parts that contain resources.
    * Fixed problem with storing ships containing resizable parts **in editor**.
    * Fixed problem with launching of ships containing several hangars with something stored **in editor** in more than one of them.
    * [**See the full list of changes and bugfixes...**](https://github.com/allista/hangar/issues?q=milestone%3A%22Improved+Functionality+Update%22+is%3Aclosed)
* v1.2.0
    * Added resizable **Radial-to-Stack Adapters** and the 6-node **Station Hub** with elongated radial tubes to connect parts that are wider than their attach nodes.
    * Added **Procedural Adapter** with separately resizable stack nodes to connect resized hangars with other parts seamlessly.
    * Added resizable **Heavy Recycler** for those who have *Extraplanetary Launchpads* (the part will not appear if ExLP is not present). It is more powerful, has integrated lights and metal storage. Its model matches more or less the style of hangars and its trigger area is much smaller and is hidden between two arms that help to prevent accidental recycling and add a little bit of realism.
    * **REMOVED** S4-S3 and S4-S2 adapters. If you used them in some ships that are currently in flight, install the DeprecatedParts addon, or it'll break your save.
    * Reworked the model of the **Small Inline Hangar**. It **is not rounded anymore and has size2 attach nodes.** Textures for both Inline Hangars were improved a little.
    * Changed the default size (as indicated in the tweakable of a resizable part) of small *Inline Hangar* and *Ground Hangar*. They are now considered to be size2 parts. This means they can only be scaled up to twice their original size and can never be as large as their Inhabitalbe counterparts. This gives some specialization to these parts and improves ingame balance.
    * Rebalanced TechTree distribution of sizes and parts. [**See the corresponding picture**](http://i.imgur.com/fG1EGOX.png).
    * Corrected Mass, Cost and Entry Cost calculations for all parts. Some became a little heavier and more expensive, some lighter and cheaper; and for all of them the Entry Cost is increased to be more stock-alike. But the more expensive the part, the less its Entry Cost in percentage.
    * Added side-walls to ramps of the Rover Lander, and made the ramps open 10 degrees more to be able to fit all low profile engines underneath, including Aerospike and Puddle.
    * Added officially maintained [**DesaturatedTexturePack**](https://github.com/allista/hangar/raw/master/DesaturatedTexturePack/DesaturatedTexturePack.zip).
    * [**See the full list of changes and bugfixes...**](https://github.com/allista/hangar/issues?q=milestone%3A%22Visual+Quality+and+Usability+Update%22+is%3Aclosed)
* v1.1.1.1 - **Hotfix for the [issue #42](https://github.com/allista/hangar/issues/42)**
    * Rovers are now launched at the planet/moon where the launching hangar is landed. Sorry for that stupid bug ^_^'
    * Improved DropDownList: ScrollView now takes all the space to the bottom of the window and the button is wide enough to contain the longest item from the list. Also numbering of items starts from 1 now.
* v1.1.1
    * Added support for the stock AppLauncher. **The Toolbar is no longer required**, but _it takes priority over the AppLauncher_ if installed.
    * Added option to show in Editor arrows indicating ship's forward and downward directions; such arrows are also shown for each hangar indicating orientation of a launched vessel. This should be especially **helpful in rover construction**, as rover's orientation often differs from the orientation of its control part.
    * Added names to hangars and an option to rename a hangar through a context menu.
    * **Removed docking ports from the _ground_ hangars**. If you have something somehow docked to these ports, **undock it prior to updating**.
    * Important bugfixes ([see the full list](https://github.com/allista/hangar/issues?q=milestone%3A%22v1.1.1+-+multiple+bugfixes+and+some+improvements%22+is%3Aclosed+-label%3Atask+-label%3Ainvalid)):
        * Fixed the problem with friction between rover wheels and hangar's floor.
        * GUI Windows are now constrained to the screen boundaries.
        * Hangar selector is now shown only if there're multiple hangars in the same vessel.
        * A ship with launch clamps cannot be stored inside a hangar in Editor anymore.
* v1.1.0
    * Added Rover Lander Hangar to easily land rovers on planetary bodies
    * Added (proper) support for TAC Life Support, RemoteTech2 and DeadlyReentry (a heatshield for Rover Lander is included)
    * Added recalculation of the amounts of resources on part resize, as well as several other properties of some modules. Unfortunately, I have to replicate some of TweakScale's functionality here; my part resizer is more specialized and TweakScale can't replace it, as much as want it to.
    * [Fixed issues](https://github.com/allista/hangar/issues?q=is%3Aissue+is%3Aclosed): 1, 5, 6, 7, 8, 9, 11, 13, 14, 16, 17
    * Hotfixed Spaceport tech tree position (1:34, 7 Aug 14) 
* v1.0.5324 -- Initial release