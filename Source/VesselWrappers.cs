using System;
using System.Collections.Generic;
using UnityEngine;

namespace AtHangar
{
	public enum VesselType { VAB, SPH, SubAssembly }

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
			metric = new Metric(construct.Parts, compute_hull); 
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
		public ProtoVessel vessel { get; private set; }
		public Vessel launched_vessel { get { return vessel.vesselRef; } }
		public Vector3 CoM { get; private set; }
		public List<ProtoCrewMember> crew { get; private set; }
		public int CrewCapacity { get{ return metric.CrewCapacity; } }
		public VesselResources<ProtoVessel, ProtoPartSnapshot, ProtoPartResourceSnapshot> resources { get; private set; }

		public StoredVessel() {}

		public StoredVessel(Vessel vsl, bool compute_hull=false)
		{
			vessel = vsl.BackupVessel();
			metric = new Metric(vsl, compute_hull);
			id     = vessel.vesselID;
			name   = vessel.vesselName;
			CoM    = vsl.findLocalCenterOfMass();
			crew   = vsl.GetVesselCrew();
			resources = new VesselResources<ProtoVessel, ProtoPartSnapshot, ProtoPartResourceSnapshot>(vessel);
		}

		public override void Save(ConfigNode node)
		{
			//nodes
			ConfigNode vessel_node = node.AddNode("VESSEL");
			ConfigNode metric_node = node.AddNode("METRIC");
			ConfigNode crew_node   = node.AddNode("CREW");
			vessel.Save(vessel_node);
			metric.Save(metric_node);
			foreach(ProtoCrewMember c in crew)
			{
				ConfigNode n = crew_node.AddNode(c.name);
				c.Save(n);
			}
			//values
			node.AddValue("CoM", ConfigNode.WriteVector(CoM));
		}

		public override void Load(ConfigNode node)
		{
			ConfigNode vessel_node = node.GetNode("VESSEL");
			ConfigNode metric_node = node.GetNode("METRIC");
			ConfigNode crew_node   = node.GetNode("CREW");
			vessel = new ProtoVessel(vessel_node, FlightDriver.FlightStateCache);
			metric = new Metric(metric_node);
			crew   = new List<ProtoCrewMember>();
			foreach(ConfigNode cn in crew_node.nodes) crew.Add(new ProtoCrewMember(cn));
			id   = vessel.vesselID;
			name = vessel.vesselName;
			CoM  = ConfigNode.ParseVector3(node.GetValue("CoM"));
			resources = new VesselResources<ProtoVessel, ProtoPartSnapshot, ProtoPartResourceSnapshot>(vessel);
		}

		public void Load()
		{
			vessel.Load(FlightDriver.FlightStateCache.flightState);
			vessel.LoadObjects();
		}
	}

	#region Waiters
	public class VesselWaiter
	{
		public Vessel vessel;
		public VesselWaiter(Vessel vsl) { vessel = vsl; }

		protected static bool parts_inited(List<Part> parts)
		{
			bool inited = true;
			foreach(Part p in parts)
			{
				if(!p.started)
				{
					inited = false;
					break;
				}
			}
			return inited;
		}

		public bool loaded
		{
			get 
			{
				if(vessel.id != FlightGlobals.ActiveVessel.id) return false;
				vessel = FlightGlobals.ActiveVessel;
				if(parts_inited(vessel.parts)) return true;
				OrbitPhysicsManager.HoldVesselUnpack(2);
				return false;
			}
		}
	}

	public class LaunchedVessel : VesselWaiter
	{
		readonly List<ProtoCrewMember> crew;
		readonly StoredVessel sv;

		public LaunchedVessel(StoredVessel sv, Vessel vsl, List<ProtoCrewMember> crew)
			: base(vsl)
		{
			this.sv = sv;
			this.crew = crew;
		}

		public void transferCrew() 
		{ CrewTransfer.addCrew(vessel, crew); }

		public void tunePosition()
		{
			Vector3 dP = vessel.findLocalCenterOfMass() - sv.CoM;
			vessel.SetPosition(vessel.vesselTransform.TransformPoint(dP));
		}
	}
	#endregion
}

