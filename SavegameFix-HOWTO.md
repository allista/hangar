To fix a **career** savegame corrupted by removing Hangar do the following:

* Open the **`[KSP]/saves/[your-savegame]/persistent.sfs`** file in any text editor.
* Find the ContractSystem SCENARIO section and the list of CONTRACTS:

***

    SCENARIO
    {
        name = ContractSystem
        scene = 7, 8, 5, 6
        update = 19091.7407734679
        CONTRACTS
		{
            ...

***

* In the list of CONTRACTS find **all** CONTRACTs (even FINISHED) with

    **`agent = AT Industries`** parameter
    
    and delete them. For example:

***

    //the whole CONTRACT or CONTRACT_FINISHED section should be deleted
    CONTRACT_FINISHED
	{
		guid = 4d0ca84e-9921-4c65-9f98-c45fce4fb6cf
		type = CollectScience
		prestige = 0
		seed = -1913671910
		state = Completed
		agent = AT Industries
		deadlineType = Floating
		expiryType = Floating
		values = 64800,46008000,5000,31499.9985694885,5249.99976158142,1,139.3728,73.17073,83508.6001704375,18708.6001704375,46026708.6001704,0
		body = 1
		location = Space
		PARAM
		{
			name = CollectScience
			enabled = False
			state = Complete
			values = 0,0,0,0,0
			body = 1
			location = Space
		}
	}

***

**NOTE**, that this should work not only for Hangar, but for any mod that provides its own Agent for Contract System. The list of Agents could usually be found in mod's folder in `Agents.cfg` file.