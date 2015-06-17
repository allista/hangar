using System.Collections.Generic;
using UnityEngine;

namespace AtHangar
{
	public class HangarPassage : ControllableModuleBase, ISerializationCallbackReceiver
	{
		[KSPField] public bool HideInfo;

		public readonly Dictionary<string, PassageNode> Nodes = new Dictionary<string, PassageNode>();
		public ConfigNode ModuleConfig;
		public bool Ready { get; protected set; }

		#region Setup
		public override string GetInfo()
		{
			if(HideInfo) return "";
			init_nodes();
			if(Nodes.Count == 0) return "";
			var info = "Vessels can pass through:\n";
			var nodes = new List<string>(Nodes.Keys);
			nodes.Sort(); 
			nodes.ForEach(n => info += string.Format("- {0}: {1:F2}m x {2:F2}m\n", 
				n, Nodes[n].Size.x, Nodes[n].Size.y));
			return info;
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			//only save config for the first time
			if(ModuleConfig == null) ModuleConfig = node;
		}

		//workaround for ConfigNode non-serialization
		public byte[] _module_config;
		public virtual void OnBeforeSerialize()
		{ _module_config = ConfigNodeWrapper.SaveConfigNode(ModuleConfig); }
		public virtual void OnAfterDeserialize() 
		{ ModuleConfig = ConfigNodeWrapper.RestoreConfigNode(_module_config); }

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			early_setup(state);
			Setup();
			start_coroutines();
		}

		protected virtual void early_setup(StartState state) { init_nodes(); }

		void init_nodes()
		{
			Nodes.Clear();
			if(ModuleConfig == null) 
			{ this.Log("ModuleConfig is null. THIS SHOULD NEVER HAPPEN!"); return; }
			foreach(ConfigNode n in ModuleConfig.GetNodes(PassageNode.NODE_NAME))
			{
				var pn = new PassageNode(part);
				pn.Load(n);
				Nodes.Add(pn.NodeID, pn);
			}
		}

		virtual public void Setup(bool reset = false) {}

		protected virtual void start_coroutines() { Ready = true; }
		#endregion

		#region Logistics
		public PassageNode GetNodeByPart(Part p)
		{
			foreach(var n in Nodes.Values)
			{ if(n.OtherPart == p) return n; }
			return null;
		}

		public List<HangarPassage> ConnectedPassages(PassageNode requesting_node = null)
		{
			var this_node = requesting_node != null? requesting_node.OtherNode : null;
			var C = new List<HangarPassage>{this};
			foreach(var pn in Nodes.Values)
			{
				if(pn == this_node) continue;
				var other_passage = pn.OtherPassage;
				if(other_passage != null)
					C.AddRange(other_passage.ConnectedPassages(pn));
			}
			return C;
		}

		virtual public bool CanHold(PackedVessel vsl) { return true; }

		public bool CanTransferTo(PackedVessel vsl, HangarPassage other, PassageNode requesting_node = null)
		{
			if(!enabled) return false;
			if(this == other) return CanHold(vsl);
			var this_node = requesting_node != null? requesting_node.OtherNode : null;
			bool can_transfer = false;
			foreach(var pn in Nodes.Values)
			{
				if(pn == this_node) continue;
				var other_passage = pn.CanPassThrough(vsl);
				if(other_passage != null) 
					can_transfer = other_passage.CanTransferTo(vsl, other, pn);
				if(can_transfer) break;
			}
			return can_transfer;
		}

		public Part ConnectedPartWithModule<ModuleT>(PassageNode requesting_node = null)
			where ModuleT : PartModule
		{
			if(part.HasModule<ModuleT>()) return part;
			var this_node = requesting_node != null? requesting_node.OtherNode : null;
			foreach(var pn in Nodes.Values)
			{
				if(pn == this_node) continue;
				var other_passage = pn.OtherPassage;
				if(other_passage == null) continue;
				var p = other_passage.ConnectedPartWithModule<ModuleT>(pn);
				if(p != null) return p;
			}
			return null;
		}
		#endregion
	}


	public class PassageNode : ConfigNodeObject
	{
		new public const string NODE_NAME = "PASSAGE_NODE";
		[Persistent] public string NodeID = "_none_";
		[Persistent] public Vector3 Size; //ConfigNode's bug: can't LoadObjectFromConfig if I use Vector2

		readonly Part part;
		AttachNode part_node;
		ModuleDockingNode docking_node;

		public PassageNode(Part part) { this.part = part; }

		public Part OtherPart
		{
			get
			{
				if(part_node != null && part_node.attachedPart != null) 
					return part_node.attachedPart;
				if(docking_node != null && part.vessel != null) 
					return part.vessel[docking_node.dockedPartUId];
				return null;
			}
		}

		HangarPassage get_other_passage()
		{
			var other_part = OtherPart;
			return other_part != null ? other_part.GetModule<HangarPassage>() : null;
		}

		public HangarPassage OtherPassage 
		{ 
			get 
			{
				var other_passage = get_other_passage();
				if(other_passage != null && 
					other_passage.GetNodeByPart(part) != null)
					return other_passage;
				return null;
			}
		}

		public PassageNode OtherNode
		{
			get 
			{
				var other_passage = get_other_passage();
				return other_passage != null ? other_passage.GetNodeByPart(part) : null;
			}
		}

		public Vector2 MinSize 
		{ 
			get 
			{ 
				var other_node = OtherNode;
				if(other_node == null) return Size;
				return new Vector2(Mathf.Min(Size.x, other_node.Size.x), 
					Mathf.Min(Size.y, other_node.Size.y));
			}
		}

		public override void Load(ConfigNode node)
		{
			base.Load(node);
			part_node = part.findAttachNode(NodeID);
			docking_node = part.GetModule<ModuleDockingNode>();
			if(docking_node != null && docking_node.referenceAttachNode != NodeID) docking_node = null;
		}

		public HangarPassage CanPassThrough(PackedVessel vsl)
		{
			var other_passage = get_other_passage();
			if(other_passage == null) return null;
			var other_node = other_passage.GetNodeByPart(part);
			if(other_node == null) return null;
			var size = new Vector2(Mathf.Min(Size.x, other_node.Size.x), 
				Mathf.Min(Size.y, other_node.Size.y));
			return vsl.metric.FitsSomehow(size)? other_passage : null;
		}
	}
}

