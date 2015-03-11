//   PathFinder.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2015 Allis Tauri
//
// This work is licensed under the Creative Commons Attribution 4.0 International License. 
// To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/ 
// or send a letter to Creative Commons, PO Box 1866, Mountain View, CA 94042, USA.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.IO;
using KSPAPIExtensions;

namespace AtHangar
{
	public class PathFinder : PartModule
	{
		const int batch_size = 100;
		SurfaceWalker walker;
		IEnumerator<YieldInstruction> walk;

		[KSPField(isPersistant=true, guiActive=true, guiName="Time", guiFormat = "F1", guiUnits = "s")]
		public float Time;
		public double start_time;

		[KSPField(isPersistant=true, guiActive=true, guiName="Steps")]
		public int Steps;

		[KSPField(isPersistant=true, guiActive=true, guiName="Distance", guiFormat="F4", guiUnits = "arc.s")]
		public float Distance;

		[KSPField(isPersistant=true, guiActive=true, guiName="Ck", guiFormat="F3")]
		[UI_FloatEdit(scene=UI_Scene.Flight, minValue=0.001f, maxValue=0.999f, incrementLarge=0.1f, incrementSmall=0.01f, incrementSlide=0.001f)]
		public float Ck = 0.5f;

		[KSPField(isPersistant=true, guiActive=true, guiName="Bk", guiFormat="F3")]
		[UI_FloatEdit(scene=UI_Scene.Flight, minValue=0.001f, maxValue=0.999f, incrementLarge=0.1f, incrementSmall=0.01f, incrementSlide=0.001f)]
		public float Bk = 0.5f;

		[KSPField(isPersistant=true, guiActive=true, guiName="Ek", guiFormat="F3")]
		[UI_FloatEdit(scene=UI_Scene.Flight, minValue=0.001f, maxValue=0.999f, incrementLarge=0.1f, incrementSmall=0.01f, incrementSlide=0.001f)]
		public float Ek = 0.5f;

		[KSPField(isPersistant=true, guiActive=true, guiName="Sk", guiFormat="F3")]
		[UI_FloatEdit(scene=UI_Scene.Flight, minValue=0f, maxValue=1f, incrementLarge=0.1f, incrementSmall=0.01f, incrementSlide=0.001f)]
		public float Sk = 0.5f;

		[KSPField(isPersistant=true, guiActive=true, guiName="Fk", guiFormat="F3")]
		[UI_FloatEdit(scene=UI_Scene.Flight, minValue=0f, maxValue=1f, incrementLarge=0.1f, incrementSmall=0.01f, incrementSlide=0.001f)]
		public float Fk = 0.5f;

		[KSPField(isPersistant=true, guiActive=true, guiName="Ak", guiFormat="F3")]
		[UI_FloatEdit(scene=UI_Scene.Flight, minValue=0.01f, maxValue=2f, incrementLarge=0.1f, incrementSmall=0.01f, incrementSlide=0.001f)]
		public float Ak = 1f;

		[KSPField(isPersistant=true, guiActive=true, guiName="Hk", guiFormat="F3")]
		[UI_FloatEdit(scene=UI_Scene.Flight, minValue=0.001f, maxValue=1f, incrementLarge=0.1f, incrementSmall=0.01f, incrementSlide=0.001f)]
		public float Hk = 0.1f;

		[KSPField(isPersistant=true, guiActive=true, guiName="Ik", guiFormat="F3")]
		[UI_FloatEdit(scene=UI_Scene.Flight, minValue=0.001f, maxValue=1f, incrementLarge=0.1f, incrementSmall=0.01f, incrementSlide=0.001f)]
		public float Ik = 0.1f;

		[KSPField(isPersistant=true, guiActive=true, guiName="seed", guiFormat="F0")]
		[UI_FloatEdit(scene=UI_Scene.Flight, minValue=0f, maxValue=1000000f, incrementLarge=1000000f, incrementSmall=1000000f, incrementSlide=1f)]
		public float seed = 42f;

		[KSPField(isPersistant=true, guiActive=true, guiName="back step", guiFormat="F0")]
		[UI_FloatEdit(scene=UI_Scene.Flight, minValue=1f, maxValue=1000f, incrementLarge=100f, incrementSmall=10f, incrementSlide=1f)]
		public float back_step = 42f;

		[KSPField(isPersistant=true, guiActive=true, guiName="D", guiFormat="F1", guiUnits = "m")]
		[UI_FloatEdit(scene=UI_Scene.Flight, minValue=1f, maxValue=1000f, incrementLarge=100f, incrementSmall=10f, incrementSlide=1f)]
		public float D = 50f;

		[KSPField(isPersistant=true, guiActive=true, guiName="N", guiFormat="F")]
		[UI_FloatEdit(scene=UI_Scene.Flight, minValue=1000f, maxValue=1000000f, incrementLarge=100000f, incrementSmall=10000f, incrementSlide=1000f)]
		public float N = 100000f;

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			StartCoroutine(slow_update());
		}

		[KSPEvent (guiActive = true, guiName = "Build Route", active = true)]
		public void BuildRoute() 
		{
			if(walker != null) return;
			if(vessel.targetObject == null)
			{ ScreenMessager.showMessage("Build Route: no target"); return; }
			var target = vessel.targetObject.GetVessel();
			if(target == null)
			{ ScreenMessager.showMessage("Build Route: target is not a vessel"); return; }
			else ScreenMessager.showMessage("Building route to '{0}'", target.vesselName);

			var start = new Vector2d(vessel.latitude, vessel.longitude);
			var end = new Vector2d(target.latitude, target.longitude);
			walker = new SurfaceWalker(vessel.mainBody);
			walker.Fk = Fk; walker.Bk = Bk; walker.Ck = Ck;
			walker.Ek = Ek; walker.Sk = Sk; walker.Ak = Ak;
			walker.Hk = Hk; walker.Ik = Ik; walker.back_step = (int)back_step;
			walker.seed = (int)seed;
			walk = walker.Walk(start, end, D, (int)N);
			start_time = Planetarium.GetUniversalTime();
			StartCoroutine(walk);
		}

		[KSPEvent (guiActive = true, guiName = "Build Map", active = true)]
		public void BuildMap() 
		{
			var w = new SurfaceWalker(vessel.mainBody);
			w.BuildFullMap(0.5, string.Format("../MassCalc/{0}_map", vessel.mainBody.bodyName));
		}

		IEnumerator<YieldInstruction> slow_update()
		{
			while(true)
			{
				yield return new WaitForSeconds(0.1f);
				if(walker == null) continue;
				Steps = walker.Steps;
				Distance = (float)walker.Delta.magnitude*60;
				Time = (float)(Planetarium.GetUniversalTime()-start_time);
				if(walker.PathFound || walker.Steps <= 0)
				{
					yield return null;
					Utils.Log("BuildRoute:\n" +
					          "Succeded: {0}\n" +
					          "Final Delta: {1}", walker.PathFound, walker.Delta); //debug
					walker.SavePath("../MassCalc/path");
					yield return null;
					var lat_ext = walker.Path.Select(n => n.lat).ToList();
					lat_ext.Add(walker.Start.x);
					lat_ext.Add(walker.End.x);
					lat_ext.Sort();
					var lon_ext = walker.Path.Select(n => n.lon).ToList();
					lon_ext.Add(walker.Start.y);
					lon_ext.Add(walker.End.y);
					lon_ext.Sort();
					var map_delta = Math.Max((lat_ext[lat_ext.Count-1]-lat_ext[0]+2)/600,
					                         (lon_ext[lon_ext.Count-1]-lon_ext[0]+2)/600);
					yield return null;
					walker.BuildMap(
						lat_ext[0]-1, lat_ext[lat_ext.Count-1]+1,
						lon_ext[0]-1, lon_ext[lat_ext.Count-1]+1,
						map_delta, "../MassCalc/Current_map");
					walker = null;
				}
			}
		}

		class SurfaceWalker
		{
			const double max_angle = 15;
			double delta = 1e-2;
			public double Fk, Bk, Ck, Ek, Sk, Ak, Hk, Ik;
			public int seed, back_step;

			CelestialBody body;
			Neighbour[] neighbours = new Neighbour[8];
			MapNode start, end;
			double distance, cur_dist, frac_dist, inv_frac_dist, smoothing;

			MapNode current { get { return Path.Empty? start : Path.Last; } }
			MapNode previous { get { return Path.Multinode? Path.Last.prev: start; } }

			HashSet<MapNode> closed = new HashSet<MapNode>();
			public MapPath Path, FullPath;
			public bool Walking { get; private set; }
			public bool PathFound { get; private set; }
			public Vector2d Delta { get; private set; }
			public int Steps { get; private set; }
			public Vector2d Start { get { return start != null? start.pos : Vector2d.zero; }}
			public Vector2d End { get { return end != null? end.pos : Vector2d.zero; }}

			public SurfaceWalker(CelestialBody body)
			{
				this.body = body;
				Path = new MapPath(body);
				FullPath = new MapPath(body);//debug
			}

			bool update_neighbours()
			{
				var c  = current;
				var p  = previous;
				var ds = c.DistanceTo(start).magnitude;
				var t  = Path.Tail((int)Math.Ceiling(smoothing*frac_dist)+1)?? p;
				var total_p = 0.0;
				var ind = 0;
				for(int i = -1; i < 2; i++)
					for(int j = -1; j < 2; j++)
					{
						if(i == 0 && j == 0) continue;
						var n  = new Neighbour(new MapNode(c.lat+i*delta, c.lon+j*delta, body));
						if(Path.Multinode && n.node.SameAs(p)) 
						{ n.node = p; n.isPrevious = true; }
						var incline_mod = step_incline_mod(c, n.node, cur_dist);
						if(incline_mod > 0)
						{
							if(closed.Contains(n.node))
								n.prob *= Ck;
							else if(Path.Multinode && 
							        Vector2d.Dot(t.DistanceTo(c), c.DistanceTo(n.node)) <= 0)
								n.prob *= Bk;
							else
								n.prob *= incline_mod*delta_prob(n.node, cur_dist, ds)/Bk/Ck;
						} else n.prob = 0;
						total_p += n.prob;
						neighbours[ind++] = n;
					}
				if(total_p.Equals(0)) return false;
				for(int i = 0; i < 8; i++) neighbours[i].prob /= total_p;
				Array.Sort(neighbours, (a, b) => a.prob.CompareTo(b.prob));
				return true;
			}

			double delta_prob(MapNode n, double de, double ds)
			{
				var nde = n.DistanceTo(end).magnitude;
				if(nde < de) return 1 + Math.Pow((de-nde)/delta/1.4142137, 0.5+Fk);

				var nds = n.DistanceTo(start).magnitude;
				return nds < ds ? (1 - Math.Pow((ds-nds)/delta/1.4142137, 0.5+Sk)) * Ek : Ek;
			}

			double step_incline_mod(MapNode p1, MapNode p2, double de)
			{
				if(body.ocean && p2.height < 0) return 0;
				var wp1 = body.GetWorldSurfacePosition(p1.lat, p1.lon, 0);
				var wp2 = body.GetWorldSurfacePosition(p2.lat, p2.lon, 0);
				var tan = (p2.height-p1.height)/(wp2-wp1).magnitude;
//				var dir = (end.height-p1.height);
				var mod = (max_angle-Math.Atan(Math.Abs(tan))*Mathf.Rad2Deg)/max_angle;
				return mod > 0? Math.Pow(mod, Ak*frac_dist) : 0;
			}

			public IEnumerator<YieldInstruction> Walk(Vector2d start_point, Vector2d end_point, double d = 0.01, int max_steps = 10000)
			{
				if(max_steps < 1000) max_steps = 1000;
				delta = d/body.Radius * Mathf.Rad2Deg;
				if(Math.Abs(end_point.y - start_point.y) > 180)
				{
					if(end_point.y > start_point.y) end_point.y -= 360;
					else end_point.y += 360;
				}
				start = new MapNode(start_point, body);
				end = new MapNode(end_point, body);
				distance = start.DistanceTo(end).magnitude;
				smoothing = Ik/delta;
				closed.Clear(); 
				Path.Clear(); FullPath.Clear();//debug
				Path.Add(start); FullPath.Add(new MapNode(start.pos, body));//debug
				var R = new System.Random(seed);
				var batch = batch_size;
				Steps = max_steps;
				yield return null;
				Walking = true;
				while(Steps > 0)
				{
					Delta = current.DistanceTo(end);
					cur_dist = Delta.magnitude;
					frac_dist = Math.Pow(cur_dist/distance, Hk);
//					inv_frac_dist = 1-frac_dist > 1e-5? 1-frac_dist : 1e-5;
					//					Utils.Log("Global delta: {0}", Delta);//debug
					if(cur_dist <= delta*2)
					{ Path.Add(end); Delta = Vector2d.zero; PathFound = true; break; }
					if(!update_neighbours()) { Utils.Log("No suitable neighbours found!"); break; }//debug
					var sample = R.NextDouble();
					var next = new Neighbour();
					foreach(var n in neighbours)
					{ 
						if(n.prob >= sample) { next = n; break; } 
						sample -= n.prob;
					}
					if(next.isPrevious) 
					{
//						Utils.Log("Neighbours:\n{0}", //debug
//						          neighbours.Aggregate("", 
//						                       		   (s, n) => 
//						                               s+string.Format("delta {0}, {1}; prob {2}; prev {3}\n", 
//						                                               end.lat-n.node.lat, end.lon-n.node.lon, n.prob, n.isPrevious)));
//						closed.Add(current);
//						Path.MakeLast(previous);
						var t = Path.Tail((int)Math.Ceiling(back_step*frac_dist)+1)?? previous;
						Path.MakeLast(t).ToList().ForEach(n => closed.Add(n));
//						Utils.Log("Step back!\nClosed nodes: {0}\nChoosed the neighbour with P={1}", closed.Count, next.prob);  //debug
					}
					else 
					{ 
						var node = Path.Find(next.node);
						if(node == null) Path.Add(next.node);
						else Path.MakeLast(node).ToList().ForEach(n => closed.Add(n));
					}
					FullPath.Add(new MapNode(next.node.pos, body));//debug
					Steps--;
					batch--;
					if(batch <= 0)
					{
						batch = batch_size;
						yield return null;
					}
				}
				Walking = false;
			}

			public void BuildFullMap(double d, string filename)
			{ BuildMap(-90, 90, 0, 360, d, filename); }

			public void BuildMap(double lat_min, double lat_max, double lon_min, double lon_max, double d, string filename)
			{
				using(var file = new StreamWriter(filename+".py"))
				{
					file.WriteLine(@"
import numpy as np
Map = np.array([");
					for(double lat = lat_min; lat < lat_max; lat += d)
					{
						file.WriteLine("[");
						for(double lon = lon_min; lon < lon_max; lon += d)
						{
							var n = new MapNode(lat, lon, body);
							file.WriteLine(string.Format("[{0}, {1}, {2}],", n.lat, n.lon, n.height));
						}
						file.WriteLine("],");
					}
					file.WriteLine("])");
				}
			}

			public void SavePath(string filename)
			{
				using(var file = new StreamWriter(filename+".py"))
				{
					file.WriteLine(@"
import numpy as np
path = np.array([");
					file.WriteLine(string.Format("[{0}, {1}],", start.lat, start.lon));
					foreach(var p in FullPath)//debug
						file.WriteLine(string.Format("[{0}, {1}],", p.lat, p.lon));
					file.WriteLine(string.Format("[{0}, {1}],", end.lat, end.lon));
					file.WriteLine("])");
				}
			}

			public class MapNode : IEnumerable<MapNode>, IEquatable<MapNode>
			{
				public const double eps = 1e-5;

				CelestialBody body;
				public readonly Vector2d pos;
				public readonly double height;
				public double lat { get { return pos.x; } }
				public double lon { get { return pos.y; } }
				public Vector3d pos3d { get { return new Vector3d(pos.x, pos.y, height); } }

				public MapNode prev, next;

				double get_height(Vector2d p)
				{ return body.pqsController.GetSurfaceHeight(body.GetRelSurfaceNVector(p.x, p.y))-body.Radius; }

				public MapNode(Vector2d pos, CelestialBody body)
				{
					this.body = body;
					this.pos = pos;
					height = get_height(pos);
					prev = null;
					next = null;
				}

				public MapNode(double lat, double lon, CelestialBody body)
					: this(new Vector2d(lat, lon), body) {}

				public bool SameAs(MapNode other)
				{
					return 
						Math.Abs(pos.x - other.pos.x) < eps &&
						Math.Abs(pos.y - other.pos.y) < eps;
				}

				public bool Equals(MapNode other) { return SameAs(other); }
				public override bool Equals(object obj)
				{
					var other = obj as MapNode;
					return other != null && SameAs(other);
				}
				public override int GetHashCode() { return (int)(pos.x/eps) ^ (int)(pos.y/eps); }

				public Vector2d DistanceTo(MapNode other)
				{ return other.pos - pos; }

				public static implicit operator Vector3d(MapNode n)	{ return n.pos3d; }

				public IEnumerator<MapNode> GetEnumerator()
				{
					var c = this;
					while(c != null)
					{
						yield return c;
						c = c.next;
					}
				}
				IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
			}

			public class MapPath : IEnumerable<MapNode>
			{
				CelestialBody body;
				public MapNode First { get; private set; }
				public MapNode Last { get; private set; }
				public bool Empty { get { return First == null; } }
				public bool Multinode { get { return First != Last; } }

				public MapPath(CelestialBody body) { this.body = body; }

				public MapNode Add(double lat, double lon, double h)
				{
					var n = new MapNode(lat, lon, body);
					Add(n);
					return n;
				}

				public MapNode Add(Vector2d pos, double h)
				{
					var n = new MapNode(pos, body);
					Add(n);
					return n;
				}

				public void Add(MapNode n)
				{
					if(Last != null)
					{
						n.prev = Last;
						Last.next = n;
					}
					else First = n;
					Last = n;
				}

				public void Clear() { First = null; Last = null; }

				public MapNode Find(MapNode n)
				{
					var c = Last;
					while(c != null) 
					{
						if(c.SameAs(n)) 
							return c;
						c = c.prev;
					} 
					return null;
				}

				public MapNode Tail(int i)
				{
					var c = Last;
					while(c != null && i > 0) 
					{
						c = c.prev;
						i--;
					} 
					return c;
				}

				public IEnumerable<MapNode> MakeLast(MapNode n)
				{
					if(n == Last) return null;
					var f = n.next;
					Last = n; 
					Last.next = null;
					f.prev = null;
					return f;
				}

				public IEnumerator<MapNode> GetEnumerator()
				{
					var c = First;
					while(c != null)
					{
						yield return c;
						c = c.next;
					}
				}
				IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
			}

			struct Neighbour
			{
				public MapNode node;
				public double prob;
				public bool isPrevious;

				public Neighbour()
				{
					node = null;
					prob = -1;
					isPrevious = false;
				}

				public Neighbour(MapNode n) 
				{ 
					node = n;
					prob = 1;
					isPrevious = false;
				}
			}
		}
	}
}

