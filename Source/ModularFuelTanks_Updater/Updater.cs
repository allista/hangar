using AtHangar;
using RealFuels;
using System.Collections.Generic;
using System.Linq;

namespace ModularFuelTanks_Updater
{
	public class ModularFuelTanks_Updater : ModuleUpdater<ModuleFuelTanks>
	{
		public override void OnRescale(Scale scale)
		{
			module.ChangeVolume(base_module.volume * scale.absolute.cube);
			if(!part.HasModule<ResourcesUpdater>()) return;
			var mft_names = module.fuelList.Select(t => t.name);
			var mft_resources = part.Resources.list.Where(r => mft_names.Contains(r.name));
			foreach(PartResource resource in mft_resources)
			{

				resource.amount /= scale.relative.cube;
				resource.maxAmount /= scale.relative.cube;
			}
//			foreach(PartResource r in part.Resources)
//			{
//				r.amount /= scale.relative.cube;
//				r.maxAmount /= scale.relative.cube;
//			}
		}
	}
}

