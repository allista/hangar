//Packing algorithm based on <http://www.blackpawn.com/texts/lightmaps/default.html>  
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AtHangar
{
	public abstract class PackedVessel 
	{ 
		public Metric metric; 
		public Guid id; 
		public string  name     { get; protected set; }
		public Vector3 size     { get { return metric.size; } }
		public float   volume   { get { return metric.volume; } }
		public float   mass     { get { return metric.mass; } set { metric.mass = value; } }
		public float   cost     { get { return metric.cost; } set { metric.cost = value; } }
		public Vector3 CoG      { get { return metric.center; } } //center of geometry
		public int CrewCapacity { get{ return metric.CrewCapacity; } }

		public abstract void Save(ConfigNode node);
		public abstract void Load(ConfigNode node);
	}

	public class VesselsPack<V> : IEnumerable<PackedVessel> where V : PackedVessel, new()
	{
		Dictionary<Guid, V> stored_vessels = new Dictionary<Guid, V>();
		public Metric space;

		public bool  UsePacking = true;
		public float VesselsMass;
		public float VesselsCost;
		public float UsedVolume;

		public VesselsPack() {}
		public VesselsPack(bool use_packing) { UsePacking = use_packing; }
		public VesselsPack(Metric space) { this.space = space; }

		#region Packing
		/// <summary>
		/// Chooses the best rotation of a size vector "s" to store it inside the box with the size vector "box_size".
		/// The rotation is considered optimal if it gives the absolute maximum remainder in one of the dimensions,
		/// if in other dimensions the vector still fits.
		/// </summary>
		/// <param name="box_size">Size vector of the box where the smaller box will be packed.</param>
		/// <param name="s">Size vector of a small box to be packed.</param>
		static Vector3 optimal_rotation(Vector3 box_size, Vector3 s)
		{
			Vector3[] vs = {s, //all six permutations of coorditates
							new Vector3(s.x, s.z, s.y),
							new Vector3(s.y, s.x, s.z),
							new Vector3(s.y, s.z, s.x),
							new Vector3(s.z, s.x, s.y),
							new Vector3(s.z, s.y, s.x)};
			Vector3 optimal = s; float delta = 0f;
			foreach(Vector3 v in vs)
			{
				Vector3 d = box_size-v; //space remainder
				if(d.x < 0 || d.y < 0 || d.z < 0) continue; //doesn't fit in one of the dimensions
				float[] d_values = {d.x, d.y, d.z}; 
				float max_d = d_values.Max();
				if(delta < max_d) { delta = max_d; optimal = v; }
			}
			return optimal;
		}
		
		bool add_vessel(Node n, Vector3 s, Guid id)
		{
			if(n.first != null)
				return add_vessel(n.first, s, id) || add_vessel(n.second, s, id);
			else
			{
				//if leaf node is used or smaller, cannot store
				if(n.vid != Guid.Empty || n < s) return false;
				//if the vessel fits perfectly, store it
				if(n.Matches(s))
				{
					n.vid = id;
					return true;
				}
				//clone the node
				n.first  = new Node(n);
				n.second = new Node(n);
				//rotate the vessel if needed
				s = optimal_rotation(n.size, s);
				//space remainder
				Vector3 d = n.size - s;
				//partition node space
				if(d.x > d.y && d.x > d.z)
				{
					n.first.size.x   = s.x;
					n.second.pos.x  += s.x;
					n.second.size.x -= s.x;
				}
				else if(d.y > d.x && d.y > d.z)
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
				return add_vessel(n.first, s, id);
			}
		}

		static void sort_vessels(List<V> vessels)
		{ vessels.Sort((x,y) => -1*x.metric.volume.CompareTo(y.metric.volume)); } //Descending sort order

		bool pack(List<V> vessels)
		{
			sort_vessels(vessels);
			var root = new Node(space);
			foreach(V vsl in vessels)
			{ if(!add_vessel(root, vsl.size, vsl.id)) return false; }
			return true;
		}

		List<V> pack_some(List<V> vessels)
		{
			sort_vessels(vessels);
			var root = new Node(space);
			var rem  = new List<V>();
			foreach(V vsl in vessels) 
			{ if(!add_vessel(root, vsl.size, vsl.id)) rem.Add(vsl); }
			return rem;
		}
		#endregion

		#region Main Interface
		void change_params(V vsl, int k=1)
		{
			VesselsMass += k*vsl.mass;
			VesselsCost += k*vsl.cost;
			UsedVolume  += k*vsl.volume;
			if(UsedVolume  < 0) UsedVolume  = 0;
			if(VesselsMass < 0) VesselsMass = 0;
			if(VesselsCost < 0) VesselsCost = 0;
		}

		public void UpdateParams()
		{
			VesselsMass = 0; VesselsCost = 0; UsedVolume  = 0;
			foreach(V vsl in stored_vessels.Values)
				change_params(vsl);
		}

		public bool CanAdd(V vsl)
		{
			if(UsePacking)
			{
				var vessels = Values;
				vessels.Add(vsl);
				return pack(vessels);
			}
			return space.volume-UsedVolume-vsl.volume >= 0;
		}

		public bool TryAdd(V vsl)
		{
			if(!CanAdd(vsl)) return false;
			stored_vessels.Add(vsl.id, vsl);
			change_params(vsl);
			return true;
		}

		public void ForceAdd(V vsl) 
		{ 
			stored_vessels.Add(vsl.id, vsl);
			change_params(vsl);
		}

		public void Set(List<V> vessels)
		{
			stored_vessels.Clear();
			vessels.ForEach(ForceAdd);
		}

		public bool Remove(V vsl) 
		{ 
			if(stored_vessels.Remove(vsl.id))
			{ change_params(vsl, -1); return true; }
			return false;
		}

		public void Clear() 
		{ 
			stored_vessels.Clear();
			VesselsMass = 0;
			VesselsCost = 0;
			UsedVolume  = 0;
		}
		
		public int Count { get { return stored_vessels.Count; } }
		public List<V> Values { get { return new List<V>(stored_vessels.Values); } }

		#region IEnumerable
		public IEnumerator<PackedVessel> GetEnumerator()
		{ foreach(var v in stored_vessels.Values) yield return v; }

		IEnumerator IEnumerable.GetEnumerator()
		{ return GetEnumerator(); }
		#endregion

		#region Save Load
		public void Save(ConfigNode node)
		{
			foreach(V vsl in stored_vessels.Values)
			{
				ConfigNode stored_vessel_node = node.AddNode("STORED_VESSEL");
				vsl.Save(stored_vessel_node);
			}
		}

		public void Load(ConfigNode node)
		{
			var vessels = new List<V>();
			foreach(ConfigNode vn in node.nodes)
			{
				var vsl = new V();
				vsl.Load(vn);
				vessels.Add(vsl);
			}
			Set(vessels);
		}
		#endregion
		#endregion

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

			//all possible rotations of "s" are considered
			public static bool operator>(Node n, Vector3 s)
			{ 
				float[] nd = { n.size.x, n.size.y, n.size.z };
				float[] sd = { s.x, s.y, s.z };
				Array.Sort(nd); Array.Sort(sd);
				return nd[0] > sd[0] && nd[1] > sd[1] && nd[2] > sd[2]; 
			}

			//all possible rotations of "s" are considered
			public static bool operator<(Node n, Vector3 s)
			{ 
				float[] nd = { n.size.x, n.size.y, n.size.z };
				float[] sd = { s.x, s.y, s.z };
				Array.Sort(nd); Array.Sort(sd);
				return nd[0] < sd[0] || nd[1] < sd[1] || nd[2] < sd[2]; 
			}


			/// <summary>
			/// Compares size of the node with a given vector, 
			/// considering all permutations of coordinates.
			/// </summary>
			/// <param name="s">A vector to which the node is compared</param>
			public bool Matches(Vector3 s)
			{ 
				float[] nd = { size.x, size.y, size.z };
				float[] sd = { s.x, s.y, s.z };
				Array.Sort(nd); Array.Sort(sd);
				return nd[0] == sd[0] && nd[1] == sd[1] && nd[2] == sd[2];
			}

			public void Save(ConfigNode node, bool debug = true)
			{
				node.AddValue("pos", ConfigNode.WriteVector(pos));
				node.AddValue("size", ConfigNode.WriteVector(size));
				if(first != null)
				{
					ConfigNode f_node = node.AddNode("FIRST");
					ConfigNode s_node = node.AddNode("SECOND");
					first.Save(f_node, false); second.Save(s_node, false);
				}
				else node.AddValue("vid", vid);
				if(debug) Debug.Log(node);
			}
		}
	}
}

