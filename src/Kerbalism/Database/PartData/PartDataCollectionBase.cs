using System;
using System.Collections;
using System.Collections.Generic;

namespace KERBALISM
{
	public abstract class PartDataCollectionBase : IList<PartData>
	{
		public const string NODENAME_PARTS = "PARTS";

		public abstract List<PartData> Parts { get; }

		public int Count => Parts.Count;

		public bool IsReadOnly => false;

		PartData IList<PartData>.this[int index] { get => Parts[index]; set => Parts[index] = value; }

		public PartData this[int index] => Parts[index];

		public abstract PartData this[Part part] { get; }
		public abstract bool Contains(PartData data);
		public abstract bool Contains(Part part);
		public abstract bool TryGet(Part part, out PartData pd);

		// TODO : We really need to implement some caching for this
		public IEnumerable<T> AllModulesOfType<T>()
		{
			foreach (PartData partData in Parts)
			{
				for (int i = 0; i < partData.modules.Count; i++)
				{
					if (partData.modules[i] is T moduleData)
					{
						yield return moduleData;
					}
				}
			}
		}

		IEnumerator<PartData> IEnumerable<PartData>.GetEnumerator()
		{
			return Parts.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return Parts.GetEnumerator();
		}

		List<PartData>.Enumerator GetEnumerator()
		{
			return Parts.GetEnumerator();
		}

		public abstract void Save(ConfigNode VesselDataNode);
		public abstract void Load(ConfigNode VesselDataNode);

		public int IndexOf(PartData item)
		{
			return Parts.IndexOf(item);
		}

		public void Insert(int index, PartData item)
		{
			Parts.Insert(index, item);
		}

		public void RemoveAt(int index)
		{
			Parts.RemoveAt(index);
		}

		public void Add(PartData item)
		{
			Parts.Add(item);
		}

		public void Clear()
		{
			Parts.Clear();
		}

		public void CopyTo(PartData[] array, int arrayIndex)
		{
			Parts.CopyTo(array, arrayIndex);
		}

		public bool Remove(PartData item)
		{
			return Parts.Remove(item);
		}
	}


}
