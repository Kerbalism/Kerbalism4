using System;
using System.Runtime.CompilerServices;

namespace KERBALISM
{
	public abstract class RecipeIO
	{
		internal readonly int resourceId;
		internal readonly Recipe recipe;
		internal VesselResource vesselResource;
		protected double nominalRate;
		internal double requestedRate;
		protected double maxIOFactor;

		protected double amount;
		protected double invAmount;

		protected string title;


		internal RecipeIO(Recipe recipe, int resourceId, double nominalRate)
		{
			this.recipe = recipe;
			this.resourceId = resourceId;
			NominalRate = nominalRate;
		}

		public string Title
		{
			get => title == null ? recipe.title : title;
			set => title = value;
		}

		public override string ToString()
		{
			return $"{GetType().Name} : {vesselResource?.Name ?? resourceId.ToString()}, nominalRate={nominalRate}";
		}

		public double NominalRate
		{
			get => nominalRate;
			set
			{
				if (value.IsNegativeOrNaN())
				{
					Lib.LogStack($"{recipe.title} attempted to set an invalid I/O rate : `{value}`", Lib.LogLevel.Warning);
					value = 0.0;
				}

				nominalRate = value;
			}
		}

		internal void Prepare(VesselResHandler resHandler, double elapsedSec, double ioScale)
		{
			maxIOFactor = 1.0;
			vesselResource = resHandler.GetResource(resourceId);
			requestedRate = nominalRate * ioScale;
			amount = requestedRate * elapsedSec;
			if (amount.IsZeroOrNegativeOrNaN())
			{
				invAmount = 1.0;
			}
			else
			{
				// attempt to limit FP precision issues, by always bumping invAmount
				// to the next higher positive double. Limited testing show this is quite
				// effective at eliminating "execution remainders".
				invAmount = Lib.NextHigherPositiveDouble(1.0 / amount);
			}
		}

		internal abstract double GetWorstIO(double initialIO);

		internal abstract void ApplyToResource(double IOFactor);

		public abstract double ExecutedRate { get; }

		public abstract double SignedExecutedRate { get; }

		public double ExecutedMaxIOFactor => Math.Min(maxIOFactor, 1.0);
	}

	public abstract class RecipeInputBase : RecipeIO
	{
		protected RecipeInputBase(Recipe recipe, int resourceId, double nominalRate) : base(recipe, resourceId, nominalRate)
		{
		}
	}

	public abstract class RecipeOutputBase : RecipeIO
	{
		public bool dump;
		public bool dumpIsTweakable;

		protected RecipeOutputBase(Recipe recipe, int resourceId, double nominalRate, bool dump, bool dumpIsTweakable) : base(recipe, resourceId, nominalRate)
		{
			this.dump = dump;
			this.dumpIsTweakable = dumpIsTweakable;
		}
	}

	public sealed class RecipeInput : RecipeInputBase
	{
		internal RecipeInput(Recipe recipe, int resourceId, double nominalRate)
			: base(recipe, resourceId, nominalRate) { }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal override double GetWorstIO(double initialIO)
		{
			if (amount != 0.0)
				maxIOFactor = Math.Max((vesselResource.Amount + vesselResource.Deferred) * invAmount, 0.0);

			return Math.Min(maxIOFactor, initialIO);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal override void ApplyToResource(double IOFactor)
		{
			vesselResource.Consume(amount * IOFactor);
		}

		public override double ExecutedRate
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => recipe.ExecutedFactor * requestedRate;
		}

		public override double SignedExecutedRate
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => recipe.ExecutedFactor * -requestedRate;
		}
	}

	public sealed class RecipeConstraint : RecipeInputBase
	{
		internal RecipeConstraint(Recipe recipe, int resourceId, double nominalRate)
			: base(recipe, resourceId, nominalRate) { }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal override double GetWorstIO(double initialIO)
		{
			if (amount != 0.0)
				maxIOFactor = Math.Max((vesselResource.Amount + vesselResource.Deferred) * invAmount, 0.0);

			return Math.Min(maxIOFactor, initialIO);
		}

		internal override void ApplyToResource(double IOFactor) { }

		public override double ExecutedRate
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => recipe.ExecutedFactor * requestedRate;
		}

		public override double SignedExecutedRate
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => recipe.ExecutedFactor * -requestedRate;
		}
	}

	public sealed class RecipeBiInputMain : RecipeInputBase
	{
		private RecipeBiInputAlt altInput;
		private double mainInputProportion;

		internal RecipeBiInputMain(Recipe recipe, RecipeBiInputAlt altInput, int resourceId, double nominalRate)
			: base(recipe, resourceId, nominalRate)
		{
			this.altInput = altInput;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal override double GetWorstIO(double initialIO)
		{
			// TODO : test this extensively !!!
			if (amount != 0.0)
				maxIOFactor = Math.Max((vesselResource.Amount + vesselResource.Deferred) * invAmount, 0.0);

			double ioFactor = Math.Min(maxIOFactor, initialIO);
			mainInputProportion = ioFactor / initialIO;
			if (ioFactor < initialIO)
				ioFactor += altInput.GetAltWorstIO(maxIOFactor, initialIO);

			return ioFactor;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal override void ApplyToResource(double IOFactor)
		{
			vesselResource.Consume(amount * mainInputProportion * IOFactor);
		}

		public override double ExecutedRate
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => recipe.ExecutedFactor * mainInputProportion * requestedRate;
		}

		public override double SignedExecutedRate
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => recipe.ExecutedFactor * mainInputProportion * -requestedRate;
		}
	}

	public sealed class RecipeBiInputAlt : RecipeInputBase
	{
		private double altInputProportion;

		internal RecipeBiInputAlt(Recipe recipe, int resourceId, double nominalRate)
			: base(recipe, resourceId, nominalRate) { }

		internal double GetAltWorstIO(double mainIOFactor, double initialIOFactor)
		{
			if (amount != 0.0)
				maxIOFactor = Math.Max((vesselResource.Amount + vesselResource.Deferred) * invAmount, 0.0);

			double altIOFactor = Math.Min(maxIOFactor, initialIOFactor - mainIOFactor);
			altInputProportion = altIOFactor / initialIOFactor;
			return altIOFactor;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal override double GetWorstIO(double initialIO)
		{
			return initialIO;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal override void ApplyToResource(double IOFactor)
		{
			vesselResource.Consume(amount * altInputProportion * IOFactor);
		}

		public override double ExecutedRate
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => recipe.ExecutedFactor * altInputProportion * requestedRate;
		}

		public override double SignedExecutedRate
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => recipe.ExecutedFactor * altInputProportion * -requestedRate;
		}
	}

	public sealed class RecipeOutput : RecipeOutputBase
	{
		internal RecipeOutput(Recipe recipe, int resourceId, double nominalRate, bool dump, bool dumpIsTweakable)
			: base(recipe, resourceId, nominalRate, dump, dumpIsTweakable) { }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal override double GetWorstIO(double initialIO)
		{
			if (dump)
				return initialIO;

			if (amount != 0.0)
				maxIOFactor = Math.Max((vesselResource.Capacity - (vesselResource.Amount + vesselResource.Deferred)) * invAmount, 0.0);

			return Math.Min(maxIOFactor, initialIO);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal override void ApplyToResource(double IOFactor)
		{
			vesselResource.Produce(amount * IOFactor);
		}

		public override double ExecutedRate
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => recipe.ExecutedFactor * requestedRate;
		}

		public override double SignedExecutedRate
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => recipe.ExecutedFactor * requestedRate;
		}
	}

	public sealed class RecipeLocalInput : RecipeInputBase
	{
		private PartResourceWrapper localResource;

		internal RecipeLocalInput(Recipe recipe, PartResourceWrapper localResource, double nominalRate)
			: base(recipe, localResource.resId, nominalRate)
		{
			this.localResource = localResource;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal override double GetWorstIO(double initialIO)
		{
			if (amount != 0.0)
				maxIOFactor = Math.Max(localResource.Amount * invAmount, 0.0);

			return Math.Min(maxIOFactor, initialIO);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal override void ApplyToResource(double IOFactor)
		{
			double amountDelta = Math.Min(localResource.Amount, amount * IOFactor);

			if (localResource.FlowState)
				vesselResource.Consume(amountDelta);

			vesselResource.ResourceWrapper.AddLocalDeferredRequest(localResource, -amountDelta);
		}

		public override double ExecutedRate
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => recipe.ExecutedFactor * requestedRate;
		}

		public override double SignedExecutedRate
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => recipe.ExecutedFactor * -requestedRate;
		}
	}

	public sealed class RecipeLocalOutput : RecipeOutputBase
	{
		private PartResourceWrapper localResource;

		internal RecipeLocalOutput(Recipe recipe, PartResourceWrapper localResource, double nominalRate, bool dump, bool dumpIsTweakable)
			: base(recipe, localResource.resId, nominalRate, dump, dumpIsTweakable)
		{
			this.localResource = localResource;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal override double GetWorstIO(double initialIO)
		{
			if (dump)
				return initialIO;

			if (amount != 0.0)
				maxIOFactor = Math.Max((localResource.Capacity - localResource.Amount) * invAmount, 0.0);

			return Math.Min(maxIOFactor, initialIO);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal override void ApplyToResource(double IOFactor)
		{
			double amountDelta = Math.Min(localResource.Capacity - localResource.Amount, amount * IOFactor);

			if (localResource.FlowState)
				vesselResource.Consume(amountDelta);

			vesselResource.ResourceWrapper.AddLocalDeferredRequest(localResource, amountDelta);
		}

		public override double ExecutedRate
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => recipe.ExecutedFactor * requestedRate;
		}

		public override double SignedExecutedRate
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => recipe.ExecutedFactor * requestedRate;
		}
	}

	public sealed class UnknownIO : RecipeIO
	{
		public UnknownIO(VesselResource resource) : base(Recipe.UnknownBrokerRecipe, resource.id, 0.0)
		{
			vesselResource = resource;
		}

		internal override double GetWorstIO(double initialIO)
		{
			throw new NotImplementedException();
		}

		internal override void ApplyToResource(double IOFactor)
		{
			throw new NotImplementedException();
		}

		public override double ExecutedRate
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => requestedRate;
		}

		public override double SignedExecutedRate
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => requestedRate;
		}
	}
}
