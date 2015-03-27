[CENTER][SIZE=5][B]Hangar[/B] - [B][I]store your vessels until they are needed[/I][/B][/SIZE][/CENTER]
[HR][/HR]
[SIZE=4][b]Introduction[/b][/SIZE]

Hangars are parts that can [b]store whole ships inside[/b]. Stored ships are not docked but [b]unloaded[/b], which reduces part count considerably, improving framerate and memory footprint while allowing to carry out missions of great complexity.

Aside from the hangars themselves the mod provides a set of utility parts that help in various usage scenarios. To further reduce part count, most parts (including hangars) have multiple integrated modules and resources that are balanced and often eliminate the need to add such things as batteries, lights, generators, probe cores and reaction wheels.

[imgur]Z916l[/imgur]

[SIZE=4][b]Features[/b][/SIZE]

[list]
[*]Many [b]different types of hangars[/b] for any application
[*]Most hangars [b]may be rescaled[/b] to the needed size and proportions [b]via tweakables[/b][*][b]Ships could be stored in hangars at construction time[/b]
[*][b]Crew and resources can be transferred[/b] between a ship with a hangar and stored ships
[*][b]Ground hangars have anchors[/b] for comfort use on low-gravity worlds
[*][b]An asteroid can also be stored in a hangar[/b]. If it fits, of course.
[*]And vice versa: [b]a hangar can be made inside and asteroid![/b]
[*]In addition, many utility parts are provided to help in various usage scenarios.
[*][b]For more information read:[/b][/list]
[SIZE=4][b][url=https://github.com/allista/hangar/wiki]Documentation[/url][/b][/SIZE]

[SIZE=4][b][url=https://github.com/allista/hangar/blob/master/ChangeLog.md]ChangeLog[/url][/b][/SIZE]

[SIZE=4][b][url=https://github.com/allista/hangar/milestones]Future plans[/url][/b][/SIZE]

[SIZE=4][b]Support this project:[/b][/SIZE]

[list]
[*][url=https://funding.wmtransfer.com/hangar-ksp-plugin/donate][b]WebMoney[/b][/url]
[*][url=https://flattr.com/submit/auto?user_id=allista&url=https%3A%2F%2Fgithub.com%2Fallista%2Fhangar][B]Flattr[/B][/url]
[*][url=https://gratipay.com/allista][img]https://img.shields.io/gratipay/allista.svg[/img][/url][/list]
[HR][/HR]
[SIZE=4][b]Downloads and Installation[/b][/SIZE]

[SIZE=3][b][COLOR=#FF0000]Delete the old version[/COLOR] of the mod before installing the new one.[/b][/SIZE]
[i]You may keep the config.xml to save positions of GUI windows.[/i]

[SIZE=3][b]Before any upgrade I recommend you to [COLOR=#FF0000]backup your saves.[/COLOR][/b][/SIZE]

Releases are available at:

[SIZE=3][list]
[*][url=https://kerbalstuff.com/mod/270/Hangar][b]Kerbal Stuff[/b][/url] (starting from v1.3.0)
[*][url=https://github.com/KSP-CKAN/CKAN][b]CKAN[/b][/url] is now officially supported
[*][url=https://github.com/allista/hangar/releases][b]GitHub[/b][/url] (including beta pre-releases, all releases and source code)
[*] Optional Packages:
[list][*] [url=https://github.com/allista/hangar/releases/tag/v2.3.1]Radial Hangar part[/url][/list][/list]
[B][COLOR=#006400]Before using a hangar, study the list of modules that are integrated into it (RMB on part's icon).[/COLOR][/B][/SIZE]
Many of the hangars have plenty of modules (like batteries, command modules, fuel tanks, etc.) to reduce part count. Don't worry, all is balanced by weight and cost, no cheating.

[SIZE=3][b]Known Issues[/b][/SIZE]

[list]
[*]GUI:[list]Dropdown lists show vertical scrollbars when there are too many items. But due to the implemented click-through prevention mechanism the scrollbars cannot be moved by mouse cursor; use mouse wheel instead. [i]And curse Unity3D for the poor GUI API.[/i][/list]
[*]Rovers:[list]Rovers stored [b]in editor[/b] have somewhat smaller dimensions due to inactive suspension of the wheels. So if you pack several rovers [b]tightly[/b] into a hangar, and than launch one of them, the launched rover sometimes cannot be stored again into that same hangar with the "No room ..." message. Again: it's no bug, calculations are performed correctly, the rover's just got bigger.[/list]
[*]Mod Conflicts: none at the moment.
[*]Other:[list]Removing Hangar [B]in career[/B] mode sometimes corrupts the savegame (the user cannot enter VAB/SPH and so on). To fix such savegame see [URL="https://github.com/allista/hangar/blob/master/SavegameFix-HOWTO.md"][B]the HOWTO[/B][/URL].[/list][/list]
[SIZE=3][b]Requirements[/b][/SIZE]

[list][*]Hangar uses [url=http://forum.kerbalspaceprogram.com/threads/81496]KSPAPIExtensions[/url] by [url=http://forum.kerbalspaceprogram.com/members/100707-swamp_ig]swamp_ig[/url]. This plugin is bundled with the Hangar.[*]The [url=http://forum.kerbalspaceprogram.com/threads/55219]ModuleManager[/url] [b]is required[/b].[/list]
[SIZE=3][b]Recommended mods[/b][/SIZE]

[list]
[*] [url=http://forum.kerbalspaceprogram.com/threads/59545]Extraplanetary Launchpads[/url]: big ground hangars are not suitable as parts for vessel construction and are too heavy to launch anyway. So the only meaningful way to use them is to build them on site.
[*] [url=https://kerbalstuff.com/mod/510/Throttle%20Controlled%20Avionics%20-%20Continued]Throttle Controlled Avionics[/url]: if you're planning to build VTOLs and hovercrafts with spaceplane hangars, this mod (which I currently maintain) will help greatly as it ads automatic thrust balancing and altitude control.
[*] [url=http://forum.kerbalspaceprogram.com/threads/38768]Editor Extensions[/url]: invaluable for vessel design. Even considering 0.90 editor improvements.[/list]
[SIZE=3][b]Supported mods[/b][/SIZE]

Hangar supports [url=http://forum.kerbalspaceprogram.com/threads/79745-0-24-2-KSP-AVC-Add-on-Version-Checker-Plugin-1-0-4-KSP-AVC-Online]KSP Addon Version Checker[/url]. 

And some functionality and parts are added if the following mods are installed:
[list]
[*][url=http://forum.kerbalspaceprogram.com/threads/40667-0-25-TAC-Life-Support-v0-10-1-10Oct-No-Win64-Support]TAC Life Support [b]v0.10.+[/b][/url] adds life support resources and systems to inhabitable hangars,
[*][url=http://forum.kerbalspaceprogram.com/threads/83305]RemoteTech[/url] adds RT antennas and SPUs to controllable hangars,
[*][url=http://forum.kerbalspaceprogram.com/threads/56440]AntennaRange[/url] adds limited data transmitters to all hangars with integrated probe cores (thanks to [Kerbas-ad-astra]([url]https://github.com/Kerbas-ad-astra[/url])),
[*][url=http://forum.kerbalspaceprogram.com/threads/54954]Deadly Reentry[/url] adds Ablative Shielding resource to the Heatshields,
[*][url=http://forum.kerbalspaceprogram.com/threads/59545]Extraplanetary Launchpads[/url] adds a new Heavy Recycler model that fits the style of hangars.
[*][url=http://forum.kerbalspaceprogram.com/threads/79588]MKS/OKS[/url] adds the Substrate Mixer -- a converter that allows to turn useless Silicates and Waste into a useful Substrate resource.[/list]
[SIZE=3][b]Unsupported Mods[/b][/SIZE]

[list][*][url=http://forum.kerbalspaceprogram.com/threads/91790]Asteroid Recycling Technologies[/url] is not compatible with the Asteroid Hangars framework. Both mods [b]can be installed[/b] at the same time, but [b]you cannot use the same asteroid[/b] both as a hangar and by ART machinery.[/list]
[HR][/HR]
[SIZE=4][b]Acknowledgements[/b][/SIZE]

[b]First of, I want to thank my beloved wife for her support, understanding and help. This work takes much time...[/b]

I also want to thank:
[list][*][url=https://github.com/taniwha-qf]Taniwha[/url] for inspiration and advice.
[*][url=http://forum.kerbalspaceprogram.com/members/20077-DragonEG]DragonEG[/url] for helping me to fix the friction problem.
[*][url=https://github.com/Kerbas-ad-astra]Kerbas-ad-astra[/url] for making AntennaRange adaptation.
[*] [url=http://forum.kerbalspaceprogram.com/members/78247-Thorbane]Thorbane[/url] for making Switchable Tank Type configs for support of USI (MKS/OKS, FFT, Karbonite, Karbonite+) and initial support of KSPI to Switchable Tanks.[/list]
A record that I used to create propeller sound effect was obtained at [url=http://www.soundjay.com]Sound Jay[/url].

And here are the mods which sources provided me with an understanding of how KSP API works. And with working solutions in some cases. In no particular order:
[list][*][url=http://forum.kerbalspaceprogram.com/threads/59545]Extraplanetary Launchpads[/url]
[*][url=http://forum.kerbalspaceprogram.com/threads/53134-Kerbal-Attachment-System-%28KAS%29-0-4-7-Pipes-as-fuel-lines-and-even-fewer-explosions!]Kerbal Attach System[/url]
[*][url=http://forum.kerbalspaceprogram.com/threads/39512]Procedural Fairings[/url]
[*][url=http://forum.kerbalspaceprogram.com/threads/12384]MechJeb2[/url]
[*][url=http://forum.kerbalspaceprogram.com/threads/23979]Kethane[/url]
[*][url=http://forum.kerbalspaceprogram.com/threads/50077-0-23-5-Fusebox-electric-charge-tracker-and-build-helper-1-0-released-12-07-14]Fusebox[/url]
[*][url=http://forum.kerbalspaceprogram.com/threads/60936]CrewManifest[/url]
[*][url=http://forum.kerbalspaceprogram.com/threads/80234]TweakScale[/url]
[*][url=http://forum.kerbalspaceprogram.com/threads/24786]Kerbal Alarm Clock[/url]
[*][url=http://forum.kerbalspaceprogram.com/threads/57988]RealChutes[/url]
[*][url=http://forum.kerbalspaceprogram.com/threads/83305]RemoteTech[/url]
[*][url=http://forum.kerbalspaceprogram.com/threads/70676-WIP-Procedural-Parts-The-next-phase-of-Stretchy-SRBs]ProceduralParts[/url]
[*][url=http://forum.kerbalspaceprogram.com/threads/52896]RCS Sounds[/url][/list]
[HR][/HR]
[url=http://creativecommons.org/licenses/by/4.0/][img]http://i.creativecommons.org/l/by/4.0/88x31.png[/img][/url]
This work is licensed under a [url=http://creativecommons.org/licenses/by/4.0/]Creative Commons Attribution 4.0 International License[/url].