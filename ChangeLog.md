###If you're upgrading _from v1.1.1.1 or below_, follow these instructions:

1. **If** your savegame contains *landed* **Rover Lander** *with opened doors*,
    * switch to it **before** upgrading and **close the doors**.
2. Delete the old version of the mod before installing this one.
_You may keep the config.xml to save positions of GUI windows._
3. Install the new version.
4. **If** your savegame contains:
    * Any ship that includes:
        * **S4-S3 or S4-S2 adapters**
        * *small* **Inline Hangar**
        * *small* **Ground Hangar**:
    * install the [**Deprecated Parts Addon**](https://github.com/allista/hangar/releases/download/v1.3.0/DeprecatedParts-v1.1.1.1_to_v1.3.zip), then recover these ships.
5. After that, **if** you have installed the **Deprecated Parts Addon**, uninstall it.

***

###Delete the old version of the mod before installing the new one.
_You may keep the config.xml to save positions of GUI windows._

***

###ChangeLog###

* **1.3.0** - _IF you are upgrading **from v1.1.1.1 or below**_, READ THE INSTALLATION INSTRUCTIONS CAREFULLY
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