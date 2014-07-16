//Packing algorithm based on <http://www.blackpawn.com/texts/lightmaps/default.html>  
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace AtHangar
{
	public class Node
	{
		//size, position and stored vessel
		public Vector3 pos  = Vector3.zero;
		public Vector3 size = Vector3.zero;
		public Guid vid = Guid.Empty;
		
		//children
		public Node first  = null;
		public Node second = null;
		
		public Node() {}
		public Node(Vector3 pos, Vector3 size)
		{
			this.pos  = new Vector3(pos.x, pos.y, pos.z);
			this.size = new Vector3(size.x, size.y, size.z);
		}
		public Node(Metric space) : this(Vector3.zero, space.size) { }
		public Node(Node n) : this(n.pos, n.size) {}
		
		public static bool operator>(Node n, Vector3 s)
		{ return n.size.x > s.x && n.size.y > s.y && n.size.z > s.z; }
		
		public static bool operator<(Node n, Vector3 s)
		{ return n.size.x < s.x || n.size.y < s.y || n.size.z < s.z; }
		
		public void Save(ConfigNode node, bool debug = true)
		{
			node.AddValue("pos", ConfigNode.WriteVector(pos));
			node.AddValue("size", ConfigNode.WriteVector(size));
			node.AddValue("vid", vid);
			if(first != null)
			{
				ConfigNode f_node = node.AddNode("FIRST");
				ConfigNode s_node = node.AddNode("SECOND");
				first.Save(f_node, false); second.Save(s_node, false);
			}
			if(debug) Debug.Log(node);
		}
	}
	
	public class VesselsPack
	{
		private Dictionary<Guid, StoredVessel> stored_vessels = new Dictionary<Guid, StoredVessel>();
		public Metric space = new Metric();
		
		public VesselsPack() {}
		public VesselsPack(Metric space) { this.space = space; }
		
		private bool add_vessel(Node n, StoredVessel vsl)
		{
			if(n.first != null)
			{
				if(add_vessel(n.first, vsl)) return true;
				return add_vessel(n.second, vsl);
			}
			else
			{
				Vector3 s = vsl.metric.size;
				//if node is used, cannot store
				if(n.vid != Guid.Empty || n < s) return false;
				//if the vessel fits perfectly, store it
				if(n.size == s)
				{
					n.vid = vsl.vessel.vesselID;
					return true;
				}
				//clone the node
				n.first  = new Node(n);
				n.second = new Node(n);
				//space leftovers
				float dx = n.size.x - s.x;
				float dy = n.size.y - s.y;
				float dz = n.size.z - s.z;
				//partition node space
				if(dx > dy && dx > dz)
				{
					n.first.size.x   = s.x;
					n.second.pos.x  += s.x;
					n.second.size.x -= s.x;
				}
				else if(dy > dx && dy > dz)
				{
					n.first.size.y   = s.y;
					n.second.pos.y  += s.y;
					n.second.size.y -= s.y;
				}
				else
				{
					n.first.size.z   = s.z;
					n.second.pos.z  += s.z;
					n.second.size.z -= s.z;
				}
				//fit in the first subnode
				return add_vessel(n.first, vsl);
			}
		}
		
		private bool pack(List<StoredVessel> vessels)
		{
			vessels.Sort((x,y) => -1*x.metric.volume.CompareTo(y.metric.volume)); //Descending sort order
			Node root = new Node(space);
			foreach(StoredVessel vsl in vessels) { if(!add_vessel(root, vsl)) { root.Save(new ConfigNode("VesselsPack")); return false; } }
			root.Save(new ConfigNode("VesselsPack")); //Debug
			return true;
		}
		
		public bool Add(StoredVessel vsl)
		{
			List<StoredVessel> vessels = this.Values;
			vessels.Add(vsl);
			Utils.logBounds(vsl.metric.bounds); //Debug
			Utils.logBounds(space.bounds); //Debug
			if(!pack(vessels)) return false;
			stored_vessels.Add(vsl.vessel.vesselID, vsl);
			return true;
		}
		
		public void Set(List<StoredVessel> vessels)
		{
			stored_vessels.Clear();
			foreach(StoredVessel sv in vessels) 
				stored_vessels.Add(sv.vessel.vesselID, sv);
		}
		
		public bool Repack() { return pack(this.Values); }
		
		//mimic Dictionary
		public void Remove(Guid vid)
		{
			if(!stored_vessels.ContainsKey(vid)) return;
			stored_vessels.Remove(vid);
		}
		
		public bool ContainsKey(Guid vid) { return stored_vessels.ContainsKey(vid); }
		
		public bool TryGetValue(Guid vid, out StoredVessel vessel)
		{ return stored_vessels.TryGetValue(vid, out vessel); }
		
		public int Count { get { return stored_vessels.Count; } }
		public List<Guid> Keys { get { return new List<Guid>(stored_vessels.Keys); } }
		public List<StoredVessel> Values { get { return new List<StoredVessel>(stored_vessels.Values); } }
		public StoredVessel this[Guid vid] { get { return stored_vessels[vid]; } }
	}
}
