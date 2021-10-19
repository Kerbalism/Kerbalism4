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
		protected struct LocalDeferredRequest
		{
			public PartResourceWrapper localWrapper;
			public double deferred;

			public LocalDeferredRequest(PartResourceWrapper localWrapper, double deferred)
			{
				this.localWrapper = localWrapper;
				this.deferred = deferred;
			}
		}


		protected List<PartResourceWrapper> partResources = new List<PartResourceWrapper>();

		IEnumerator<PartResourceWrapper> IEnumerable<PartResourceWrapper>.GetEnumerator() => partResources.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => partResources.GetEnumerator();
		List<PartResourceWrapper>.Enumerator GetEnumerator() => partResources.GetEnumerator();

		/// <summary> current amount </summary>
		public double amount = 0.0;

		/// <summary> current capacity </summary>
		public double capacity = 0.0;

		/// <summary> remember vessel-wide amount of previous step, to calculate rate and detect non-Kerbalism brokers </summary>
		public double oldAmount = 0.0;

		/// <summary> remember vessel-wide capacity of previous step, to detect flow state changes </summary>
		public double oldCapacity = 0.0;

		protected List<LocalDeferredRequest> localDeferredRequests = new List<LocalDeferredRequest>();

		public void AddLocalDeferredRequest(PartResourceWrapper localWrapper, double amount)
		{
			localDeferredRequests.Add(new LocalDeferredRequest(localWrapper, amount));
		}

		public bool NeedUpdate => amount != oldAmount || capacity != oldCapacity || localDeferredRequests.Count != 0;

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
				//partResources.Clear();
			}

			localDeferredRequests.Clear();
		}

		/// <summary> synchronize deferred to the PartResource / ProtoPartResourceSnapshot references</summary>
		/// <param name="deferred">amount to add or remove</param>
		/// <param name="equalizeMode">if true, the total amount (current + deffered) will redistributed equally amongst all parts </param>
		public abstract void SyncToPartResources(double deferred, bool equalizeMode);

		public virtual void SyncFromPartResources(bool resetCurrent = true, bool updateOld = true)
		{
			ClearPartResources(resetCurrent, updateOld);

			foreach (PartResourceWrapper partResourceWrapper in partResources)
			{
				if (!partResourceWrapper.FlowState)
					continue;

				// avoid possible NaN creation
				if (partResourceWrapper.Capacity <= 0.0)
					continue;

				amount += partResourceWrapper.Amount;
				capacity += partResourceWrapper.Capacity;
			}
		}


		public virtual void SyncFromPartResources(PartResourceWrapper partResource)
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
			partResources = otherWrapper.partResources;
		}

		public void AddPartWrapper(PartResourceWrapper partResourceWrapper)
		{
			partResources.Add(partResourceWrapper);
		}

		public void RemovePartWrapper(PartResourceWrapper partResourceWrapper)
		{
			partResources.Remove(partResourceWrapper);
		}

		public override string ToString()
		{
			return $"{amount:F1} / {capacity:F1}";
		}
	}

	/// <summary>
	/// EditorResourceWrapper doesn't keep the PartResourceWrapper references and doesn't synchronize anything
	/// </summary>
	public sealed class PlannerPartResourceWrapperCollection : PartResourceWrapperCollection
	{
		public override void SyncFromPartResources(PartResourceWrapper partResource)
		{
			// avoid possible NaN creation
			if (partResource.Capacity <= 0.0)
				return;

			amount += partResource.Amount;
			capacity += partResource.Capacity;
		}

		public override void SyncToPartResources(double deferred, bool equalizeMode) { }
	}

	public sealed class VesselPartResourceWrapperCollection : PartResourceWrapperCollection
	{
		public override void SyncToPartResources(double deferred, bool equalizeMode)
		{
			foreach (LocalDeferredRequest localRequest in localDeferredRequests)
			{
				double localDeffered = Lib.Clamp(localRequest.deferred, -localRequest.localWrapper.Amount, localRequest.localWrapper.Capacity - localRequest.localWrapper.Amount);
				localRequest.localWrapper.Amount += localDeffered;

				if (localRequest.localWrapper.FlowState)
				{
					deferred -= localDeffered;
					amount += localDeffered;
				}
			}

			// clamp consumption/production to vessel amount/capacity
			// - if deferred is negative, then amount is guaranteed to be greater than zero
			// - if deferred is positive, then capacity - amount is guaranteed to be greater than zero
			deferred = Lib.Clamp(deferred, -amount, capacity - amount);

			if (deferred == 0.0)
				return;

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
				if (deferred == 0.0)
					return;

				// apply deferred consumption/production to all parts, simulating ALL_VESSEL_BALANCED
				if (deferred < 0.0)
				{
					foreach (PartResourceWrapper partResource in partResources)
					{
						// calculate consumption coefficient for the part
						double k = partResource.Amount / amount;

						// apply deferred consumption
						partResource.Amount += deferred * k;
					}
				}
				else
				{
					foreach (PartResourceWrapper partResource in partResources)
					{
						// calculate production coefficient for the part
						double k = (partResource.Capacity - partResource.Amount) / (capacity - amount);

						// apply deferred production
						partResource.Amount += deferred * k;
					}
				}
			}

			// update amount, to get correct rate and levels at all times
			amount += deferred;
		}
	}
}
