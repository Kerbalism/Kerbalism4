using System;
using System.Collections;
using System.Collections.Generic;

namespace KERBALISM
{
	/// <summary>
	/// For a single resource type, keep track of amount, capacity and references to the individual PartResourceWrappers,
	/// which themselves are holding either a KSP PartResource/ProtoPartResource, or a PartVirtualResource
	/// This is 
	/// </summary>
	public abstract class PartResourceWrapperCollection : IEnumerable<PartResourceWrapper>
	{
		protected List<PartResourceWrapper> partResources = new List<PartResourceWrapper>();

		public IEnumerator<PartResourceWrapper> GetEnumerator() => partResources.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => partResources.GetEnumerator();

		/// <summary> current amount </summary>
		public double amount = 0.0;

		/// <summary> current capacity </summary>
		public double capacity = 0.0;

		/// <summary> remember vessel-wide amount of previous step, to calculate rate and detect non-Kerbalism brokers </summary>
		public double oldAmount = 0.0;

		/// <summary> remember vessel-wide capacity of previous step, to detect flow state changes </summary>
		public double oldCapacity = 0.0;

		/// <summary> To be called at the beginning of a simulation step :
		/// <para/>- save current amount/capacity in oldAmount/oldCapacity
		/// <para/>- reset amount/capacity
		/// <para/>- clear the stock PartResource / ProtoPartResourceSnapshot references</summary>
		/// <param name="doReset">if false, don't reset amount/capacity and don't clear references. For processing editor simulation steps</param>
		public virtual void ClearPartResources(bool resetCurrent = true, bool updateOld = true)
		{
			if (updateOld)
			{
				oldAmount = amount;
				oldCapacity = capacity;
			}

			if (resetCurrent)
			{
				amount = 0.0;
				capacity = 0.0;
				partResources.Clear();
			}
		}

		/// <summary> synchronize deferred to the PartResource / ProtoPartResourceSnapshot references</summary>
		/// <param name="deferred">amount to add or remove</param>
		/// <param name="equalizeMode">if true, the total amount (current + deffered) will redistributed equally amongst all parts </param>
		public virtual void SyncToPartResources(double deferred, bool equalizeMode) { }

		public virtual void AddPartResourceWrapper(PartResourceWrapper partResource)
		{
			// avoid possible NaN creation
			if (partResource.Capacity <= 0.0)
				return;

			partResources.Add(partResource);
			amount += partResource.Amount;
			capacity += partResource.Capacity;
		}

		public void SyncWithOtherWrapper(PartResourceWrapperCollection otherWrapper)
		{
			amount = otherWrapper.amount;
			capacity = otherWrapper.capacity;
			oldAmount = otherWrapper.oldAmount;
			oldCapacity = otherWrapper.oldCapacity;
		}

		public override string ToString()
		{
			return $"{amount:F1} / {capacity:F1}";
		}
	}

	/// <summary>
	/// EditorResourceWrapper doesn't keep the PartResourceWrapper references and doesn't synchronize anything
	/// </summary>
	public class PlannerPartResourceWrapperCollection : PartResourceWrapperCollection
	{
		public override void AddPartResourceWrapper(PartResourceWrapper partResource)
		{
			// avoid possible NaN creation
			if (partResource.Capacity <= 0.0)
				return;

			amount += partResource.Amount;
			capacity += partResource.Capacity;
		}

		public override void SyncToPartResources(double deferred, bool equalizeMode) { }
	}

	public class VesselPartResourceWrapperCollection : PartResourceWrapperCollection
	{
		public override void SyncToPartResources(double deferred, bool equalizeMode)
		{
			if (equalizeMode)
			{
				// apply deferred consumption/production to all parts,
				// equally balancing the total amount amongst all parts
				foreach (PartResourceWrapper partResource in partResources)
				{
					partResource.Amount = (amount + deferred) * (partResource.Capacity / capacity);
				}
			}
			else
			{
				// apply deferred consumption/production to all parts, simulating ALL_VESSEL_BALANCED
				// avoid very small values in deferred consumption/production
				if (Math.Abs(deferred) > 1e-16)
				{
					foreach (PartResourceWrapper partResource in partResources)
					{
						// calculate consumption/production coefficient for the part
						double k;
						if (deferred < 0.0)
							k = partResource.Amount / amount;
						else
							k = (partResource.Capacity - partResource.Amount) / (capacity - amount);

						// apply deferred consumption/production
						partResource.Amount += deferred * k;
					}
				}
			}
		}
	}
}
