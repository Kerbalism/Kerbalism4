using System;
using System.Collections.Generic;

namespace KERBALISM
{
	/// <summary>
	/// VesselResourceAbstract is a vessel wide, non persisted resource that behave like any other resource in regard to recipes<br/>
	/// Its amount and capacity isn't held in any part, and must be set directy using the relevant methods.<br/>
	/// Also note that the amount/capacity isn't carried over on vessel merge/split events, so this must be handled manually.
	/// </summary>
	public sealed class VesselResourceAbstract : VesselResource
	{
		public override string Name => name;
		public string name = string.Empty;

		public override string Title => title;
		public string title = string.Empty;

		public override bool Visible => visible;
		public bool visible = false;

		/// <summary> Amount of virtual resource. This can be set directly if needed.</summary>
		public override double Amount => amount;
		protected double amount;

		public override bool NeedUpdate => true;

		/// <summary>
		/// Storage capacity of the virtual resource. Will default to double.MaxValue unless explicitely defined
		/// <para/>Note that a virtual resource used as output in a Recipe will follow the same rules as a regular resource regarding the "dump" behvior specified in the Recipe.
		/// </summary>
		public override double Capacity => capacity;
		protected double capacity;

		public void SetAmount(double amount)
		{
			if (Lib.IsNegativeOrNaN(amount))
				this.amount = 0.0;
			else if (amount > capacity)
				this.amount = capacity;
			else
				this.amount = amount;

			level = capacity > 0.0 ? this.amount / capacity : 0.0;
		}

		public void SetCapacity(double capacity)
		{
			if (Lib.IsNegativeOrNaN(capacity))
				this.capacity = 0.0;
			else
				this.capacity = capacity;

			if (amount > capacity)
				amount = capacity;

			level = this.capacity > 0.0 ? amount / this.capacity : 0.0;
		}

		public void SetAmountAndCapacity(double amount, double capacity)
		{
			if (Lib.IsZeroOrNegativeOrNaN(capacity))
			{
				this.capacity = 0.0;
				this.amount = 0.0;
			}
			else
			{
				this.capacity = capacity;
				if (Lib.IsNegativeOrNaN(amount))
					this.amount = 0.0;
				else if (amount > capacity)
					this.amount = capacity;
				else
					this.amount = amount;
			}

			level = this.capacity > 0.0 ? this.amount / this.capacity : 0.0;
		}

		public void SetAmountAndCapacity(double amountAndCapacity)
		{
			if (Lib.IsNegativeOrNaN(amountAndCapacity))
			{
				capacity = 0.0;
				amount = 0.0;
			}
			else
			{
				capacity = amountAndCapacity;
				amount = amountAndCapacity;
			}

			level = capacity > 0.0 ? amount / capacity : 0.0;
		}

		/// <summary>Don't use this to create a virtual resource, use the VesselResHandler.CreateVirtualResource() method</summary>
		public VesselResourceAbstract(int id) : base(id)
		{
			capacity = double.MaxValue;
		}

		protected override void OnExecuteAndSyncToParts(VesselResHandler resHandler, double elapsedSec, bool checkCoherency)
		{
			double newAmount = Lib.Clamp(amount + deferred, 0.0, capacity);
			deferred = 0.0;

			// note : VesselResources return zero Rate when there is no actual change in amount, so we try to be consistent
			// and reproduce the same logic here
			rate = (newAmount - amount) / elapsedSec;
			amount = newAmount;
			level = Capacity > 0.0 ? amount / capacity : 0.0;
		}

 		public override void EditorFinalize()
		{
			level = capacity > 0.0 ? amount / capacity : 0.0;
		}
	}
}
