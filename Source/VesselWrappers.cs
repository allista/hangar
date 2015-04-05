using System;
using System.Collections.Generic;
using UnityEngine;

namespace AtHangar
{
	public class PackedConstruct : PackedVessel
	{
		public string flag { get; private set; }
		public ShipConstruct construct { get; private set; }
		ConfigNode vessel_node;

		public bool LoadConstruct()
		{
			UnloadConstruct();
			construct = new ShipConstruct();
			if(!construct.LoadShip(vessel_node))
			{
				UnloadConstruct();
				return false;
			}
			return true;
		}

		public void UpdateMetric(bool compute_hull = false)
		{ 
			if(construct == null) return;
			if(construct.parts.Count == 0) return;
			//sort parts from root to leavs
			var parts = construct.parts[0].AllConnectedParts();
			metric = new Metric(parts, compute_hull); 
		}

		public void UnloadConstruct() 
		{ 
			if(construct == null) return;
			foreach(Part p in construct) 
			{
				if(p != null && p.gameObject != null)
					UnityEngine.Object.Destroy(p.gameObject);
			}
			construct = null; 
		}

		public PackedConstruct() {}

		public PackedConstruct(string file, string flag)
		{
			this.flag = flag;
			vessel_node = ConfigNode.Load(file);
			vessel_node.name = "VESSEL";
			if(!LoadConstruct()) return;
			name = construct.shipName;
			id = Guid.NewGuid();
		}

		protected PackedConstruct(PackedConstruct pc)
		{
			flag = pc.flag;
			vessel_node = pc.vessel_node;
			metric = pc.metric;
			name = pc.name;
			if(pc.construct != null)
				LoadConstruct();
			id = Guid.NewGuid();
		}

		public virtual PackedConstruct Clone()
		{ return new PackedConstruct(this); }

		public override void Save(ConfigNode node)
		{
			ConfigNode metric_node = node.AddNode("METRIC");
			node.AddNode(vessel_node);
			metric.Save(metric_node);
			node.AddValue("name", name);
			node.AddValue("flag", flag);
			node.AddValue("id",   id);
		}

		public override void Load(ConfigNode node)
		{
			ConfigNode metric_node = node.GetNode("METRIC");
			vessel_node = node.GetNode("VESSEL");
			metric = new Metric(metric_node);
			name   = node.GetValue("name");
			flag   = node.GetValue("flag");
			id     = new Guid(node.GetValue("id"));
		}
	}

	public class StoredVessel : PackedVessel
	{
		public ProtoVessel proto_vessel { get; private set; }
		public Vessel vessel { get { return proto_vessel.vesselRef; } }
		public Vector3 CoM { get { return proto_vessel.CoM; } }
		public Vector3d dV;
		public List<ProtoCrewMember> crew { get; private set; }
		public VesselResources<ProtoVessel, ProtoPartSnapshot, ProtoPartResourceSnapshot> resources { get; private set; }

		public StoredVessel() {}

		public StoredVessel(Vessel vsl, bool compute_hull=false)
		{
			proto_vessel = vsl.BackupVessel();
			metric = new Metric(vsl, compute_hull);
			id     = proto_vessel.vesselID;
			name   = proto_vessel.vesselName;
			crew   = vsl.GetVesselCrew();
			resources = new VesselResources<ProtoVessel, ProtoPartSnapshot, ProtoPartResourceSnapshot>(proto_vessel);
		}

		public override void Save(ConfigNode node)
		{
			//nodes
			ConfigNode vessel_node = node.AddNode("VESSEL");
			ConfigNode metric_node = node.AddNode("METRIC");
			ConfigNode crew_node   = node.AddNode("CREW");
			proto_vessel.Save(vessel_node);
			metric.Save(metric_node);
			crew.ForEach(c => c.Save(crew_node.AddNode(c.name)));
		}

		public override void Load(ConfigNode node)
		{
			ConfigNode vessel_node = node.GetNode("VESSEL");
			ConfigNode metric_node = node.GetNode("METRIC");
			ConfigNode crew_node   = node.GetNode("CREW");
			proto_vessel = new ProtoVessel(vessel_node, HighLogic.CurrentGame);
			metric = new Metric(metric_node);
			crew   = new List<ProtoCrewMember>();
			foreach(ConfigNode cn in crew_node.nodes) 
				crew.Add(new ProtoCrewMember(HighLogic.CurrentGame.Mode, cn));
			id   = proto_vessel.vesselID;
			name = proto_vessel.vesselName;
			resources = new VesselResources<ProtoVessel, ProtoPartSnapshot, ProtoPartResourceSnapshot>(proto_vessel);
		}
	}
}

