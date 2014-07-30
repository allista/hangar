#Hangar#

##_store your vessels until they are needed_##

***
###WARNING: this is still BETA. There should be bugs. 
While fixing them and implementing new features I'll try as hard as I can to maintain backward compatibility, _but I can't guarantee it_. So if you plan to use it in your main game, **backup your saves**.
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
    * most may be rescaled to the needed size and proportions via tweakables (mass, volume and cost are changed accordingly)
* There are several types of hangars:
    * in-line hangars for spaceships 
    * ground hangars for colonies
    * each type has inhabitable variant with integrated crew cabins
    * there's also the Spaceport part that combines a huge hangar with a cockpit; as such, the Spaceport has only a single stack node at its bottom
* All hangars are equipped with internal docking port for easy targeting. If the hangar is inactive, this port may be used for normal docking
* Ground hangars have anchoring modules for comfort use on low-gravity worlds and integrated probe cores for autonomous operation
* Crew and resources can be transferred between a vessel with a hangar and stored vessels
* Smart internal machinery ensures optimal filling of a hangar and mass distribution, while preventing attempts to store objects that do not fit in
* A hangar can be filled with vessels at construction time (NOTE: a vessel with a filled hangar will stutter for a second or two upon launch; that's normal)
* An asteroid can also be stored in a hangar. If it fits, of course.
* Interface:
    * Hangars are controlled with a GUI
    * For the vessels that do not have any hangars the GUI shows their volume and dimensions
    * A vessel can have multiple hangars. Provided GUI allows easy switching between them by highlighting the hangar that is currently selected
    * The Toolbar plugin is required for now
* 

##Recommended mods##

There are many great mods out there that I love and use myself. But the one mode that I strongly recommend to use with the Hangar to improve game experience is the **Extraplanetary Launchpads** by Taniwha. For one thing: big ground hangars are not suitable as parts for vessel construction and are too heavy to launch anyway. So the only meaningful way to use them is to build them on site.

##Usage details##

###Hangars in general###

All hangars are parts and thus may be added to the vessel at construction. Hangars have gate(s) which may be open or closed; in addition, internal machinery of a hangar may be deactivated or activated again.
All controls and information about a hangar are located in the dedicated GUI window that may be summoned through the context menu of any hangar (menu entry "Show Controls") or through the Toolbar button.

####Storing a vessel###

To store a vessel a hangar should have its gates opened and machinery activated. A vessel should fit into the hangar and the hangar should have enough free space inside. The relative speed of the hangar and the vessel should be less than 1 m/s and the vessel should not be accelerated.

To store vessels **during ship construction** select "Edit contents" entry in hangar's context menu to summon vessel selection window. There you select the type of a vessels to choose from (VAB, SPH or Subassemblies) and push the "Select Vessel" button. All stored vessels appear in the same window in a list below. To remove stored vessel from the hangar push the "X" button corresponding the that vessel. To completely clear the hangar push "Clear" button. The vessel should also fit into the hangar which should also have enough free space. If a hangar with some vessels already stored is resized and there's not enough room for all the vessels anymore, the smallest vessels are removed from the hangar to free enough space.

Mass and cost of stored vessels are added to that of the hangar.


###Ground hangars###

Despite being parts, ground hangars are meant to be used as separate self-sufficient colony buildings. They have an additional context menu entry "Attach anchor". It allows to fix the hangar to the ground, provided it is landed and not moving faster than 1 m/s.

###Spaceport###

##Possible use cases##

###Launch a satellite network###

###Orbital station###

####Docks for maintenance ships###

####Fast crew transfer with orbit-to-orbit shuttles###

###Exploration ship###

###Rover storage for colony###

#Acknowledgements#



<a rel="license" href="http://creativecommons.org/licenses/by/4.0/"><img alt="Creative Commons License" style="border-width:0" src="http://i.creativecommons.org/l/by/4.0/88x31.png" /></a><br />This work is licensed under a <a rel="license" href="http://creativecommons.org/licenses/by/4.0/">Creative Commons Attribution 4.0 International License</a>.