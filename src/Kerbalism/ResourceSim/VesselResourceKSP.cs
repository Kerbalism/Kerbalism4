using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KERBALISM
{
	/// <summary>
	/// Handler for a single "real" resource on a vessel. Expose vessel-wide information about amounts, rates and brokers (consumers/producers).
	/// Responsible for synchronization between the resource simulator and the actual resources present on each part. 
	/// </summary>
	public sealed class VesselResourceKSP : VesselResource
	{
		public readonly PartResourceDefinition stockDefinition;

		/// <summary> Associated resource name</summary>
		public override string Name => stockDefinition.name;

		/// <summary> Shortcut to the resource definition "displayName" </summary>
		public override string Title => stockDefinition.displayName;

		/// <summary> Shortcut to the resource definition "isVisible" </summary>
		public override bool Visible => stockDefinition.isVisible;

		/// <summary> Amount of resource</summary>
		public override double Amount => resourceWrapper.amount;

		/// <summary> Storage capacity of resource</summary>
		public override double Capacity => resourceWrapper.capacity;

		public override bool NeedUpdate => deferred != 0.0 || resourceWrapper.NeedUpdate;

		/// <summary> Shortcut to the resource definition "abbreviation" </summary>
		public string Abbreviation => stockDefinition.abbreviation;

		/// <summary> Shortcut to the resource definition "density" </summary>
		public float Density => stockDefinition.density;

		/// <summary> Shortcut to the resource definition "unitCost" </summary>
		public float UnitCost => stockDefinition.unitCost;

		private readonly UnknownIO unknownIO;

		public double UnknownBrokersRate { get; private set; }

		/// <summary>Ctor</summary>
		public VesselResourceKSP(PartResourceDefinition stockDefinition, PartResourceWrapperCollection wrapper)
			: base(stockDefinition.id)
		{
			this.stockDefinition = stockDefinition;
			this.resourceWrapper = wrapper;

			unknownIO = new UnknownIO(this);

			Init();
		}

		public void SetWrapper(PartResourceWrapperCollection resourceWrapper)
		{
			this.resourceWrapper = resourceWrapper;
		}

		protected override void OnExecuteAndSyncToParts(VesselResHandler resHandler, double elapsedSec, bool checkCoherency)
		{
			// As we haven't yet synchronized anything, changes to amount can only come from non-Kerbalism producers or consumers
			double unknownChange = resourceWrapper.amount - resourceWrapper.oldAmount;

			// Avoid false detection due to precision errors
			if (Math.Abs(unknownChange) < 1e-05) unknownChange = 0.0;
			UnknownBrokersRate = unknownChange / elapsedSec;
			
			// detect flow state changes
			bool flowStateChanged = resourceWrapper.capacity - resourceWrapper.oldCapacity > 1e-05;

			resourceWrapper.SyncToPartResources(deferred, equalizeMode == EqualizeMode.Enabled);

			equalizeMode = EqualizeMode.NotSet;

			// reset deferred production/consumption
			deferred = 0.0;

			// recalculate level
			level = resourceWrapper.capacity > 0.0 ? resourceWrapper.amount / resourceWrapper.capacity : 0.0;

			// calculate rate of change per-second
			// - don't update rate during warp blending (stock modules have instabilities during warp blending) 
			// - ignore interval-based rules consumption/production
			rate = (resourceWrapper.amount - resourceWrapper.oldAmount) / elapsedSec;

			if (UnknownBrokersRate > 0.0)
			{
				unknownIO.requestedRate = UnknownBrokersRate;
				executedIOList.Add(unknownIO);
			}

			// if incoherent producers are detected, do not allow high timewarp speed
			// - can be disabled in settings
			// - ignore incoherent consumers (no negative consequences for player)
			// - ignore flow state changes (avoid issue with process controllers and other things that alter resource capacities)
			if (checkCoherency && !flowStateChanged && UnknownBrokersRate / Capacity > 0.001)
			{
				CoherencyWarning(resHandler.VesselName, Title);
			}
		}

		private void CoherencyWarning(string vesselName, string resourceName)
		{
			Message.Post
			(
				Severity.warning,
				$"On <b>{vesselName}</b>\na producer of <b>{resourceName}</b> has\n incoherent behavior at high warp speeds.",
				"<i>You are likely using a mod that isn't supported by Kerbalism</i>"
			);
			Lib.StopWarp(5);
		}
	}
}
