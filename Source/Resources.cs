//This code is based on code from Extraplanetary Launchpads plugin. Resources.cs module. 
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using KSP.IO;


namespace AtHangar 
{
//ProtoPartResourceSnapshot.resourceValues:
//	RESOURCE
//	{
//		name = MonoPropellant
//		amount = 7.5
//		maxAmount = 7.5
//		flowState = True
//		isTweakable = True
//		hideFlow = False
//		flowMode = Both
//	}
	
	#region Interfaces
	public class Resource<T>
	{
		private PartResource res;
		private ProtoPartResourceSnapshot pres;
		private bool is_resource = typeof(T).FullName == typeof(PartResource).FullName; //KSP mono microlib does not support Type.Equal
		private bool is_proto    = typeof(T).FullName == typeof(ProtoPartResourceSnapshot).FullName;
		
		public Resource(T res)
		{
			if(res == null) throw new NullReferenceException("Resource<T>: res cannot be null");
			if(!(is_resource || is_proto)) 
				throw new NotSupportedException("Resource<T>: T should be either " +
												"PartResource or ProtoPartResourceSnapshot");
			if(is_resource)	this.res = (PartResource)(object)res;
			else this.pres = (ProtoPartResourceSnapshot)(object)res;
		}
		
		public string resourceName 
		{ 
			get 
			{ 
				if(is_resource) return res.resourceName; 
				else return pres.resourceName;
			} 
		}
		
		public double amount
		{
			get
			{
				if(is_resource) return res.amount;
				else return double.Parse(pres.resourceValues.GetValue("amount"));
			}
			set
			{
				if(is_resource) res.amount = value;
				else pres.resourceValues.SetValue("amount", value.ToString());
			}
		}
		
		public double maxAmount
		{
			get
			{
				if(is_resource) return res.maxAmount;
				else return double.Parse(pres.resourceValues.GetValue("maxAmount"));
			}
		}
		
		public bool flowState
		{
			get
			{
				if(is_resource) return res.flowState;
				else return bool.Parse(pres.resourceValues.GetValue("flowState"));
			}
		}
		
		public bool isTweakable
		{
			get
			{
				if(is_resource) return res.isTweakable;
				else return bool.Parse(pres.resourceValues.GetValue("isTweakable"));
			}
		}
		
		public bool hideFlow
		{
			get
			{
				if(is_resource) return res.hideFlow;
				else return bool.Parse(pres.resourceValues.GetValue("hideFlow"));
			}
		}
		
		public PartResource.FlowMode flowMode
		{
			get
			{
				if(is_resource) return res.flowMode;
				else return (PartResource.FlowMode)Enum.Parse(typeof(PartResource.FlowMode), 
				                                              pres.resourceValues.GetValue("flowMode"));
			}
		}
	}
	
	
	public class Part<T>
	{
		private Part part;
		private ProtoPartSnapshot ppart;
		private bool is_part  = typeof(T).FullName == typeof(Part).FullName;
		private bool is_proto = typeof(T).FullName == typeof(ProtoPartSnapshot).FullName;
		
		public Part(T part)
		{
			if(part == null) throw new NullReferenceException("Part<T>: part cannot be null");
			if(!(is_part || is_proto)) 
				throw new NotSupportedException("Part<T>: T should be either " +
												"Part or ProtoPartSnapshot");
			if(is_part)	this.part = (Part)(object)part;
			else this.ppart = (ProtoPartSnapshot)(object)part;
		}
		
		public List<Resource<R>> Resources<R>() 
		{ 
			List<Resource<R>> Rs = new List<Resource<R>>();
			if(is_part)
			{
				foreach(PartResource pr in part.Resources) 
					Rs.Add(new Resource<R>((R)(object)pr));
				return Rs;
			}
			else
			{
				foreach(ProtoPartResourceSnapshot pr in ppart.resources) 
					Rs.Add(new Resource<R>((R)(object)pr));
				return Rs;
			}	
		}
		
	}
	
	
	public class Vessel<T>
	{
		private Vessel vessel;
		private ProtoVessel pvessel;
		private bool is_vessel = typeof(T).FullName == typeof(Vessel).FullName;
		private bool is_proto  = typeof(T).FullName == typeof(ProtoVessel).FullName;
		
		public Vessel(T vessel)
		{
			if(vessel == null) throw new NullReferenceException("Vessel<T>: vessel cannot be null");
			if(!(is_vessel || is_proto)) 
				throw new NotSupportedException("Vessel<T>: T should be either " +
												"Vessel or ProtoVessel");
			if(is_vessel)
				this.vessel = (Vessel)(object)vessel;
			else this.pvessel = (ProtoVessel)(object)vessel;
		}
		
		public List<Part<P>> parts<P>() 
		{ 
			List<Part<P>> Ps = new List<Part<P>>();
			if(is_vessel)
			{
				foreach(Part p in vessel.parts) 
					Ps.Add(new Part<P>((P)(object)p));
				return Ps;
			}
			else
			{
				foreach(ProtoPartSnapshot p in pvessel.protoPartSnapshots) 
					Ps.Add(new Part<P>((P)(object)p));
				return Ps;
			}	
		}
		
	}
	#endregion
	
	
	#region VesselResources
	// Thanks to Taranis Elsu and his Fuel Balancer mod for the inspiration. (c) Taniwha
	public class ResourcePartMap<P, R>
	{
		public Resource<R> resource;
		public Part<P> part;

		public ResourcePartMap(Resource<R> resource, Part<P> part)
		{
			this.resource = resource;
			this.part = part;
		}
	}

	public class ResourceInfo<P, R>
	{ public List<ResourcePartMap<P, R>> parts = new List<ResourcePartMap<P, R>>(); }
	

	public class VesselResources<V, P, R> 
	{
		public Dictionary<string, ResourceInfo<P, R>> resources;
		public List<string> resourcesNames { get { return new List<string>(resources.Keys); } }
		
		
		private void AddPart(Part<P> part)
		{
			foreach (Resource<R> resource in part.Resources<R>()) 
			{
				ResourceInfo<P, R> resourceInfo;
				if (!resources.ContainsKey(resource.resourceName)) 
				{
					resourceInfo = new ResourceInfo<P, R>();
					resources[resource.resourceName] = resourceInfo;
				}
				resourceInfo = resources[resource.resourceName];
				resourceInfo.parts.Add (new ResourcePartMap<P, R>(resource, part));
			}
		}

		private void RemovePart(Part<P> part)
		{
			var remove_list = new List<string>();
			foreach(var resinfo in resources) 
			{
				string resource = resinfo.Key;
				ResourceInfo<P, R> resourceInfo = resinfo.Value;
				foreach(var pm in resourceInfo.parts) 
				{
					if (pm.part == part) 
					{
						resourceInfo.parts.Remove(pm);
						break;
					}
				}
				if (resourceInfo.parts.Count == 0)
					remove_list.Add (resource);
			}
			foreach (string resource in remove_list)
				resources.Remove(resource);
		}

		public VesselResources () { resources = new Dictionary<string, ResourceInfo<P, R>>();	}

		public VesselResources (P rootPart)
		{
			resources = new Dictionary<string, ResourceInfo<P, R>>();
			AddPart(new Part<P>(rootPart));
		}

		public VesselResources(V vessel)
		{
			resources = new Dictionary<string, ResourceInfo<P, R>>();
			Vessel<V> vsl = new Vessel<V>(vessel);
			foreach (Part<P> part in vsl.parts<P>()) AddPart(part);
		}

		// Completely empty the vessel of any and all resources.
		public void RemoveAllResources(HashSet<string> resources_to_remove = null)
		{
			foreach (KeyValuePair<string, ResourceInfo<P, R>> pair in resources) 
			{
				string resource = pair.Key;
				if(resources_to_remove != null && !resources_to_remove.Contains (resource)) 
					continue;
				ResourceInfo<P, R> resourceInfo = pair.Value;
				foreach (ResourcePartMap<P, R> partInfo in resourceInfo.parts)
					partInfo.resource.amount = 0.0;
			}
		}

		// Return the vessel's total capacity for the resource.
		// If the vessel has no such resource 0.0 is returned.
		public double ResourceCapacity (string resource)
		{
			if (!resources.ContainsKey(resource)) return 0.0;
			ResourceInfo<P, R> resourceInfo = resources[resource];
			double capacity = 0.0;
			foreach (ResourcePartMap<P, R> partInfo in resourceInfo.parts)
				capacity += partInfo.resource.maxAmount;
			return capacity;
		}

		// Return the vessel's total available amount of the resource.
		// If the vessel has no such resource 0.0 is returned.
		public double ResourceAmount (string resource)
		{
			if (!resources.ContainsKey (resource)) return 0.0;
			ResourceInfo<P, R> resourceInfo = resources[resource];
			double amount = 0.0;
			foreach (ResourcePartMap<P, R> partInfo in resourceInfo.parts)
				amount += partInfo.resource.amount;
			return amount;
		}

		// Transfer a resource into (positive amount) or out of (negative
		// amount) the vessel. No attempt is made to balance the resource
		// across parts: they are filled/emptied on a first-come-first-served
		// basis.
		// If the vessel has no such resource no action is taken.
		// Returns the amount of resource not transfered (0 = all has been
		// transfered).
		public double TransferResource(string resource, double amount)
		{
			if(!resources.ContainsKey(resource)) return amount;
			ResourceInfo<P, R> resourceInfo = resources[resource];
			foreach (ResourcePartMap<P, R> partInfo in resourceInfo.parts) 
			{
				double adjust = amount;
				Resource<R> res = partInfo.resource;
				if (adjust < 0  && -adjust > res.amount)
					// Ensure the resource amount never goes negative
					adjust = -res.amount;
				else if (adjust > 0 &&
						 adjust > (res.maxAmount - res.amount))
					// ensure the resource amount never excees the maximum
					adjust = res.maxAmount - res.amount;
				partInfo.resource.amount += adjust;
				amount -= adjust;
			}
			return amount;
		}
	}
	#endregion
	
	public class ResourceManifest
	{
		public string name;
		public double pool;
		public double amount;
		public double offset;
		public double minAmount;
		public double maxAmount;
		public double capacity;
	}
}
