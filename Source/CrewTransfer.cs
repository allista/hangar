using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;


namespace AtHangar
{
	public class CrewTransfer
	{
		//real vessel
		//add some crew to a part
		public static bool addCrew(Part p, List<ProtoCrewMember> crew)
		{
			if(crew.Count == 0) return false;
			if(p.CrewCapacity <= p.protoModuleCrew.Count) return false;
			while(p.protoModuleCrew.Count < p.CrewCapacity && crew.Count > 0)
			{
				ProtoCrewMember kerbal = crew[0];
				p.AddCrewmember(kerbal);
				kerbal.rosterStatus = ProtoCrewMember.RosterStatus.Assigned;
				if (kerbal.seat != null) kerbal.seat.SpawnCrew();
				crew.RemoveAt(0);
			}
			return true;
		}
		
		//add some crew to a vessel
		public static void addCrew(Vessel vsl, List<ProtoCrewMember> crew)
		{
			foreach(Part p in vsl.parts)
			{
				if(crew.Count == 0) break;
				addCrew(p, crew);
			}
			GameEvents.onVesselChange.Fire(vsl);
		}
		
		//remove crew from a part
		public static List<ProtoCrewMember> delCrew(Part p, List<ProtoCrewMember> crew)
		{
			List<ProtoCrewMember> deleted = new List<ProtoCrewMember>();
			if(p.CrewCapacity == 0 || p.protoModuleCrew.Count == 0) return deleted;
			foreach(ProtoCrewMember kerbal in crew)
			{ 
				ProtoCrewMember part_kerbal = p.protoModuleCrew.Find(k => k.name == kerbal.name);
				if(part_kerbal != null) 
				{
					deleted.Add(part_kerbal);
					p.RemoveCrewmember(part_kerbal);
					part_kerbal.seat = null;
				}
			}
			return deleted;
		}
		
		//remove crew from a vessel
		public static List<ProtoCrewMember> delCrew(Vessel vsl, List<ProtoCrewMember> crew)
		{
			List<ProtoCrewMember> deleted = new List<ProtoCrewMember>();
			foreach(Part p in vsl.parts)	
				deleted.AddRange(delCrew(p, crew));
			GameEvents.onVesselChange.Fire(vsl);
			vsl.SpawnCrew();
			return deleted;
		}
	}
}

