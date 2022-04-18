using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace KERBALISM
{
	public interface IRecipeExecutedCallback
	{
		bool IsCallbackRegistered { get; set; }
	}

	public interface IMultipleRecipeExecutedCallback : IRecipeExecutedCallback
	{
		List<Recipe> ExecutedRecipes { get; }
		
	}

	public interface ICommonRecipeExecutedCallback : IRecipeExecutedCallback
	{
		void OnRecipesExecuted(double elapsedSec);
	}

	public sealed class Recipe
	{
		public class UnknownBroker
		{
			public static readonly UnknownBroker Instance = new UnknownBroker();
			public string BrokerTitle => RecipeCategory.Unknown.title;
		}

		public static readonly Recipe UnknownBrokerRecipe = new Recipe("Unknown", RecipeCategory.Unknown);

		// data
		public string title;
		public RecipeCategory category;

		internal readonly Action<double> onRecipeExecutedCallback;
		internal readonly bool hasCallback;
		internal readonly List<RecipeInputBase> inputs = new List<RecipeInputBase>(); // set of input resources
		internal readonly List<RecipeOutputBase> outputs = new List<RecipeOutputBase>(); // set of output resources


		/// <summary>
		/// Only effective/applicable on pure output recipes that have more than one output.<br/>
		/// If true, the recipe execution isn't constrained by the resources storage capacity.<br/>
		/// Said otherwise, the nominal rate of all outputs is always applied.<br/>
		/// This has the same effect as if all outputs have "dump" set to true.
		/// </summary>
		private bool overflow = false;

		/// <summary>
		/// Only effective/applicable on pure input recipes that have more than one input.<br/>
		/// If true, the recipe execution isn't constrained by the resources availability.<br/>
		/// Said otherwise, the nominal rate of all inputs is always applied.
		/// </summary>
		private bool underflow = false;


		private double ioScale; // optional global factor to apply on the nominal input/output rates
		private double ioMax; // optional constraint 
		public int priority;

		// performance optimization
		private int inputsCount;
		private int outputsCount;

		// % of the recipe is left to execute, internal variable
		private double left = 1.0;
		private bool fullyExecuted = false;

		public Action<double> onRecipeExecuted;

		public double ExecutedFactor
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => 1.0 - left;
		}

		/// <summary>
		/// Create a resource input/output recipe. To trigger the execution of that recipe, call its <see cref="RequestExecution"/> method every update.<br/>
		/// To add a callback called after the recipe execution, either :<br/>
		/// - Implement the <see cref="ICommonRecipeExecutedCallback"/> interface on an object and pass that object to the <c>RequestExecution()</c> method.<br/>
		/// - Implement the <see cref="IMultipleRecipeExecutedCallback"/> interface on an object, pass that object to the <c>RequestExecution()</c> method and pass the recipe-specific callback to <paramref name="onRecipeExecuted"/>
		/// </summary>
		/// <param name="title"> UI title</param>
		/// <param name="category"> recipe category</param>
		/// <param name="onRecipeExecuted">Optional callback called after the recipe execution. Require the RequestExecution() method to be passed a IMultipleRecipeExecutedCallback object</param>
		/// <param name="overflow">Only applies to output-only recipes. If true, the nominal rate of all outputs is always applied, regardless of their storage capacity</param>
		/// <param name="underflow">Only applies to input-only recipes. If true, the nominal rate of all inputs is always applied, regardless of their availability</param>
		public Recipe(string title, RecipeCategory category, Action<double> onRecipeExecuted = null, bool overflow = false, bool underflow = false)
		{
			this.title = title;
			this.category = category;
			this.onRecipeExecuted = onRecipeExecuted;
			this.overflow = overflow;
			this.underflow = underflow;
		}

		public override string ToString()
		{
			return $"{title}, category={category.name}, ioScale={ioScale}";
		}

		/// <summary>add an input to the recipe</summary>
		public RecipeConstraint AddConstraint(int resourceId, double nominalRate)
		{
			overflow = false; // recipes with inputs can't overflow
			RecipeConstraint constraint = new RecipeConstraint(this, resourceId, nominalRate);
			inputs.Add(constraint);
			return constraint;
		}

		/// <summary>add an input to the recipe</summary>
		public RecipeInput AddInput(int resourceId, double nominalRate)
		{
			overflow = false; // recipes with inputs can't overflow
			RecipeInput input = new RecipeInput(this, resourceId, nominalRate);
			inputs.Add(input);
			return input;
		}

		/// <summary>A bi-input is a "main" input that is substitued by an "alt" input if the main input can't satisfy the demand </summary>
		public void AddBiInput(int mainResourceId, double mainNominalRate, int altResourceId, double altNominalRate, out RecipeBiInputMain mainInput, out RecipeBiInputAlt altInput)
		{
			overflow = false; // recipes with inputs can't overflow
			altInput = new RecipeBiInputAlt(this, altResourceId, altNominalRate);
			mainInput = new RecipeBiInputMain(this, altInput, mainResourceId, mainNominalRate);
			inputs.Add(mainInput);
			inputs.Add(altInput);
		}

		/// <summary>add an input to the recipe</summary>
		public RecipeOutput AddOutput(int resourceId, double rate, bool dump, bool dumpIsTweakable)
		{
			underflow = false; // recipes with outputs can't underflow
			RecipeOutput output = new RecipeOutput(this, resourceId, rate, dump, dumpIsTweakable);
			outputs.Add(output);
			return output;
		}

		/// <summary>add an input to the recipe, using the KSP resource name (slower).</summary>
		public RecipeInput AddInput(string resourceName, double nominalRate)
		{
			if (!VesselResHandler.allKSPResourceIdsByName.TryGetValue(resourceName, out int resourceId))
				return null;

			overflow = false; // recipes with inputs can't overflow
			RecipeInput input = new RecipeInput(this, resourceId, nominalRate);
			inputs.Add(input);
			return input;
		}

		/// <summary>add an input to the recipe, using the KSP resource name (slower)</summary>
		public RecipeOutput AddOutput(string resourceName, double rate, bool dump, bool dumpIsTweakable)
		{
			if (!VesselResHandler.allKSPResourceIdsByName.TryGetValue(resourceName, out int resourceId))
				return null;

			underflow = false; // recipes with outputs can't underflow
			RecipeOutput output = new RecipeOutput(this, resourceId, rate, dump, dumpIsTweakable);
			outputs.Add(output);
			return output;
		}

		/// <summary>add an input to the recipe</summary>
		public RecipeLocalInput AddLocalInput(PartResourceWrapper localResource, double nominalRate)
		{
			overflow = false; // recipes with inputs can't overflow
			RecipeLocalInput input = new RecipeLocalInput(this, localResource, nominalRate);
			inputs.Add(input);
			return input;
		}

		/// <summary>add an input to the recipe</summary>
		public RecipeLocalOutput AddLocalOutput(PartResourceWrapper localResource, double nominalRate, bool dump, bool dumpIsTweakable)
		{
			underflow = false; // recipes with outputs can't underflow
			RecipeLocalOutput output = new RecipeLocalOutput(this, localResource, nominalRate, dump, dumpIsTweakable);
			outputs.Add(output);
			return output;
		}

		/// <summary>
		/// Mark this recipe for deferred execution by the provided VesselResHandler.<br/>
		/// This must to be called repeteadly on every vessel update.
		/// Once the recipe is executed, the IRecipeBroker owner of the recipe will have its OnRecipeExecuted() method called.
		/// </summary>
		/// <param name="ioScale">Global scale to apply to all inputs/outputs nominal rates</param>
		/// <param name="ioMax">[0;1] factor : maximum execution level. This allow setting a custom IO constraint without having to define an input/output for it</param>
		/// <returns>true if the recipe will be executed, false otherwise</returns>
		public bool RequestExecution(VesselResHandler resHandler, IRecipeExecutedCallback callback = null, double ioScale = 1.0, double ioMax = 1.0)
		{
			if (ioScale <= 0.0 || ioMax <= 0.0)
				return false;

			this.ioScale = ioScale;
			this.ioMax = Math.Min(ioMax, 1.0);

			resHandler.RequestRecipeExecution(this);

			if (callback != null)
			{
				if (!callback.IsCallbackRegistered)
				{
					callback.IsCallbackRegistered = true;
					resHandler.RequestRecipeCallback(callback);
				}

				if (callback is IMultipleRecipeExecutedCallback multiCallback)
				{
					multiCallback.ExecutedRecipes.Add(this);
				}
			}
			
			return true;
		}

		public static void ExecuteRecipes(VesselResHandler resHandler, List<Recipe> recipes, double elapsedSec)
		{
			for (int i = recipes.Count - 1; i >= 0; i--)
			{
				Recipe recipe = recipes[i];

				recipe.left = recipe.ioMax;
				recipe.fullyExecuted = recipe.left == 0.0;
				recipe.inputsCount = recipe.inputs.Count;
				recipe.outputsCount = recipe.outputs.Count;

				for (int j = 0; j < recipe.inputsCount; j++)
					recipe.inputs[j].Prepare(resHandler, elapsedSec, recipe.ioScale);

				for (int k = 0; k < recipe.outputsCount; k++)
					recipe.outputs[k].Prepare(resHandler, elapsedSec, recipe.ioScale);
			}

			// sort all recipes by their priority
			// Note : strangely, the delegate form is consistently 20-40% faster than implementing IComparable<Recipe>
			recipes.Sort((x, y) => y.priority.CompareTo(x.priority));

			// Call all recipes repeteadly until all of them report that they can't perform any
			// production/consumption, either because they have completed their request (left == 0)
			// or because the resources they request are empty, or the ones they produce are full.
			bool executing = true;
			int recipesCount = recipes.Count;
			while (executing)
			{
				executing = false;
				// TODO : possible performance optimization
				// It might be worthwile to remove recipes from being iterated over once they are executed (left == 0).
				// We need to profile how frequently 
				for (int i = 0; i < recipesCount; i++)
				{
					Recipe recipe = recipes[i];
					if (!recipe.fullyExecuted)
					{
						executing |= recipe.ExecuteRecipeStep(resHandler);
					}
				}
			}

			//for (int i = 0; i < recipesCount; i++)
			//{
			//	Recipe recipe = recipes[i];

			//	if (recipe.hasCallback)
			//		recipe.onRecipeExecutedCallback(elapsedSec);
			//}
		}

		/// <summary>
		/// Execute the recipe and record deferred consumption/production for inputs/ouputs.
		/// This need to be called multiple times for complete execution of the recipe.
		/// return true if something was produced or consumed, false otherwise
		/// </summary>
		private bool ExecuteRecipeStep(VesselResHandler resHandler)
		{
			// determine worst input ratio
			double worstInput = left;
			if (!underflow)
			{
				for (int i = 0; i < inputsCount; ++i)
					worstInput = inputs[i].GetWorstIO(worstInput);
			}

			// determine worst output ratio
			double worstOutput = left;
			if (!overflow)
			{
				for (int i = 0; i < outputsCount; ++i)
					worstOutput = outputs[i].GetWorstIO(worstOutput);
			}

			// determine worst io
			double worstIO = Math.Min(worstInput, worstOutput);

			if (worstIO > 0.0)
			{
				// consume inputs
				for (int i = 0; i < inputsCount; ++i)
					inputs[i].ApplyToResource(worstIO);

				// produce outputs
				for (int i = 0; i < outputsCount; ++i)
					outputs[i].ApplyToResource(worstIO);

				// update % left to execute, 
				left = left - worstIO;

				// check if fully executed, avoid a negative value due to FP errors
				if (left <= 0.0)
				{
					left = 0.0;
					fullyExecuted = true;
				}

				// the recipe was executed, at least partially
				return true;
			}

			// nothing was produced or consumed
			return false;
		}
	}
}
