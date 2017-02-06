//   VesselWrappers.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
using System.Collections.Generic;
using UnityEngine;
using AT_Utils;

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

		public void UpdateMetric()
		{ 
			if(construct == null) return;
			if(construct.parts.Count == 0) return;
			//sort parts from root to leavs
			var parts = construct.parts[0].AllConnectedParts();
			metric = new Metric(parts, true); 
		}

		public void UnloadConstruct() 
		{ 
			if(construct == null) return;
			foreach(Part p in construct) 
			{
				if(p != null) 
				{
					p.OnDelete();
					if(p.gameObject != null)
						UnityEngine.Object.Destroy(p.gameObject);
				}
			}
			construct.Clear();
			construct = null; 
		}

		public PackedConstruct() {}

		public PackedConstruct(ShipConstruct construct, string flag)
		{
			this.flag = flag;
			this.construct = construct;
			vessel_node = construct.SaveShip();
			vessel_node.name = "VESSEL";
			resources = new VesselResources(vessel_node);
			name = construct.shipName;
			id = Guid.NewGuid();
		}

		public PackedConstruct(string file, string flag)
		{
			this.flag = flag;
			vessel_node = ConfigNode.Load(file);
			vessel_node.name = "VESSEL";
			resources = new VesselResources(vessel_node);
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
			resources = new VesselResources(vessel_node);
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
			resources = new VesselResources(vessel_node);
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

		public StoredVessel() {}

		public StoredVessel(Vessel vsl)
		{
			proto_vessel = vsl.BackupVessel();
			metric = new Metric(vsl, true);
			id     = proto_vessel.vesselID;
			name   = proto_vessel.vesselName;
			crew   = proto_vessel.GetVesselCrew();
			resources = new VesselResources(proto_vessel);

		}

		public void RemoveProtoVesselCrew()
		{
			if(proto_vessel.GetVesselCrew().Count == 0) return;
			foreach(var p in proto_vessel.protoPartSnapshots)
			{
				while(p.protoModuleCrew.Count > 0)
				{
					var c = p.GetCrew(0);
					proto_vessel.RemoveCrew(c);
					p.RemoveCrew(0);
				}
			}
		}

		public void ExtractProtoVesselCrew(Vessel dest_vessel, Part start_from_part = null)
		{
			if(vessel == null) return;
			if(start_from_part != null) 
				CrewTransferBatch.moveCrew(vessel, start_from_part, false);
			CrewTransferBatch.moveCrew(vessel, dest_vessel, false);
			proto_vessel = vessel.BackupVessel();
            resources = new VesselResources(proto_vessel);
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
			resources = new VesselResources(proto_vessel);
		}
	}
}

