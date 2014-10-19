using System.Collections.Generic;
using UnityEngine;

namespace AtHangar
{
	public class PassageNode : ConfigNodeObject
	{
		new public const string NODE_NAME = "PASSAGE_NODE";
		[Persistent] public string NodeID = "_none_";
		[Persistent] public Vector3 Size; //ConfigNode'bug: can't LoadObjectFromConfig if I use Vector2

		readonly Part part;
		public AttachNode PartNode { get; private set; }

		public PassageNode(Part part) { this.part = part; }

		HangarPassage get_other_passage()
		{
			if(PartNode == null || PartNode.attachedPart == null) return null;
			return PartNode.attachedPart.GetModule<HangarPassage>();
		}

		public HangarPassage OtherPassage 
		{ 
			get 
			{
				var other_passage = get_other_passage();
				if(other_passage == null) return null;
				var other_node = other_passage.GetNodeByPart(part);
				return other_node != null? other_passage : null;
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
			PartNode = part.findAttachNode(NodeID);
		}

		public HangarPassage CanPassThrough(PackedVessel vsl)
		{
			var other_passage = get_other_passage();
			if(other_passage == null) return null;
			var other_node = other_passage.GetNodeByPart(part);
			if(other_node == null) return null;
			var size = new Vector2(Mathf.Min(Size.x, other_node.Size.x), 
								   Mathf.Min(Size.y, other_node.Size.y));
			if(!vsl.metric.FitsSomehow(size)) return null;
			return other_passage;
		}
	}


	public class HangarPassage : PartModule
	{
		public readonly Dictionary<string, PassageNode> Nodes = new Dictionary<string, PassageNode>();
		public ConfigNode ModuleConfig;
		public bool Ready { get; protected set; }

		#region Setup
		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			//only save config for the first time
			if(ModuleConfig == null) ModuleConfig = node;
		}

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			early_setup(state);
			Setup();
			start_coroutines();
		}

		protected virtual void early_setup(StartState state) 
		{
			if(part.HasModule<ModuleDockingNode>())
				this.Log("WARNING: this part also has ModuleDockingNode. " +
					"HangarPassage will not work properly upon docking.");
			this.Log("HangarPassage.OnStart");//debug
			//initialize passage nodes
			foreach(ConfigNode n in ModuleConfig.GetNodes(PassageNode.NODE_NAME))
			{
				var pn = new PassageNode(part);
				pn.Load(n);
				Nodes.Add(pn.NodeID, pn);
			}
			this.Log("ModuleConfig:\n{0}", ModuleConfig);//debug
			this.Log("Nodes: {0}", Nodes.Count);//debug
		}

		virtual public void Setup(bool reset = false) {}

		protected virtual void start_coroutines() { Ready = true; }
		#endregion

		#region Logistics
		public PassageNode GetNodeByPart(Part p)
		{
			PassageNode node = null;
			var an = part.findAttachNodeByPart(p);
			if(an != null) Nodes.TryGetValue(an.id, out node);
			return node;
		}

		public List<HangarPassage> GetConnectedPassages(PassageNode requesting_node = null)
		{
			var this_node = requesting_node != null? requesting_node.OtherNode : null;
			var C = new List<HangarPassage>{this};
			foreach(var pn in Nodes.Values)
			{
				if(pn == this_node) continue;
				var other_passage = pn.OtherPassage;
				if(other_passage != null)
					C.AddRange(other_passage.GetConnectedPassages(pn));
			}
			return C;
		}

		virtual public bool CanHold(PackedVessel vsl) { return true; }

		public bool CanTransferTo(PackedVessel vsl, HangarPassage other, PassageNode requesting_node = null)
		{
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
		#endregion
	}
}

