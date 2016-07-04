//   PartUpdaters.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2015 Allis Tauri
//
// This work is licensed under the Creative Commons Attribution 4.0 International License. 
// To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/ 
// or send a letter to Creative Commons, PO Box 1866, Mountain View, CA 94042, USA.

// This code is based on Procedural Fairings plug-in by Alexey Volynskov, KzPartResizer class
// And on ideas drawn from the TweakScale plugin

using System;
using System.Collections.Generic;
using UnityEngine;
using AT_Utils;

namespace AtHangar
{
	public class PassageUpdater : ModuleUpdater<HangarPassage>
	{ 
		protected override void on_rescale(ModulePair<HangarPassage> mp, Scale scale)
		{
			mp.module.Setup(!scale.FirstTime);
			foreach(var key in new List<string>(mp.module.Nodes.Keys))
				mp.module.Nodes[key].Size = Vector3.Scale(mp.base_module.Nodes[key].Size, 
					new Vector3(scale, scale, 1));
		}
	}

	public class HangarMachineryUpdater : ModuleUpdater<HangarMachinery>
	{ 
		protected override void on_rescale(ModulePair<HangarMachinery> mp, Scale scale)
		{
			mp.module.Setup(!scale.FirstTime);
			mp.module.EnergyConsumption = mp.base_module.EnergyConsumption * scale.absolute.quad * scale.absolute.aspect; 
		}
	}

	public class GenericInflatableUpdater : ModuleUpdater<HangarGenericInflatable>
	{
		protected override void on_rescale(ModulePair<HangarGenericInflatable> mp, Scale scale)
		{
			mp.module.InflatableVolume = mp.base_module.InflatableVolume * scale.absolute.cube * scale.absolute.aspect;
			mp.module.CompressedGas   *= scale.relative.cube * scale.relative.aspect;
			mp.module.ForwardSpeed     = mp.base_module.ForwardSpeed / (scale.absolute * scale.aspect);
			mp.module.ReverseSpeed     = mp.base_module.ReverseSpeed / (scale.absolute * scale.aspect);
			if(mp.module.Compressor == null) return;
			mp.module.Compressor.ConsumptionRate = mp.base_module.Compressor.ConsumptionRate * scale.absolute.cube * scale.absolute.aspect;
		}
	}

	public class ResourceConverterUpdater : ModuleUpdater<AnimatedConverterBase>
	{
		protected override void on_rescale(ModulePair<AnimatedConverterBase> mp, Scale scale)
		{ mp.module.SetRatesMultiplier(mp.base_module.RatesMultiplier * scale.absolute.cube * scale.absolute.aspect); }
	}

	public class HangarFairingsUpdater : ModuleUpdater<HangarFairings>
	{
		protected override void on_rescale(ModulePair<HangarFairings> mp, Scale scale)
		{ 
			mp.module.JettisonForce = mp.base_module.JettisonForce * scale.absolute.cube * scale.absolute.aspect;
			mp.module.FairingsCost = mp.base_module.FairingsCost * scale.absolute.cube * scale.absolute.aspect;
		}
	}

}
