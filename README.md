#Hangar#

##_store your vessels until they are needed_##

***

Hangars are parts that can **store whole ships inside**. Stored ships are not docked but **unloaded**, which reduces part count considerably, improving framerate and memory footprint while allowing to carry out missions of great complexity.

Aside from the hangars themselves the mod provides a set of utility parts that help in various usage scenarios. To further reduce part count, most parts (including hangars) have multiple integrated modules and resources that are balanced and often eliminate the need to add such things as batteries, lights, generators, probe cores and reaction wheels.

##Features###

* Hangars are fit for any application: 
    * small light and cheap as well as huge, packed with all needed modules
    * **most may be rescaled** to the needed size and proportions **via tweakables** (mass, volume and cost are changed accordingly)
* There are several types of hangars:
    * **In-line Hangars** (simple and habitable) for spaceships
    * **Inflatable Ground Hangars** that may be easily dropped off from the orbit
    * **Stationary Ground Hangars** (simple and habitable) for colonies
    * **Rover Lander** hangar that has all needed modules and fuel to autonomously land on a planet or moon, bringing some rovers along the way
    * **Spaceport** that combines a huge hangar with a cockpit; the Spaceport has only a single stack node at its bottom and meant to be used instead of a cockpit
* Ground hangars have anchoring modules for comfort use on low-gravity worlds and integrated probe cores with antennas for autonomous operation
* Crew and resources can be transferred between a vessel with a non-inflatable hangar and stored vessels
* Smart internal machinery ensures optimal filling of a hangar and mass distribution, while preventing attempts to store objects that do not fit in
* A non-inflatable hangar can be filled with vessels at construction time (NOTE: a vessel with a filled hangar will stutter for a second or two upon launch; that's normal)
* **An asteroid can also be stored in a hangar**. If it fits, of course.
* And vice versa: **a hangar can be made inside and asteroid!**
* In addition, many utility parts are provided to help in various usage scenarios.
* For more information read:

##[Documentation](https://github.com/allista/hangar/wiki)

##[ChangeLog](https://github.com/allista/hangar/blob/development/ChangeLog.md)

##[Future plans](https://github.com/allista/hangar/milestones)

***

##Support this project:

[**WebMoney**](https://funding.wmtransfer.com/hangar-ksp-plugin/donate)

[![Flattr this](https://api.flattr.com/button/flattr-badge-large.png)](https://flattr.com/submit/auto?user_id=allista&url=https%3A%2F%2Fgithub.com%2Fallista%2Fhangar)

<iframe frameborder="0" allowtransparency="true" scrolling="no" src="https://money.yandex.ru/embed/small.xml?account=410011588562910&quickpay=small&any-card-payment-type=on&button-text=06&button-size=l&button-color=orange&targets=%D0%9F%D0%BE%D0%B4%D0%B4%D0%B5%D1%80%D0%B6%D0%BA%D0%B0+%D0%BF%D1%80%D0%BE%D0%B5%D0%BA%D1%82%D0%B0+%22Hangar%3A+KSP+Plugin%22&default-sum=100&successURL=" width="260" height="54"></iframe>

***

##Downloads and Installation

**Delete the old version of the mod before installing the new one.**

_You may keep the config.xml to save positions of GUI windows._

**Before any upgrade I recommend you to backup your saves.**

Releases are available from:

* [**GitHub**](https://github.com/allista/hangar/releases)
* [**Kerbal Stuff**](https://kerbalstuff.com/mod/270/Hangar) (from v1.3.0)

There's also a [**Desaturated Texture Pack**](https://github.com/allista/hangar/raw/master/DesaturatedTexturePack/DesaturatedTexturePack.zip) for those who prefer a more stock-like color scheme.

**Before using a hangar study the list of modules that are integrated into it _(RMB on part's icon)_.**
Many of the hangars have plenty of modules (like batteries, command modules, fuel tanks, etc.) to reduce part count. Don't worry, all is balanced by weight and cost, no cheating.

###Known Issues
* GUI:
    * Dropdown lists show vertical scrollbars when there are too many items. But due to the implemented click-through prevention mechanism the scrollbars cannot be moved by mouse cursor; use mouse wheel instead. _And curse Unity3D for the poor GUI API._
* Rovers:
    * Rovers stored **in editor** have somewhat smaller dimensions due to inactive suspension of the wheels. So if you pack several rovers **tightly** into a hangar, and than launch one of them, the launched rover sometimes cannot be stored again into that same hangar with the "No room ..." message. Again: it's no bug, calculations are performed correctly, the rover's just got bigger.

###Requirements

* Hangar uses [KSPAPIExtensions](http://forum.kerbalspaceprogram.com/threads/81496) by [swamp_ig](http://forum.kerbalspaceprogram.com/members/100707-swamp_ig). This plugin is bundled with the Hangar as it should be.
* The [ModuleManager](http://forum.kerbalspaceprogram.com/threads/55219) **is required**.

###Recommended mods

There are many great mods out there that I love and use myself. But the one mode that I strongly recommend to use with the Hangar to improve game experience is the [**Extraplanetary Launchpads**](http://forum.kerbalspaceprogram.com/threads/59545) by [Taniwha](https://github.com/taniwha-qf). For one thing: big ground hangars are not suitable as parts for vessel construction and are too heavy to launch anyway. So the only meaningful way to use them is to build them on site.

Also if you want to avoid many problems when building a rover that you plan to store inside a hangar, I strongly recommend to use the [Select Root](http://forum.kerbalspaceprogram.com/threads/43208) and [Editor Extensions](http://forum.kerbalspaceprogram.com/threads/38768).

###Supported mods

Hangar supports [KSP Addon Version Checker](http://forum.kerbalspaceprogram.com/threads/79745-0-24-2-KSP-AVC-Add-on-Version-Checker-Plugin-1-0-4-KSP-AVC-Online). 

And some functionality and parts are added if the following mods are installed:

* [TAC Life Support **v0.10.+**](http://forum.kerbalspaceprogram.com/threads/40667-0-25-TAC-Life-Support-v0-10-1-10Oct-No-Win64-Support) adds life support resources and systems to inhabitable hangars,
* [RemoteTech](http://forum.kerbalspaceprogram.com/threads/83305) adds RT antennas and SPUs to controllable hangars,
* [Deadly Reentry](http://forum.kerbalspaceprogram.com/threads/54954) adds Ablative Shielding resource to the Heatshields,
* [Extraplanetary Launchpads](http://forum.kerbalspaceprogram.com/threads/59545) adds a new Heavy Recycler model that fits the style of hangars.
* [MKS/OKS](http://forum.kerbalspaceprogram.com/threads/79588) adds the Substrate Mixer -- a converter that allows to turn useless Silicates and Waste into a useful Substrate resource.

###Unsupported Mods
* [Asteroid Recycling Technologies](http://forum.kerbalspaceprogram.com/threads/91790) is not compatible with the Asteroid Hangars framework. Both mods **can be installed** at the same time, but **you cannot use the same asteroid** both as a hangar and by ART machinery.

***

##Acknowledgements

**First of, I want to thank my beloved wife for her support, understanding and help. This work takes much time...**

I also want to thank:

* [Taniwha](https://github.com/taniwha-qf) for inspiration and advice.
* [DragonEG](http://forum.kerbalspaceprogram.com/members/20077-DragonEG) for helping me to fix the friction problem.

And here are the mods which sources provided me with an understanding of how KSP API works. And with working solutions in some cases. In no particular order:

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
* [RemoteTech](http://forum.kerbalspaceprogram.com/threads/83305)
* [ProceduralParts](http://forum.kerbalspaceprogram.com/threads/70676-WIP-Procedural-Parts-The-next-phase-of-Stretchy-SRBs)
* [RCS Sounds](http://forum.kerbalspaceprogram.com/threads/52896)

***

<a rel="license" href="http://creativecommons.org/licenses/by/4.0/"><img alt="Creative Commons License" style="border-width:0" src="http://i.creativecommons.org/l/by/4.0/88x31.png" /></a><br />This work is licensed under a <a rel="license" href="http://creativecommons.org/licenses/by/4.0/">Creative Commons Attribution 4.0 International License</a>.