using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace AtHangar
{
	public enum VesselType { VAB, SPH, SubAssembly }

	public class PackedConstruct : PackedVessel
	{
		public string name { get; private set; }
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

		public void DisablePhysics()
		{
			if(construct == null) return;
			foreach(Part p in construct.Parts)
				if(p.Rigidbody != null) p.Rigidbody.Sleep();
		}
		public void EnablePhysics()
		{
			if(construct == null) return;
			foreach(Part p in construct.Parts)
				if(p.Rigidbody != null) p.Rigidbody.WakeUp();
		}

		public PackedConstruct() {}

		public PackedConstruct(string file, string flag)
		{
			this.flag = flag;
			vessel_node = ConfigNode.Load(file);
			vessel_node.name = "VESSEL";
			if(!LoadConstruct()) return;
			metric = new Metric(construct.Parts);
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
		public float volume { get { return metric.volume; } }
		public float mass { get { return metric.mass; } set { metric.mass = value; } }
		public float cost { get { return metric.cost; } set { metric.cost = value; } }
		public Vector3 CoM { get; private set; }
		public Vector3 CoG { get { return metric.center; } } //center of geometry
		public List<ProtoCrewMember> crew { get; private set; }
		public int CrewCapacity { get{ return metric.CrewCapacity; } }
		public VesselResources<ProtoVessel, ProtoPartSnapshot, ProtoPartResourceSnapshot> resources { get; private set; }
		public bool damaged { get; private set; }

		public StoredVessel() {}

		public StoredVessel(Vessel vsl)
		{
			vessel  = vsl.BackupVessel();
			metric  = new Metric(vsl);
			id      = vessel.vesselID;
			CoM     = vsl.findLocalCenterOfMass();
			crew    = vsl.GetVesselCrew();
			damaged = vsl.parts.Aggregate(false, (d, p) => d || p.HasDamagedWheels());
			resources = new VesselResources<ProtoVessel, ProtoPartSnapshot, ProtoPartResourceSnapshot>(vessel);
//			fixTripLogger(); //FIXME
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
			node.AddValue("damaged", damaged);
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
			CoM  = ConfigNode.ParseVector3(node.GetValue("CoM"));
			bool _damaged = false;
			if(node.HasValue("damaged")) bool.TryParse(node.GetValue("damaged"), out _damaged);
			damaged = _damaged;
			resources = new VesselResources<ProtoVessel, ProtoPartSnapshot, ProtoPartResourceSnapshot>(vessel);
		}

		public void Repair()
		{
			if(!damaged) return;
		}

		void fixTripLogger() //FIXME
		{
			//workaround for the bug that spams savegame with multiple instances of ModuleTripLogger and 'at = ...' ConfigNode.Value-s
			Utils.logStamp("Cleaning TripLog for "+vessel.vesselName);
			string trip_logger_name = typeof(ModuleTripLogger).Name;
			foreach(ProtoPartSnapshot pp in vessel.protoPartSnapshots)
			{
				List<ProtoPartModuleSnapshot> trip_loggers = new List<ProtoPartModuleSnapshot>(pp.modules.Where(ppm => ppm.moduleName == trip_logger_name));
				if(trip_loggers.Count == 0) continue;
				ConfigNode _new = new ConfigNode();
				Dictionary<string, HashSet<string>> bodies = new Dictionary<string, HashSet<string>>();
				foreach(ProtoPartModuleSnapshot logger in trip_loggers)
				{
					ConfigNode _old = logger.moduleValues;
					foreach(ConfigNode.Value v in _old.values) 
					{ if(!_new.HasValue(v.name)) _new.AddValue(v.name, v.value); }
					foreach(ConfigNode o in _old.nodes)
					{
						if(!_new.HasNode(o.name)) _new.AddNode(o.name);
						if(!bodies.ContainsKey(o.name)) bodies.Add(o.name, new HashSet<string>());
						ConfigNode n = _new.GetNode(o.name);
						foreach(ConfigNode.Value v in o.values)
						{
							if(v.name != "at" && !n.HasValue(v.name)) 
							{ n.AddValue(v.name, v.value); continue; }
							if(bodies[o.name].Contains(v.value)) continue;
							bodies[o.name].Add(v.value);
							n.AddValue(v.name, v.value);
						}
						int items_removed = o.values.Count-n.values.Count;
						if(items_removed > 0)
							Debug.Log(string.Format("Removed {0} of {1} values from {2} node", 
							                        items_removed, o.values.Count, o.name));
					}
				}
				ProtoPartModuleSnapshot first_logger = pp.modules.First(ppm => ppm.moduleName == trip_logger_name);
				first_logger.moduleValues = _new;
				pp.modules.RemoveAll(ppm => ppm.moduleName == trip_logger_name);
				pp.modules.Add(first_logger);
			}
			Utils.logStamp();
		}

		public void Load()
		{
//			fixTripLogger(); //FIXME
			vessel.Load(FlightDriver.FlightStateCache.flightState);
			vessel.LoadObjects();
		}
	}


	public class VesselWaiter
	{
		public Vessel vessel;
		public VesselWaiter(Vessel vsl) { vessel = vsl; }

		public bool launched
		{
			get 
			{
				if(vessel.id != FlightGlobals.ActiveVessel.id) return false;
				vessel = FlightGlobals.ActiveVessel;
				bool parts_inited = true;
				foreach(Part p in vessel.parts)
				{
					if(!p.started)
					{
						parts_inited = false;
						OrbitPhysicsManager.HoldVesselUnpack(2);
						break;
					}
				}
				return parts_inited;
			}
		}
	}

	public class LaunchedVessel : VesselWaiter
	{
		readonly List<ProtoCrewMember> crew;
		readonly StoredVessel sv;
		readonly uint hangar_id;

		public LaunchedVessel(StoredVessel sv, Vessel vsl, List<ProtoCrewMember> crew, uint hangar_id)
			: base(vsl)
		{
			this.sv = sv;
			this.crew = crew;
			this.hangar_id = hangar_id;
		}

		public void transferCrew() 
		{ CrewTransfer.addCrew(vessel, crew); }

		public void tunePosition()
		{
			Vector3 dP = vessel.findLocalCenterOfMass() - sv.CoM;
			vessel.SetPosition(vessel.vesselTransform.TransformPoint(dP));
		}

		public void stiffenWheels()
		{
			Utils.Log("Trying to add WheelUpdaters to the wheels of the launched vessel");
			foreach(Part p in vessel.Parts) 
			{ 
				if(p.HasModule<ModuleWheel>()) 
					p.AddWheelUpdater(hangar_id); 
			}
		}
	}
}

