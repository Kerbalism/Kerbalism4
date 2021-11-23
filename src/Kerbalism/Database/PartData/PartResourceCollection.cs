using System.Collections.Generic;

namespace KERBALISM
{
	public class PartResourceCollection : List<PartResourceWrapper>
	{
		private enum State { Loaded, Unloaded }
		private PartData partData;
		private State state;

		public PartResourceCollection(PartData partData)
		{
			this.partData = partData;
			state = partData.IsLoaded ? State.Loaded : State.Unloaded;
			Synchronize();
		}

		public bool TryGetResourceWrapper(string resName, out PartResourceWrapper resWrapper)
		{
			foreach (PartResourceWrapper wrapper in this)
			{
				if (wrapper.ResName == resName)
				{
					resWrapper = wrapper;
					return true;
				}
			}
			resWrapper = null;
			return false;
		}

		public bool TryGetResourceWrapper(int resId, out PartResourceWrapper resWrapper)
		{
			foreach (PartResourceWrapper wrapper in this)
			{
				if (wrapper.resId == resId)
				{
					resWrapper = wrapper;
					return true;
				}
			}
			resWrapper = null;
			return false;
		}

		public PartResourceWrapper GetResourceWrapper(string resName)
		{
			foreach (PartResourceWrapper wrapper in this)
			{
				if (wrapper.ResName == resName)
				{
					return wrapper;
				}
			}

			return null;
		}

		public PartResourceWrapper GetResourceWrapper(int resId)
		{
			foreach (PartResourceWrapper wrapper in this)
			{
				if (wrapper.resId == resId)
				{
					return wrapper;
				}
			}

			return null;
		}

		/// <summary>
		/// Add a resource to the part. Note : check first if the resource exists already !
		/// Could do a "merge" here but this is usually something you want to check in the calling code.
		/// </summary>
		public PartResourceWrapper AddResource(string resName, double amount, double capacity)
		{
			if (!PartResourceLibrary.Instance.resourceDefinitions.Contains(resName))
			{
				Lib.Log($"Failed to add resource {resName} to part {partData} : the resource doesn't exists", Lib.LogLevel.Warning);
				return null;
			}

			PartResourceWrapper wrapper;

			if (partData.IsLoaded)
			{
				wrapper = new PartResourceWrapper(partData, Lib.AddResource(partData.LoadedPart, resName, amount, capacity));
				Add(wrapper);
			}
			else
			{
				wrapper = partData.resources.Find(p => p.ResName == resName);
				if (wrapper == null)
				{
					ConfigNode resNode = new ConfigNode();
					resNode.AddValue("name", resName);
					resNode.AddValue("amount", amount);
					resNode.AddValue("maxAmount", capacity);
					resNode.AddValue("flowState", true);
					ProtoPartResourceSnapshot protoResource = new ProtoPartResourceSnapshot(resNode);
					partData.ProtoPart.resources.Add(protoResource);
					wrapper = new PartResourceWrapper(partData, protoResource);
					Add(wrapper);
				}
				else
				{
					wrapper.Amount = amount;
					wrapper.Capacity = capacity;
				}
			}

			return wrapper;
		}

		/// <summary>
		/// Remve a resource from the part.
		/// </summary>
		public void RemoveResource(string resName)
		{
			PartResourceDefinition resDefinition = PartResourceLibrary.Instance.GetDefinition(resName);
			if (resDefinition == null)
			{
				Lib.Log($"Failed to remove resource {resName} to part {partData} : the resource doesn't exists", Lib.LogLevel.Warning);
				return;
			}

			if (partData.IsLoaded)
			{
				partData.LoadedPart.Resources.dict.Remove(resDefinition.id);
				partData.LoadedPart.SimulationResources?.dict.Remove(resDefinition.id);
				GameEvents.onPartResourceListChange.Fire(partData.LoadedPart);
			}
			else
			{
				partData.ProtoPart.resources.RemoveAll(p => p.resourceName == resName);
			}

			RemoveAll(p => p.ResName == resName);
		}

		// TODO : resource sync is a major performance hog. We should get ride of the pooling based sync logic and rely on events.
		//   - Loaded has GameEvents.onPartResourceListChange, and we can implement our own for unloaded
		//   - Loaded/unloaded state changes can also be made event based, this is trivial to do
		// Also, maybe we should look at how to better inline the amount/capacity getters/setters.
		// Currently every call has a 3 layer deep call stack (ksm wrapper -> stock wrapper -> stock resource)
		// The middle wrapper exists so we can mutate the top wrapper without having to reacquire a reference to it
		// when changing between the loaded/unloaded states. It's quite convenient, but maybe we could implement an event based thing here
		// too. Currently, uses case of wrappers references are :
		// - vessel resource holders
		// - modules
		public void Synchronize()
		{
			if (partData.IsLoaded)
			{
				PartResourceList stockResources = partData.LoadedPart.Resources;
				int stockCount = stockResources.Count;

				if (stockCount < Count)
				{
					for (int i = Count - 1; i > stockCount - 1; i--)
					{
						this[i].RemoveFromResHandler(partData);
						//this[i].OnResourceRemoved?.Invoke(this[i].ResName);
						//this[i].OnResourceRemoved = null;
						RemoveAt(i);
					}
				}

				for (int i = 0; i < Count; i++)
				{
					if (this[i].resId != stockResources[i].info.id)
					{
						this[i].RemoveFromResHandler(partData);
						//this[i].OnResourceRemoved?.Invoke(this[i].ResName);
						//this[i].OnResourceRemoved = null;
						this[i] = new PartResourceWrapper(partData, stockResources[i]);
					}
				}

				if (stockCount > Count)
				{
					for (int i = Count; i < stockCount; i++)
					{
						PartResourceWrapper wrapper = new PartResourceWrapper(partData, stockResources[i]);
						Add(wrapper);
					}
				}

				if (state == State.Unloaded)
				{
					state = State.Loaded;
					PartResourceCollection list = this;
					for (int i = 0; i < Count; i++)
					{
						this[i].Mutate(stockResources[i]);
					}
				}
			}
			else
			{
				List<ProtoPartResourceSnapshot> stockResources = partData.ProtoPart.resources;
				int stockCount = stockResources.Count;

				if (stockCount < Count)
				{
					for (int i = Count - 1; i > stockCount - 1; i--)
					{
						this[i].RemoveFromResHandler(partData);
						//this[i].OnResourceRemoved?.Invoke(this[i].ResName);
						//this[i].OnResourceRemoved = null;
						RemoveAt(i);
					}
				}

				for (int i = 0; i < Count; i++)
				{
					if (this[i].resId != stockResources[i].definition.id)
					{
						this[i].RemoveFromResHandler(partData);
						//this[i].OnResourceRemoved?.Invoke(this[i].ResName);
						//this[i].OnResourceRemoved = null;
						this[i] = new PartResourceWrapper(partData, stockResources[i]);
					}
				}

				if (stockCount > Count)
				{
					for (int i = Count; i < stockCount; i++)
					{
						PartResourceWrapper wrapper = new PartResourceWrapper(partData, stockResources[i]);
						Add(wrapper);
					}
				}

				if (state == State.Loaded)
				{
					state = State.Unloaded;
					PartResourceCollection list = this;
					for (int i = 0; i < Count; i++)
					{
						this[i].Mutate(stockResources[i]);
					}
				}
			}
		}
	}
}
