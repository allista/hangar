using System;
using System.Collections.Generic;
using UnityEngine;

namespace ConvexHullTest
{
	public static class CollectionsExtension
	{
		public static TSource SelectMax<TSource>(this IEnumerable<TSource> s, Func<TSource, float> metric)
		{
			float max_v = -1;
			TSource max_e = default(TSource);
			foreach(TSource e in s)
			{
				float m = metric(e);
				if(m > max_v) { max_v = m; max_e = e; }
			}
			return max_e;
		}

		public static void ForEach<TSource>(this TSource[] a, Action<TSource> action)
		{ foreach(TSource e in a) action(e); }

		public static TSource Pop<TSource>(this LinkedList<TSource> l)
		{
			TSource e = l.Last.Value;
			l.RemoveLast();
			return e;
		}

		public static TSource PopFirst<TSource>(this LinkedList<TSource> l)
		{
			TSource e = l.First.Value;
			l.RemoveFirst();
			return e;
		}
	}

	static class Utils
	{
		public static string formatVector(Vector3 v)
		{ return string.Format("({0}, {1}, {2}); |v| = {3}", v.x, v.y, v.z, v.magnitude); }

		public static void Log(string msg, params object[] args)
		{ 
			for(int i = 0; i < args.Length; i++) 
				if(args[i] is Vector3) args[i] = formatVector((Vector3)args[i]);
			Console.WriteLine(string.Format("[Hangar] "+msg, args)); 
		}
	}

	public class NamedStopwatch
	{
		readonly System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
		readonly string name;

		public NamedStopwatch(string name)
		{ this.name = name; }

		public void Start()
		{
			Utils.Log("{0}: start counting time", name);
			sw.Start();
		}

		public void Stamp()
		{
			Utils.Log("{0}: elapsed time: {1}us", name, 
				sw.ElapsedTicks/(System.Diagnostics.Stopwatch.Frequency/(1000000L)));
		}

		public void Stop() { sw.Stop(); Stamp(); }

		public void Reset() { sw.Stop(); sw.Reset(); }
	}

	class MainClass
	{
		static float NextFloat(System.Random random)
		{
//			double mantissa = (random.NextDouble() * 2.0) - 1.0;
//			double exponent = Math.Pow(2.0, random.Next(-126, 128));
			return (float)((random.NextDouble() * 2.0) - 1.0);
		}

		public static void Main(string[] args)
		{
			int N = 500; int N1 = 10;
			if(args.Length > 0) int.TryParse(args[0], out N);
			if(args.Length > 1) int.TryParse(args[1], out N1);
			var vertices = new Vector3[N];
			var r = new System.Random();
			var sw = new NamedStopwatch("Compute Hull");
			for(int n = 0; n < N1; n++)
			{
				GC.Collect();
				for(int i = 0; i < N; i++)
					vertices[i] = new Vector3(NextFloat(r), NextFloat(r), NextFloat(r)).normalized;
//				sw.Start();
//				var hull = new BruteHull(vertices);
//				sw.Stop();
//				Console.WriteLine(string.Format("BruteHull computed: faces {0}; vertices {1}", hull.Faces.Count, hull.Points.Count));
//				sw.Reset();
				sw.Start();
				var hull1 = new QuickHull(vertices);
				sw.Stop();
				Console.WriteLine(string.Format("QuickHull computed: faces {0}; vertices {1}", hull1.Faces.Count, hull1.Points.Count));
				sw.Reset();
				Console.WriteLine("=========");
			}
		}
	}
}
