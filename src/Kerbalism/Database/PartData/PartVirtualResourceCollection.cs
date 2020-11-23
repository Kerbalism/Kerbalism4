using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{


	public class PartVirtualResourceCollection : IEnumerable<VirtualPartResource>
	{
		private PartData partData;
		private List<VirtualPartResource> resources = new List<VirtualPartResource>();

		public IEnumerator<VirtualPartResource> GetEnumerator() => resources.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => resources.GetEnumerator();
		public bool Contains(string resourceName) => resources.Exists(p => p.ResName == resourceName);
		public int Count => resources.Count;

		public PartVirtualResourceCollection(PartData partData)
		{
			this.partData = partData;
		}

		public VirtualPartResource AddResource(string resourceName, double amount, double capacity, bool asSeparateContainer = false)
		{
			int containerIndex = 0;

			foreach (VirtualPartResource existingRes in resources)
			{
				if (existingRes.ResName == resourceName)
				{
					if (asSeparateContainer)
					{
						containerIndex = Math.Max(containerIndex, existingRes.ContainerIndex) + 1;
					}
					else
					{
						existingRes.Capacity = capacity;
						existingRes.Amount = amount;
						return existingRes;
					}
				}
			}

			return AddResource(resourceName, amount, capacity, containerIndex);
		}

		public VirtualPartResource AddResource(string resourceName, double amount, double capacity, int containerIndex)
		{
			VirtualResourceDefinition definition = partData.vesselData.ResHandler.AddVirtualPartResourceToHandler(resourceName);
			VirtualPartResource res = new VirtualPartResource(definition, containerIndex, amount, capacity);
			resources.Add(res);
			return res;
		}

		public VirtualPartResource GetResource(string resourceName)
		{
			return resources.Find(p => p.ResName == resourceName);
		}

		public VirtualPartResource GetResource(string resourceName, int containerIndex)
		{
			return resources.Find(p => p.ResName == resourceName && p.ContainerIndex == containerIndex);
		}

		/// <summary> remove all resources with the specified name and container index</summary>
		public void RemoveResource(string resourceName, int containerIndex)
		{
			resources.RemoveAll(p => p.ResName == resourceName && p.ContainerIndex == containerIndex);
		}

		/// <summary> remove all resources with the specified name, regardless of their container</summary>
		public void RemoveResource(string resourceName)
		{
			resources.RemoveAll(p => p.ResName == resourceName);
		}

		/// <summary> remove a resource</summary>
		public void RemoveResource(VirtualPartResource resource)
		{
			resources.RemoveAll(p => p == resource);
		}
	}
}
