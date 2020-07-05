###Delete the old version of the mod before installing a new one.
_You may keep the Hangar.user (if you have one) and config.xml files to preserve your settings._

***

* **v3.6.0.1**
    * Compatible with KSP 1.10
    * Fixed solr panel config of the Small Ground Hangar

* v3.6.0
    * **Fairings**
        * Added **Jettison Power** PAW control to be able to change
        both jettison force, torque and launch velocity of the payload
        in flight.
        * Added **Debris Destruction In** PAW control that, if set to a values
        greater than zero, arms delayed action demolition charges in fairing 
        debris (including the base, if it's not attached to anything).
        * Disabling relevant Decouplers and other PAW controls after jettisoning. 
        * Added **hibernation** to command modules
        * If fairings has other parts attached to it, these parts are now
        jettisoned with _limited_ force *3 seconds before* the payload is
        jettisoned (_to avoid collisions with the debris, while not launching
        them into the Sun_).
        * Improved and fixed box fairings model, decreased its jettison force
        * Many fixes to the jettison logic, including CoM changes,
        linear and angular impulse conservation and others.
    * Allowing to change the **aspect ratio of the Small VTOL Hangar**

* v3.5.0.3
    * Added all relevant .meta files from Unity project
    * Compiled against AT_Utils 1.9.3

* v3.5.0.2
    * **Compatible with KSP-1.9**
    * Compiled against AT_Utils 1.9.2

* v3.5.0.1
    * Compiled against AT_Utils 1.9.1

* v3.5.0
    * **Magnetic Dampers**
        * All hangars except for VTOL and fairings now have the
          ATMagneticDamper module.
        * It can be switched on/off even remotely.
        * Its attenuation can also be changed in PAW in flight.
        * Orbital hangars also have attractors, i.e. they not only damp velocity
          of vessels within their premises, but can also pull them in and dock
          them inside automatically.
        * The polarity of the attractor could be changed remotely, so a vessel
          launched from an orbital hangar can order the hangar to push it
          slowly outside. Much like with the "Launch with Punch" option, but
          the vessel is physically controlled all the way by the hangar.
        * **Damper works in Time Warp**, so a vessel that is left inside the
          hangar's docking space will not fly though its walls anymore.
        * **The damper is controlled** by the hangar in that it is automatically
          activated on launch and, in case of the orbital hangars, on hangar
          activation. This **helps a lot** with launching heavy-with-many-parts
          rovers and planes that tend to jump on their wheels and explode.
          Should also help with sliding issues mentioned on the forum.
    * Hagar window remembers its **visibility per vessel**.
    * When Configurable Containers are installed, the **Universal Fuel Tank**
      is added to fuel tanks. It is basically the Procedural Adapter
      with Tank Manager and LFO tanks by default.
    * **Rover Lander** and **Mk3 Hangar** both spawn vessels at the floor now.
      No more falling from the center of the hangar.
    * Updated the Square Heatshields' configs to be more stock-like
    * Moved Square Heatshields to **Thermal category**
    * Moved non-ground and non-inflatible hangars to **Payload category**
    * Fixed the problem with Hangar Gateway that was missing the storage in
      mined asteroids after game reloading or vessel switching.
    * Fixed the problem with exploding vessels, when launched in very tight
      quarters, i.e. with some parts of the launched vessel mere centimeters
      away from the hangar walls.
    * Fixed directions of RCS plumes on the Hangar Gateway
    * Fixed costs of square heatshield
    * Fixed the problem with automatic game saves before vessel spawning.
    * Various fixes and adaptations to AT_Utils API changes.

* v3.4.1
    * Updated **Tech Tree limits for Size and Aspect**:
        * _General Construction_: size/aspect [0.75, 1.5]
        * _Miniaturization_: size/aspect [0.5
        * **Precision Engineering**: size/aspect [**0.1**
        * _Advanced Construction_: size/aspect 3]
        * _Specialized Construction_: size/aspect 6]
        * _Composites_: size/aspect 12]
        * **Meta Materials**: size/aspect **1000**]
    * _Hopefully_ fixed aerodynamics of stock cargo bays that are patched to be
      hangars instead.
    * For modders:
        * `onLaunchedFromHangar` provides additional `bool fromFairings == true` 
           data field when a vessel is launched from `HangarFairings`
           module. This may be needed, because in this case the launched
           vessel should behave as if it was just the next stage of the
           carrier.

* v3.4.0 - **ReOrientation**
    * **Supports KSP-1.8.1**
    * **Hangar contents may be rotated both in Editor and in Flight**
        * Payload may be rotated in 90 degree steps using the dedicated UI.
            The orientation is indicated by a translucent arrow drawn
            along with the content hint. The arrow indicates the bottom and
            front directions of the payload.
        * **In Flight rotation is allowed only if the rotated payload fits**
            inside docking space. And disabled for fairings and single-use
            hangars.
        * **Orientation of each stored vessel is preserved**, so if you have
            docked (in flight), say, nose-forward, that is how the vessel will
            be launched, unless rotated manually before launch. _Note: the
            orientation will be actually slightly corrected, so that the
            stored vessel is aligned with the hangar._
        * When a storred vessel is transferred from one storage to the other
            its orientation is set to the optimal one for the recieving storage.
    * **Mobile Smelter is deprecated** in favor of the ISRUs. _As usual, parts
        already in flight will be functional, but it wouldn't be available
        in Editor._
    * **Asteroid Hatch creates resource tanks using _Material Kits_.** _Existing
        hatches will still use Metals, only the new parts will be affected.
        Tanks will cost slightly more, but will weight the same._
    * **ISRUs are patched to produce _Material Kits_** the same way they do
        in Global Construction (unless MKS is installed).
    * **Vessels stored in Editor are not _spawned-and-stored_ anymore** during
        the launch, which caused considerable lag.
    * **Hangars don't launch stored vessels if any physical object is present
        within their docking space.**
    * Vessel docking is triggerred only with dedicated trigger-colliders,
        rather than with any trigger-colliders on the part, like airlocks
        and ladders.
    * Added **Show Payload** button to part menu of fairings to show their
        content as they don't have the Hangar Window interface.
    * Numerous bugfixes and improvements:
        * Localizing vessel names.
        * Fixed NRE in case a jettisoned fairing is destroyed immediately.
        * Update content_hull_mesh on hangar resize.
        * Fixed UI not showing after launch from hangar if it was destroyed
            before launch ended.
        * Several performance improvements due to the use of the frameworks
            from AT_Utils rather than the legacy Hangar code.
        * Showing tooltips in HangarEditor.
        * Added random angular velocities to debris jettisoned from fairings
            for visual effect.
        * Limit the jettison force for parts decoupled form a fairing when
            it is itself jettisonned.
        * Fixed content highlighting in Hangar Gateway.
        * Improved hangar fairings texture and model.
        * Removed deprecated Readme.pdf from the distribution.
    * For modders (and for future integration with TCA):
        * Sending the Hangar PartModule as `onLaunchedFromHangar` `KSPEvent`
            to _all_ parts of the launched vessel.

* v3.3.7
    * Using the common Color Scheme for the hangar content hint color
    * Fixed transfer window behavior when selected payload is switched
    * Added **Show** button that displays content hint for a short time
        * *this is actually a step toward user-controlled orientation of the payload*

* v3.3.6.1
    * Fixed some issues with Procedural Adapter and APR module.
    * Using latest AT_Utils

* v3.3.6
    * Added ability to change UI color scheme at runtime
        * To access the Color Scheme dialog, **right-click the Hangar toolbar button**

* v3.3.5
    * Compatible with KSP-1.7
    * Added ability to **add single parts to a Hangar in editor**.
    * Several bugfixes

* v3.3.4.5
    * **Moved** Box Fairings to Survivability node on the Tech Tree
    * **Moved** Procedural Adapter to General Construction node
    * Corrected Tech required for the heavy 5-way RCS

* v3.3.4.4
    * Fixed 5-way RCS thrust effects
    * Using latest AT_Utils

* v3.3.4.3 -- compatible with KSP01.4.5

* v3.3.4.2
    * Using new stuff from AT_Utils 1.6
    * Fixed content hull mesh rendering on hangar resize.
    * Fixed metric calculation for the stock RadialDrill part.

* _v3.4.0-1 TODO_

* v3.3.3
    * **Single Use Grapples**:
        * Fixed attachment using auto-generated AttachNodes.
        * Fixed grapple joint reinforcement.
        * Added HUD overlay for grapple contact points:
            * red line means "too far" from attach point
            * green line means "close enough"
            * when all are green, grapple is attached immediately
    * Fixed camera jumping on launch from hangar.
    * **Merged -- Asteroid Hatch + Asteroid Hatch Port** into a single part.
        * *Old parts in flight will function, but will not be available in Editor.*
    * Added two lamps on the sides of Hatch Port Adapter.
    * Increased lamp range of Structural Grapple.
    * Corrected Box Fairing mass calculation.
    * Procedural Adapter updates mesh after passage is Ready. And updates DragCubes as well.

* v3.3.2
    * Added **Box Fairings** part for easier payload delivery onto the surface of planets.
    * Added **displaying of stored vessel**'s convex hull, when storage editor is shown.
    * Hangar window is now shown after 3s after a level is loaded.

* v3.3.1.1
    * Compatible with KSP-1.3
    * Fixed some issues with Single Use Grapple.

* v3.3.1
    * Added appropriate CLS configs by Kerbas-ad-astra.
    * Added separate icons for Toolbar and AppLauncher.
    * Fixed inability to decouple the grapple/hatch after qsave/load.
    * Fixed Asteroid Gateway store/launch functionality.
    * When a vessel is docked inside a hangar the camera is now held still (instead of jump-switching) and the controls are newtralized to avoid accidental engine burns on the hangar's vessel.
    * Asteroid Gateway's docking space have strict positioning.

* v3.3.0
    * Implemented **subassembly loading into hangars** in Editor.
    * Converted both Asteroid Hatch and Structural Grapple to use the new SingleUseGrappleNode that **fixes the sliding-hatch problem**. Moved both to Coupling category.
    * Moved Hatch Port Adapter to Coupling category.
    * Fixed doubling mass by Hangar Fairings.
    * Fixed the bug that prevented modification of StoredVessel resources.
    * Fixed TotalStoredMass display.
    * Fixed the issues with vessel transfer window.

* v3.2.1.3
    * **Dropped RemoteTech and AntennaRange patches.**
    * Added GroundWorkshop module with 100% efficiency to the Big Ground Hangar (for Ground Construction mod).
    * **Fixed the NaN storage size** in DynamicStorage caused by creation of 100%V tank.
    * **Fixed hangar switching** for ships with multiple hangars.
    * Fixed several NREs.

* v3.2.1.2
    * Fixed initialization of Configurable Containers within asteroids from a savegame.

* v3.2.1.1
    * Fixed Asteroid Drill.
    * Fixed Asteroid Hatch. Moved it to the Coupling category.
    * Rebalanced Asteroid Drill:
        * Fixed heat production and thermal efficiency.
        * Made the Drill twice more efficient.
        * Increased RCS power.
    * Rebalanced Mobile Smelter:
        * Changed productivity and thermal configuration.
        * Disabled SpecialistBonus; the thing is fully automatic and should work without kerbals.

* v3.2.1
    * **KIS inventory** is now transferred with the kerbals when a ship is stored in a hangar or launched from it.
    * Implemented **seamless camera transfer** from a hangar to a launched vessel.
    * Fixed mass calculation of stored vessel (issue #177).
    * Made Asteroid Gateway into a Probe Control Point.
    * Corrected density of Fairings panels.

* v3.2.0
    * Compiled against KSP-1.2.2
    * Added **Small VTOL Hangar** that acts like a small launchpad: you land on its roof and store the vessel; you launch a vessel and it appears on the roof, then you take off.
    * MobileSmelter consumes much more power now; comparable with IRSU.
    * Moved spawn transforms of the ground hangars to the bottom-back corner.
    * For modders:
        * Added HangarEntrance module that acts like HangarGateway, but for the same part that has HangarStorage. This way you can have several different size entrances into the same big HangarStorage.
        * Renamed HangarGateway.DockingSpace to .HangarSpace
        * Added test hangars to ForModders folder (available on GitHub).
    * Bugfixes:
        * Fixed NRE in HangarWindow when "piloting" EVAs.
        * Fixed toolbar button bug.
        * Fixed debris resize problem.

* v3.1.0
    * Made it possible **vessel launching while rotating and moving, even with acceleration**.
    * Implemented **temporary storage of resources in Hangar Fairings**. This makes the Fairings **compatible with Life Support** mods.
    * Added placeholders for internals to all crewed parts. Added "back" stack nodes to ground hangars so that they could be hanged below a skycrane-like carrier.
    * In Editor positioning arrows are only drawn if a hangar has *Strict Positioning*; otherwise only a dot is drawn.
    * Bugfixes:
        * Crew transfer.
        * Prelaunch game saving.
        * Debris jettison velocity calculation.

* v3.0.1
    * Yet another fix for vessel positioning on launch.
    * The game is now autosaved before a vessel is spawned from a hangar.
    * Spaceport and Big Hangar may be used as standalone control centers (KerbNet integration).
    * Adapted to changes in Configurable Containers.
    * Moved ToolbarWrapper to AT_Utils. Updated it.

* v3.0
    * **KSP-1.2 support.**
    * **Requires:**
        * *AT_Utils* library
        * *Community Resource Pack*
    * **Changed license to MIT (sources) + CC-BY-4.0 (assets).**
    * Removed most non-hangar, non structural parts (engines, airbrake, etc.).
    * Added Inflatable Space Hangar.
    * Changed model of the Fairings Hangar.
    * In Editor: unfit vessels are automatically stored if a hangar becomes large enough when resized.
    * Asteroid Drill now produces Ore, like stock drills.
    * Mobile Smelter produces "Metals" from  Community Resource Pack. It is then used to construct resource tanks inside asteroids.
    * Improved GUI, added tooltips.
    * Many bugfixes.

* v2.3.1.1
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
