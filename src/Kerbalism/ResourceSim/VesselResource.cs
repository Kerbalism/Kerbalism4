using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace KERBALISM
{
	/// <summary>
	/// Interface for common interactions with VesselResource and VirtualResource.
	/// You can cast this to VesselResource to get the extra information it contains (rates...)
	/// </summary>
	public abstract class VesselResource
	{
		private static StringBuilder sb = new StringBuilder();

		/// <summary> Unique id </summary>
		public readonly int id;

		/// <summary> Technical name</summary>
		public abstract string Name { get; }

		/// <summary> UI friendly name</summary>
		public abstract string Title { get; }

		/// <summary> Visibility of resource</summary>
		public abstract bool Visible { get; }

		/// <summary> Amount of resource</summary>
		public abstract double Amount { get; }

		/// <summary> Storage capacity of resource</summary>
		public abstract double Capacity { get; }

		public abstract bool NeedUpdate { get; }

		/// <summary> Rate of change in amount per-second, this is purely for visualization</summary>
		public double Rate => rate;
		protected double rate;

		/// <summary> Amount vs capacity, or 0 if there is no capacity</summary>
		public double Level => level;
		protected double level;

		/// <summary> If enabled, the total resource amount will be redistributed evenly amongst all parts. Reset itself to "NotSet" after every ExecuteAndSyncToParts() call</summary>
		public EqualizeMode equalizeMode = EqualizeMode.NotSet;
		public enum EqualizeMode { NotSet, Enabled, Disabled }

		/// <summary> Not yet consumed or produced amount that will be synchronized to the vessel parts in Sync()</summary>
		public virtual double Deferred => deferred;
		protected double deferred;

		public PartResourceWrapperCollection ResourceWrapper => resourceWrapper;
		protected PartResourceWrapperCollection resourceWrapper;

		public bool IsSupply => Supply != null;
		public Supply Supply { get; private set; }

		protected VesselResHandler lastHandler;
		protected readonly List<RecipeIO> executedIOList = new List<RecipeIO>();
		protected readonly List<CategorizedIOList> categorizedIOList = new List<CategorizedIOList>();
		protected bool ioAreCategorized;

		public List<RecipeIO> GetExecutedIO()
		{
			if (lastHandler == null)
			{
				executedIOList.Clear();
				return executedIOList;
			}

			if (!lastHandler.ExecutedRecipesAreParsed)
			{
				lastHandler.ParseExecutedRecipes();
			}

			return executedIOList;
		}

		public List<CategorizedIOList> GetCategorizedIO()
		{
			if (!ioAreCategorized)
			{
				CategorizedIOList.Categorize(GetExecutedIO(), categorizedIOList);
				ioAreCategorized = true;
			}

			return categorizedIOList;
		}

		public void AddExecutedIO(RecipeIO io) => executedIOList.Add(io);

		protected VesselResource(int id)
		{
			this.id = id;
		}

		/// <summary> Called at the VesselResHandler instantiation, after the ResourceWrapper amount and capacity has been evaluated </summary>
		protected virtual void Init()
		{
			deferred = 0.0;

			// calculate level
			level = resourceWrapper.capacity > 0.0 ? resourceWrapper.amount / resourceWrapper.capacity : 0.0;

			Supply = Supply.GetSupply(id);
		}

		public void SetResourceWrapper(PartResourceWrapperCollection resourceWrapper)
		{
			this.resourceWrapper = resourceWrapper;
		}

		/// <summary> Called by the VesselResHandler, every update :
		/// <para/> - After Recipes have been processed
		/// <para/> - After the VesselResHandler has been updated with all part resources references, and after amount/oldAmount and capacity/oldCapacity have been set
		/// </summary>
		public void ExecuteAndSyncToParts(VesselResHandler resHandler, double elapsedSec, bool checkCoherency)
		{
			ioAreCategorized = false;
			executedIOList.Clear();
			lastHandler = resHandler;

			if (NeedUpdate)
			{
				OnExecuteAndSyncToParts(resHandler, elapsedSec, checkCoherency);
			}
			else
			{
				rate = 0.0;
			}
		}

		protected abstract void OnExecuteAndSyncToParts(VesselResHandler resHandler, double elapsedSec, bool checkCoherency);

		public virtual void EditorFinalize()
		{
			level = resourceWrapper.capacity > 0.0 ? resourceWrapper.amount / resourceWrapper.capacity : 0.0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal void Consume(double quantity)
		{
			deferred -= quantity;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal void Produce(double quantity)
		{
			deferred += quantity;
		}

		/// <summary>estimate time until depletion</summary>
		public double Depletion => Amount <= 1e-10 ? 0.0 : Rate >= -1e-10 ? double.PositiveInfinity : Amount / -Rate;

		public string DepletionInfo => Amount <= 1e-10 ? Local.Monitor_depleted : Lib.HumanReadableDuration(Depletion);

		public override string ToString()
		{
			return $"{Name} : {Amount}/{Capacity} ({Rate:+0.#######/s;-0.#######/s})";
		}

		public KsmString BrokerListTooltip(KsmString ks, bool showSummary = true)
		{
			ks.Format(Title, KF.KolorYellow, KF.Bold);
			
			if (showSummary)
			{
				ks.Break();

				//if (AvailabilityFactor < 1.0)
				//{
				//	ks.Info("Availability", AvailabilityFactor.ToString("P1"), KF.Color(criticalConsumptionSatisfied ? Kolor.Yellow : Kolor.Red), KF.Bold);
				//}
				//else
				//{
					
				//}

				ks.Info("Depletion", DepletionInfo);

				ks.AlignLeft();

				if (Rate != 0.0)
				{
					ks.Format(KF.ReadableRate(Rate), KF.Color(Rate > 0.0 ? Kolor.PosRate : Kolor.NegRate), KF.Bold);
				}
				else
				{
					ks.Format(Local.TELEMETRY_nochange, KF.Bold);
				}

				ks.Format(KF.Concat(Lib.HumanReadableStorage(Amount, Capacity), " (", Level.ToString("P0"), ")"), KF.Position(80));
			}
			else
			{
				ks.AlignLeft();
			}

			List<CategorizedIOList> categorizedIO = GetCategorizedIO();

			if (categorizedIO.Count > 0)
			{
				if (showSummary)
				{
					ks.Add("\n<b>------------<pos=65px>------------</b>");
				}

				foreach (CategorizedIOList category in categorizedIO)
				{
					// exclude very tiny rates to avoid the ui flickering
					//if (category.totalRate > -1e-09 && category.totalRate < 1e-09)
					//	continue;

					ks.Break();
					ks.Format(category.category.title, KF.Underline);
					foreach (RecipeIO io in category.recipes)
					{
						ks.Break();
						ks.Format(KF.ReadableRate(io.SignedExecutedRate), KF.Color(io.SignedExecutedRate > 0.0 ? Kolor.PosRate : Kolor.NegRate));
						ks.Format(io.Title, KF.Position(65));
					}


				}
			}

			return ks;
		}

		public string BrokerListTooltip(bool showSummary = true)
		{
			KsmString ks = KsmString.Get;
			BrokerListTooltip(ks, showSummary);
			return ks.GetStringAndRelease();
		}
	}
}
