//   HangarConfig.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2015 Allis Tauri
//
// This work is licensed under the Creative Commons Attribution 4.0 International License. 
// To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/ 
// or send a letter to Creative Commons, PO Box 1866, Mountain View, CA 94042, USA.

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using AT_Utils;

namespace AtHangar
{
	public class Globals : PluginConfig
	{
		static Globals instance;
		public static Globals Instance
		{
			get
			{
				if(instance == null)
				{
					instance = new Globals();
					if(!instance.DefaultFileExists)
						instance.CreateDefaultFile();
					instance.LoadDefaultFile();
					Utils.ModName = "Hangar";
				}
				return instance;
			}
		}

		//store vessel
		[Persistent] public float  MaxSqrRelVelocity     = 1f;    //m/s
		[Persistent] public float  MaxSqrRelAcceleration = 0.01f; //m/s2
		[Persistent] public bool   EnableVesselPacking   = true;
		//restore vessel
		[Persistent] public float  MaxSqrAngularVelocity = 0.01f; //5.73 deg/s
		[Persistent] public float  MaxSqrSurfaceVelocity = 0.01f; //m/s
		[Persistent] public float  MaxGeeForce           = 0.2f;  //g
		[Persistent] public float  MaxStaticPressure     = 0.01f; //atm
		//resize and tech tree
		[Persistent] public TechFloat MinAspect = new TechFloat((a, b) => a < b);
		[Persistent] public TechFloat MaxAspect = new TechFloat((a, b) => a > b);
		[Persistent] public TechFloat MinSize   = new TechFloat((a, b) => a < b);
		[Persistent] public TechFloat MaxSize   = new TechFloat((a, b) => a > b);
		//for Metric
		[Persistent] public string MeshesToSkip = string.Empty;
		public List<string> MeshesToSkipList { get; private set; }
		//misc
		[Persistent] public string KethaneMapCollider  = "MapOverlay collider";
		[Persistent] public bool   UseStockAppLauncher = false;

		public override void Init()
		{ 
			//init meshes names
			MeshesToSkipList = new List<string>();
			if(string.IsNullOrEmpty(MeshesToSkip))
			   MeshesToSkipList = MeshesToSkip
					.Split(new []{','}, StringSplitOptions.RemoveEmptyEntries)
					.Select(s => s.Trim()).ToList();
			//initialize part resizer
			AnisotropicPartResizerConfig.MinSize = MinSize;
			AnisotropicPartResizerConfig.MaxSize = MaxSize;
			AnisotropicPartResizerConfig.MinAspect = MinAspect;
			AnisotropicPartResizerConfig.MaxAspect = MaxAspect;
		}

		public override void Load(ConfigNode node)
		{
			base.Load(node);
			//load TechFloats
			MinAspect.Load(node.GetNode(Utils.PropertyName(new {MinAspect})));
			MaxAspect.Load(node.GetNode(Utils.PropertyName(new {MaxAspect})));
			MinSize.Load(node.GetNode(Utils.PropertyName(new {MinSize})));
			MaxSize.Load(node.GetNode(Utils.PropertyName(new {MaxSize})));
		}
	}
}

