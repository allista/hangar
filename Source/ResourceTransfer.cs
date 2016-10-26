//This code is based on code from Extraplanetary Launchpads plugin. Resources.cs module. 
using System;
using System.Collections.Generic;


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
		readonly PartResource res;
		readonly ProtoPartResourceSnapshot pres;
		readonly bool is_resource = typeof(T).FullName == typeof(PartResource).FullName; //KSP mono microlib does not support Type.Equal
		readonly bool is_proto = typeof(T).FullName == typeof(ProtoPartResourceSnapshot).FullName;
		
		public Resource(T res)
		{
			if(res == null) throw new NullReferenceException("Resource<T>: res cannot be null");
			if(!(is_resource || is_proto)) 
				throw new NotSupportedException("Resource<T>: T should be either " +
												"PartResource or ProtoPartResourceSnapshot");
			if(is_resource)	this.res = (PartResource)(object)res;
			else pres = (ProtoPartResourceSnapshot)(object)res;
		}
		
		public string resourceName 
		{ get { return is_resource ? res.resourceName : pres.resourceName; } }
		
		public double amount
		{
			get	{ return is_resource ? res.amount : pres.amount; }
			set 
			{ 
				if(is_resource) res.amount = value;
				else pres.amount = value; 
			}
		}
		
		public double maxAmount
		{ get {	return is_resource ? res.maxAmount : pres.maxAmount; } }
	}
	
	
	public class Part<T>
	{
		readonly Part part;
		readonly ProtoPartSnapshot ppart;
		readonly bool is_part  = typeof(T).FullName == typeof(Part).FullName;
		bool is_proto = typeof(T).FullName == typeof(ProtoPartSnapshot).FullName;
		
		public Part(T part)
		{
			if(part == null) throw new NullReferenceException("Part<T>: part cannot be null");
			if(!(is_part || is_proto)) 
				throw new NotSupportedException("Part<T>: T should be either " +
												"Part or ProtoPartSnapshot");
			if(is_part)	this.part = (Part)(object)part;
			else ppart = (ProtoPartSnapshot)(object)part;
		}
		
		public List<Resource<R>> Resources<R>() 
		{ 
			var Rs = new List<Resource<R>>();
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
	
	
	public class Vessel<T> where T : class
	{
		readonly IShipconstruct vessel;
		readonly ProtoVessel pvessel;
		readonly bool is_vessel;
		
		public Vessel(T vsl)
		{
			if(vsl == null) throw new NullReferenceException("Vessel<T>: vessel cannot be null");
			vessel = (IShipconstruct)(object)vsl;
			pvessel = (ProtoVessel)(object)vsl;
			if(vessel == null && pvessel == null)
				throw new NotSupportedException("Vessel<T>: T should be either " +
				                                "Vessel or ProtoVessel");
			is_vessel = vessel != null;
		}
		
		public List<Part<P>> Parts<P>() 
		{ 
			var Ps = new List<Part<P>>();
			if(is_vessel)
			{
				foreach(Part p in vessel.Parts) 
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
	

	public interface IVesselResources
	{
		double ResourceCapacity(string resource);
		double ResourceAmount(string resource);
		double TransferResource(string resource, double amount);
		void RemoveAllResources(HashSet<string> resources_to_remove = null);
		List<string> resourcesNames { get; }
	}

	public class VesselResources<V, P, R> : IVesselResources where V : class
	{
		public Dictionary<string, ResourceInfo<P, R>> resources;
		public List<string> resourcesNames { get { return new List<string>(resources.Keys); } }
		
		void AddPart(Part<P> part)
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

		void RemovePart(Part<P> part)
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
			var vsl = new Vessel<V>(vessel);
			foreach (Part<P> part in vsl.Parts<P>()) AddPart(part);
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
		public double capacity;
		public double offset;
		
		public double host_amount;
		public double host_capacity;
		
		public double minAmount;
		public double maxAmount;
		
	}
}
