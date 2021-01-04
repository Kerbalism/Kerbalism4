using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

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
				wrapper = new PartResourceWrapper(Lib.AddResource(partData.LoadedPart, resName, amount, capacity));
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
					wrapper = new PartResourceWrapper(protoResource);
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


		public void Synchronize()
		{
			if (partData.IsLoaded)
			{
				int stockCount = partData.LoadedPart.Resources.Count;

				if (stockCount < Count)
				{
					for (int i = Count - 1; i > stockCount - 1; i--)
					{
						this[i].OnResourceRemoved?.Invoke(this[i].ResName);
						this[i].OnResourceRemoved = null;
						RemoveAt(i);
					}
				}

				for (int i = 0; i < Count; i++)
				{
					if (this[i].ResId != partData.LoadedPart.Resources[i].info.id)
					{
						this[i].OnResourceRemoved?.Invoke(this[i].ResName);
						this[i].OnResourceRemoved = null;
						this[i] = new PartResourceWrapper(partData.LoadedPart.Resources[i]);
					}
				}

				if (stockCount > Count)
				{
					for (int i = Count; i < stockCount; i++)
					{
						PartResourceWrapper wrapper = new PartResourceWrapper(partData.LoadedPart.Resources[i]);
						Add(wrapper);
					}
				}

				if (state == State.Unloaded)
				{
					state = State.Loaded;
					PartResourceCollection list = this;
					for (int i = 0; i < Count; i++)
					{
						this[i].Mutate(partData.LoadedPart.Resources[i]);
					}
				}
			}
			else
			{
				int stockCount = partData.ProtoPart.resources.Count;

				if (stockCount < Count)
				{
					for (int i = Count - 1; i > stockCount - 1; i--)
					{
						this[i].OnResourceRemoved?.Invoke(this[i].ResName);
						this[i].OnResourceRemoved = null;
						RemoveAt(i);
					}
				}

				for (int i = 0; i < Count; i++)
				{
					if (this[i].ResId != partData.ProtoPart.resources[i].definition.id)
					{
						this[i].OnResourceRemoved?.Invoke(this[i].ResName);
						this[i].OnResourceRemoved = null;
						this[i] = new PartResourceWrapper(partData.ProtoPart.resources[i]);
					}
				}

				if (stockCount > Count)
				{
					for (int i = Count; i < stockCount; i++)
					{
						PartResourceWrapper wrapper = new PartResourceWrapper(partData.ProtoPart.resources[i]);
						Add(wrapper);
					}
				}

				if (state == State.Loaded)
				{
					state = State.Unloaded;
					PartResourceCollection list = this;
					for (int i = 0; i < Count; i++)
					{
						this[i].Mutate(partData.ProtoPart.resources[i]);
					}
				}
			}
		}
	}
}
