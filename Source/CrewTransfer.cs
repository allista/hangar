using System.Collections.Generic;

namespace AtHangar
{
	public static class CrewTransfer
	{
		#region Vessel
		//add some crew to a part
		public static bool addCrew(Part p, List<ProtoCrewMember> crew, bool spawn = true)
		{
			if(crew.Count == 0) return false;
			if(p.CrewCapacity <= p.protoModuleCrew.Count) return false;
			while(p.protoModuleCrew.Count < p.CrewCapacity && crew.Count > 0)
			{
				var kerbal = crew[0];
				p.AddCrewmember(kerbal);
				crew.RemoveAt(0);
			}
			if(spawn)
			{
				Vessel.CrewWasModified(p.vessel);
				p.vessel.DespawnCrew();
				p.StartCoroutine(CallbackUtil.DelayedCallback(1, p.vessel.SpawnCrew));
			}
			return true;
		}
		
		//add some crew to a vessel
		public static void addCrew(Vessel vsl, List<ProtoCrewMember> crew, bool spawn = true)
		{
			foreach(Part p in vsl.parts)
			{
				if(crew.Count == 0) break;
				addCrew(p, crew, false);
			}
			Vessel.CrewWasModified(vsl);
			vsl.DespawnCrew();
			if(spawn) vsl.StartCoroutine(CallbackUtil.DelayedCallback(1, vsl.SpawnCrew));
		}
		
		//remove crew from a part
		public static List<ProtoCrewMember> delCrew(Part p, List<ProtoCrewMember> crew, bool spawn = true)
		{
			var deleted = new List<ProtoCrewMember>();
			if(p.CrewCapacity == 0 || p.protoModuleCrew.Count == 0) return deleted;
			foreach(ProtoCrewMember kerbal in crew)
			{ 
				ProtoCrewMember part_kerbal = p.protoModuleCrew.Find(k => k.name == kerbal.name);
				if(part_kerbal != null) 
				{
					deleted.Add(part_kerbal);
					p.RemoveCrewmember(part_kerbal);
				}
			}
			if(spawn && deleted.Count > 0)
			{
				Vessel.CrewWasModified(p.vessel);
				p.vessel.DespawnCrew();
				p.StartCoroutine(CallbackUtil.DelayedCallback(1, p.vessel.SpawnCrew));
			}
			return deleted;
		}
		
		//remove crew from a vessel
		public static List<ProtoCrewMember> delCrew(Vessel vsl, List<ProtoCrewMember> crew, bool spawn = true)
		{
			var deleted = new List<ProtoCrewMember>();
			vsl.parts.ForEach(p => deleted.AddRange(delCrew(p, crew, false)));
			Vessel.CrewWasModified(vsl);
			vsl.DespawnCrew();
			if(spawn) vsl.StartCoroutine(CallbackUtil.DelayedCallback(1, vsl.SpawnCrew));
			return deleted;
		}
		#endregion

		#region ProtoVessel
		//add some crew to a part
		public static bool addCrew(ProtoPartSnapshot p, List<ProtoCrewMember> crew)
		{
			if(crew.Count == 0) return false;
			if(p.partInfo.partPrefab.CrewCapacity <= p.protoModuleCrew.Count) return false;
			while(p.protoModuleCrew.Count < p.partInfo.partPrefab.CrewCapacity && crew.Count > 0)
			{
				var kerbal = crew[0];
				kerbal.rosterStatus = ProtoCrewMember.RosterStatus.Assigned;
				p.protoCrewNames.Add(kerbal.name);
				p.protoModuleCrew.Add(kerbal);
				crew.RemoveAt(0);
			}
			return true;
		}

		//add some crew to a vessel
		public static void addCrew(ProtoVessel vsl, List<ProtoCrewMember> crew)
		{
			foreach(var p in vsl.protoPartSnapshots)
			{
				if(crew.Count == 0) break;
				addCrew(p, crew);
			}
		}
		#endregion
	}
}

