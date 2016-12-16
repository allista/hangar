//   PartSpaceManager.cs
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
	public class HangarSpaceManager : ConfigNodeObject
	{
		protected Part part;

		[Persistent] public string HangarSpace = string.Empty;

		public MeshFilter Space { get; protected set; }
		public Metric SpaceMetric { get; protected set; }
		public virtual bool Valid { get { return !SpaceMetric.Empty; } }

		public override void Load(ConfigNode node)
		{
			base.Load(node);
			if(!string.IsNullOrEmpty(HangarSpace))
			{
				Space = part.FindModelComponent<MeshFilter>(HangarSpace);
				SpaceMetric = new Metric(part, HangarSpace);
				if(Space != null) flip_mesh_if_needed(Space);
			}
		}

		public void SetMetric(Metric metric) { SpaceMetric = metric; }

		public void UpdateMetric()
		{
			if(!string.IsNullOrEmpty(HangarSpace))
				SpaceMetric = new Metric(part, HangarSpace);
		}

		public HangarSpaceManager(Part part)
		{ this.part = part; }

		protected void flip_mesh_if_needed(MeshFilter mesh_filter)
		{
			//check if the hangar space has its normals flipped iside; if not, flip them
			var flipped = false;
			var mesh   = mesh_filter.sharedMesh;
			var tris   = mesh.triangles;
			var verts  = mesh.vertices;
			var center = mesh.bounds.center;
			for(int i = 0, len = tris.Length/3; i < len; i++)
			{
				var j = i*3;
				var p = new Plane(verts[tris[j]], verts[tris[j+1]], verts[tris[j+2]]);
				var outside = !p.GetSide(center);
				if(outside)
				{
					var t = tris[j];
					tris[j] = tris[j+2];
					tris[j+2] = t;
					flipped = true;
				}
			}
			if(flipped)
			{
				part.Log("The '{}' mesh is not flipped. Hangar space normals should be pointed INSIDE.", mesh_filter.name);
				mesh.triangles = tris;
				mesh.RecalculateNormals();
			}
		}

		public bool VesselFits(PackedVessel v, Transform position, Vector3 offset)
		{
			return Space != null? 
				v.metric.FitsAligned(position, Space.transform, Space.sharedMesh, offset) :
				v.metric.FitsAligned(position, part.partTransform, SpaceMetric, offset);
		}
	}

	public class VesselSpawnManager : HangarSpaceManager
	{
		#region AutoRotation
		static readonly Quaternion xyrot = Quaternion.Euler(0, 0, 90);
		static readonly Quaternion xzrot = Quaternion.Euler(0, 90, 0);
		static readonly Quaternion yzrot = Quaternion.Euler(90, 0, 0);
		static readonly Quaternion[,] swaps = 
		{
			{Quaternion.identity, 	xyrot, 					xzrot}, 
			{xyrot.Inverse(), 		Quaternion.identity, 	yzrot}, 
			{xzrot.Inverse(), 		yzrot.Inverse(), 		Quaternion.identity}
		};

		static List<KeyValuePair<float, int>> sort_vector(Vector3 v)
		{
			var s = new List<KeyValuePair<float, int>>(3);
			s.Add(new KeyValuePair<float, int>(v[0], 0));
			s.Add(new KeyValuePair<float, int>(v[1], 1));
			s.Add(new KeyValuePair<float, int>(v[2], 2));
			s.Sort((x, y) => x.Key.CompareTo(y.Key));
			return s;
		}
		#endregion

		[Persistent] public bool AutoPositionVessel;
		[Persistent] public Vector3 SpawnOffset = Vector3.zero;
		[Persistent] public string SpawnTransform = string.Empty;
		protected Transform spawn_transform;

		public override bool Valid
		{ get { return base.Valid && spawn_transform != null; } }

		public VesselSpawnManager(Part part) : base(part) {}

		public override void Load(ConfigNode node)
		{
			base.Load(node);
			if(AutoPositionVessel) 
				SpawnOffset = Vector3.zero;
			if(!string.IsNullOrEmpty(SpawnTransform))
				spawn_transform = part.FindModelTransform(SpawnTransform);
			if(spawn_transform == null)
			{
				var launch_empty = new GameObject();
				var parent = Space != null? Space.transform : part.transform;
				launch_empty.transform.SetParent(parent);
				spawn_transform = launch_empty.transform;
			}
		}

		public Vector3 GetSpawnOffset(PackedVessel v)
		{ return SpawnOffset.IsZero() ? SpawnOffset : Vector3.Scale(v.metric.extents, SpawnOffset); }

		public Transform GetSpawnTransform(PackedVessel v = null)
		{
			if(AutoPositionVessel && v != null) 
			{
				var s_size = sort_vector(SpaceMetric.size);
				var v_size = sort_vector(v.size);
				var r1 = swaps[s_size[0].Value, v_size[0].Value];
				var i2 = s_size[0].Value == v_size[1].Value? 2 : 1;
				var r2 = swaps[s_size[i2].Value, v_size[i2].Value];
				spawn_transform.localPosition = Vector3.zero;
				spawn_transform.localRotation = Quaternion.identity;
				spawn_transform.rotation = part.transform.rotation * r2 * r1;
			}
			return spawn_transform;
		}

		public bool VesselFits(PackedVessel v)
		{ return VesselFits(v, GetSpawnTransform(v), GetSpawnOffset(v)); }
	}
}

