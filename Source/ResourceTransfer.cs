using System;
using System.Collections.Generic;

namespace AtHangar 
{
	public class ResourceProxy : ProtoPartResourceSnapshot
	{
		protected ConfigNode valuesRef;
		protected ProtoPartResourceSnapshot protoRef;

		static ConfigNode resource_values(ProtoPartResourceSnapshot res)
		{
			var node = new ConfigNode("RESOURCE");
			res.Save(node);
			return node;
		}

		public ResourceProxy(PartResource res) : base(res) {}

		public ResourceProxy(ProtoPartResourceSnapshot res)
			: base(resource_values(res))
		{ 
			if(res.resourceRef != null)
				resourceRef = res.resourceRef;
			protoRef = res;
		}

		public ResourceProxy(ConfigNode node_ref)
			: base(node_ref)
		{
			valuesRef = node_ref;
		}

		public void Sync()
		{
			if(resourceRef != null)
			{
				resourceRef.amount = amount;
				resourceRef.maxAmount = maxAmount;
				resourceRef.flowState = flowState;
			}
			if(protoRef != null)
			{
				protoRef.amount = amount;
				protoRef.maxAmount = maxAmount;
				protoRef.flowState = flowState;
			}
			if(valuesRef != null)
				Save(valuesRef);
		}
	}

	public class PartProxy : Dictionary<string, ResourceProxy>
	{

		public PartProxy(Part part)
		{
			foreach(var res in part.Resources)
				Add(res.resourceName, new ResourceProxy(res));
		}

		public PartProxy(ProtoPartSnapshot proto_part)
		{
			foreach(var res in proto_part.resources)
				Add(res.resourceName, new ResourceProxy(res));
		}

		public PartProxy(ConfigNode part_node)
		{
			foreach(var res in part_node.GetNodes("RESOURCE"))
			{
				var proxy = new ResourceProxy(res);
				Add(proxy.resourceName, proxy);
			}
		}
	}

	public class VesselResources
	{
		public readonly List<PartProxy> Parts = new List<PartProxy>();
		public readonly Dictionary<string, List<PartProxy>> Resources = new Dictionary<string, List<PartProxy>>();
		public List<string> resourcesNames { get { return new List<string>(Resources.Keys); } }

		void add_part_proxy(PartProxy proxy)
		{
			Parts.Add(proxy);
			foreach(var res in proxy)
			{
				List<PartProxy> res_parts;
				if(!Resources.TryGetValue(res.Key, out res_parts))
				{
					res_parts = new List<PartProxy>();
					Resources.Add(res.Key, res_parts);
				}
				res_parts.Add(proxy);
			}
		}

		public VesselResources(IShipconstruct vessel)
		{ vessel.Parts.ForEach(p => add_part_proxy(new PartProxy(p))); }

		public VesselResources(ProtoVessel proto_vessel)
		{ proto_vessel.protoPartSnapshots.ForEach(p => add_part_proxy(new PartProxy(p))); }

		public VesselResources(ConfigNode vessel_node)
		{ 
			foreach(var part in vessel_node.GetNodes("PART"))
				add_part_proxy(new PartProxy(part));
		}

		/// <summary>
		/// Return the vessel's total capacity for the resource.
		/// If the vessel has no such resource 0.0 is returned.
		/// </summary>
		/// <returns>Total resource capacity.</returns>
		/// <param name="resource">Resource name.</param>
		public double ResourceCapacity(string resource)
		{
			if(!Resources.ContainsKey(resource)) return 0.0;
			double capacity = 0;
			Resources[resource].ForEach(p => capacity += p[resource].maxAmount);
			return capacity;
		}

		/// <summary>
		/// Return the vessel's total available amount of the resource.
		/// If the vessel has no such resource 0.0 is returned.
		/// </summary>
		/// <returns>Total resource amount.</returns>
		/// <param name="resource">Resource name.</param>
		public double ResourceAmount(string resource)
		{
			if(!Resources.ContainsKey(resource)) return 0.0;
			double amount = 0;
			Resources[resource].ForEach(p => amount += p[resource].amount);
			return amount;
		}

		/// <summary>
		/// Transfer a resource into (positive amount) or out of (negative
		/// amount) the vessel. No attempt is made to balance the resource
		/// across parts: they are filled/emptied on a first-come-first-served
		/// basis.
		/// If the vessel has no such resource no action is taken.
		/// Returns the amount of resource not transfered (0 = all has been
		/// transfered).
		/// Based on the code from Extraplanetary Launchpads plugin. Resources.cs module.
		/// </summary>
		/// <returns>The resource.</returns>
		/// <param name="resource">Resource.</param>
		/// <param name="amount">Amount.</param>
		public double TransferResource(string resource, double amount)
		{
			if(!Resources.ContainsKey(resource)) return 0.0;
			foreach(var part in Resources[resource]) 
			{
				var adjust = amount;
				var res = part[resource];
				if(adjust < 0  && -adjust > res.amount)
					// Ensure the resource amount never goes negative
					adjust = -res.amount;
				else if(adjust > 0 &&
				        adjust > (res.maxAmount - res.amount))
					// ensure the resource amount never excees the maximum
					adjust = res.maxAmount - res.amount;
				res.amount += adjust;
				res.Sync();
				amount -= adjust;
			}
			return amount;
		}
	}
	
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
