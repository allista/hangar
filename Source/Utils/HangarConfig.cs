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

namespace AtHangar
{
	/// <summary>
	/// Loads hangar configuration presets at game loading
	/// </summary>
	[KSPAddon(KSPAddon.Startup.Instantly, true)]
	public class HangarConfig : MonoBehaviour
	{
		public static HangarGlobals Globals = new HangarGlobals();

		public void Start()
		{
			//load_config//
			var roots = GameDatabase.Instance.GetConfigNodes(HangarGlobals.NODE_NAME);
			if(roots.Length == 0) return;
			if(roots.Length > 1)
				Utils.Log("HangarConfig: found {0} versions of {1} node. Using the first one.", 
				          roots.Length, HangarGlobals.NODE_NAME);
			Globals.Load(roots[0]);
			Globals.Init();
		}
	}


	public class HangarGlobals : ConfigNodeObject
	{
		new public const string NODE_NAME = "HANGARGLOBALS";

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
		[Persistent] public string KethaneMapCollider = "MapOverlay collider";

		public void Init()
		{ 
			//init meshes names
			MeshesToSkipList = new List<string>();
			if(string.IsNullOrEmpty(MeshesToSkip))
			   MeshesToSkipList = MeshesToSkip
					.Split(new []{','}, StringSplitOptions.RemoveEmptyEntries)
					.Select(s => s.Trim()).ToList();
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


	public abstract class TechValue<T> : IConfigNode
	{
		public readonly Dictionary<string, T> Values = new Dictionary<string, T>();
		public Func<T, T, bool> Compare;

		#region ConfigNode
		protected abstract T parse(string val);

		public void Load(ConfigNode node)
		{
			if(node == null) return;
			foreach(ConfigNode.Value tech in node.values)
				Values[tech.name] = parse(tech.value);
		}

		public void Save(ConfigNode node) {}
		#endregion

		#region TechTree
		//ResearchAndDevelopment.PartModelPurchased is broken and always returns 'true'
		public static bool PartIsPurchased(string name)
		{
			var info = PartLoader.getPartInfoByName(name);
			if(info == null) return false;
			if(HighLogic.CurrentGame.Mode != Game.Modes.CAREER) return true;
			var tech = ResearchAndDevelopment.Instance.GetTechState(info.TechRequired);
			return tech != null && tech.state == RDTech.State.Available && tech.partsPurchased.Contains(info);
		}

		//current_value is needed to preserve scale of an existing vessel when configuration is changed
		public bool TryGetValue(out T value, bool ignore_tech_tree = false, Func<T, T, bool> compare = null)
		{
			if(compare == null) compare = Compare;
			value = default(T); var first = true;
			foreach(var pair in Values)
			{
				if((ignore_tech_tree || PartIsPurchased(pair.Key)) &&
				   (first || compare(pair.Value, value)))
				{ value = pair.Value; first = false; }
			}
			return !first;
		}
		#endregion
	}


	public class TechFloat : TechValue<float>
	{
		protected override float parse(string val)
		{
			try { return float.Parse(val); }
			catch { return 0f; }
		}

		public TechFloat(Func<float, float, bool> compare) 
		{ Compare = compare; }
	}
}

