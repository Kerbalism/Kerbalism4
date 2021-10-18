using System;
using System.Collections.Generic;
using Flee.PublicTypes;

namespace KERBALISM
{
	public class ProcessDefinition : RecipeDefinition
	{
		public static readonly List<ProcessDefinition> definitions = new List<ProcessDefinition>();
		public static readonly Dictionary<string, ProcessDefinition> definitionsByName = new Dictionary<string, ProcessDefinition>();

		public static void ParseDefinitions(ConfigNode[] processNodes)
		{
			foreach (ConfigNode processNode in processNodes)
			{
				ProcessDefinition definition;
				try
				{
					definition = new ProcessDefinition(processNode, definitions.Count);
				}
				catch (Exception e)
				{
					string name = Lib.ConfigValue(processNode, nameof(name), string.Empty);
					ErrorManager.AddError(false, $"Error parsing PROCESS `{name}`", e.Message);
					continue;
				}

				if (definitionsByName.ContainsKey(definition.name))
				{
					ErrorManager.AddError(false, $"Duplicate definition for PROCESS `{definition.name}`");
					continue;
				}

				definitions.Add(definition);
				definitionsByName.Add(definition.name, definition);
			}
		}

		[CFGValue] public readonly string name;       // unique name for the process
		[CFGValue] public readonly string title = string.Empty;      // UI title
		[CFGValue] public readonly string desc = string.Empty;       // UI description (long text)
		[CFGValue] public readonly bool canToggle = true;    // can the process be toggled on/off
		[CFGValue] public readonly bool canAdjust = true;    // can the process execution level be adjusted
		
		[CFGValue] public readonly string categoryName; // if true, the process execution level is controlled by ProcessController module(s)
		[CFGValue] public readonly string categoryTitle; // if true, the process execution level is controlled by ProcessController module(s)
		[CFGValue] public readonly bool categoryExpand; // if true, the process execution level is controlled by ProcessController module(s)

		public readonly RecipeCategory category;   // the RecipeCategory for that process
		public readonly bool hasModifier;
		private readonly IGenericExpression<double> modifier;
		public readonly int definitionIndex;

		public bool isControlled; // if true, the process execution is controlled by ProcessController module(s)

		public ProcessDefinition(ConfigNode recipeNode, int definitionIndex) : base(recipeNode)
		{
			this.definitionIndex = definitionIndex;

			if (string.IsNullOrEmpty(title))
				title = name;

			if (string.IsNullOrEmpty(categoryName))
				categoryName = RecipeCategory.Others.name;

			if (!RecipeCategory.TryGet(categoryName, out category))
			{
				category = RecipeCategory.GetOrCreate(categoryName, categoryTitle, categoryExpand);
			}

			string modifierString = Lib.ConfigValue(recipeNode, "modifier", string.Empty);
			hasModifier = modifierString.Length > 0;

			if (hasModifier)
			{
				try
				{
					Lib.ParseFleeExpressionResHandlerResourceCall(ref modifierString);
					modifier = VesselDataBase.ExpressionBuilderInstance.ModifierContext.CompileGeneric<double>(modifierString);
				}
				catch (Exception e)
				{
					hasModifier = false;
					throw new Exception($"Can't parse modifier expression `{modifierString}`\n{e.Message}");
				}
			}


			foreach (RecipeInputDefinition input in inputs)
			{
				if (input.resourceDef.id == VesselResHandler.ElectricChargeId && category.ecProducer)
				{
					throw new Exception($"A process in category={category.name} can't have an ElectricCharge input");
				}
			}

			foreach (RecipeOutputDefinition output in outputs)
			{
				if (output.resourceDef.id == VesselResHandler.ElectricChargeId && !category.ecProducer)
				{
					throw new Exception($"A process in category={category.name} can't have an ElectricCharge output");
				}
			}

			if (Settings.LogProcessesMassConservationInfo)
				LogMassConservation();
		}

		public Recipe CreateRecipe()
		{
			Recipe recipe = new Recipe(title, category);
			foreach (RecipeInputDefinition input in inputs)
			{
				recipe.AddInput(input.resourceDef.id, input.rate);
			}

			foreach (RecipeOutputDefinition output in outputs)
			{
				recipe.AddOutput(output.resourceDef.id, output.rate, output.dumped, output.dumpedIsTweakable);
			}

			return recipe;
		}

		public double EvaluateModifier(VesselDataBase data)
		{
			if (hasModifier)
			{
				modifier.Owner = data;
				return Lib.Clamp(modifier.Evaluate(), 0.0, double.MaxValue);
			}

			return 1.0;
		}

		public string GetInfo(double capacity, bool includeDescription)
		{
			KsmString ks = KsmString.Get;

			if (includeDescription && desc.Length > 0)
			{
				ks.Add(desc).Break();
			}

			foreach (RecipeOutputDefinition output in outputs)
			{
				string title = output.resourceDef.displayName.Length > 16 ? output.resourceDef.abbreviation : output.resourceDef.displayName;
				ks.Info(title, KF.ReadableRate(output.rate * capacity, false), KF.KolorPosRate, 100);
			}

			foreach (RecipeInputDefinition input in inputs)
			{
				string title = input.resourceDef.displayName.Length > 16 ? input.resourceDef.abbreviation : input.resourceDef.displayName;
				ks.Info(title, KF.ReadableRate(input.rate * capacity, false), KF.KolorNegRate, 100);
			}

			return ks.End();
		}

		private void LogMassConservation()
		{
			KsmString ks = KsmString.Get;
			ks.Add($"Logging mass conservation info for process {name} :").Break();

			double RateInGrPerSec(RecipeIODefinition ioDefinition)
			{
				return ioDefinition.resourceDef.density * 1000.0 * 1000.0 * ioDefinition.rate;
			}

			double RateInKgPerHour(RecipeIODefinition ioDefinition)
			{
				return ioDefinition.resourceDef.density * 1000.0 * ioDefinition.rate * 3600.0;
			}

			double totalInputTonsPerSecond = 0.0;
			foreach (RecipeIODefinition input in inputs)
			{
				if (input.resourceDef.density > 0f)
				{
					totalInputTonsPerSecond += input.resourceDef.density * input.rate;
					ks.Add($"  - Input : {input.name}, {RateInGrPerSec(input)} g/s, {RateInKgPerHour(input)} kg/h").Break();
				}
				else
				{
					ks.Add($"  - Input : {input.name} - massless").Break();
				}
			}

			double totalOutputTonsPerSecond = 0.0;
			foreach (RecipeIODefinition output in outputs)
			{
				if (output.resourceDef.density > 0f)
				{
					totalOutputTonsPerSecond += output.resourceDef.density * output.rate;
					ks.Add($"  - Output : {output.name}, {RateInGrPerSec(output)} g/s, {RateInKgPerHour(output)} kg/h").Break();
				}
				else
				{
					ks.Add($"  - Output : {output.name} - massless").Break();
				}

			}

			double rawBalance = totalOutputTonsPerSecond - totalInputTonsPerSecond;
			// 1 g/24h in t/s
			double threshold = 1.0 / (1000.0 * 1000.0) / (3600.0 * 24.0);

			if (Math.Abs(rawBalance) > threshold)
			{
				if (rawBalance > 0.0)
					ks.Add($"Process is creating mass !").Break();
				else
					ks.Add($"Process is loosing mass !").Break();

				ks.Add($"Mass balance :").Break();
				double grPerHour = (rawBalance / 1000.0 / 1000.0) * 3600.0;
				ks.Add($"  {grPerHour:+0.###############;-0.###############;0} g/h").Break();
				double kgPerHour = (rawBalance / 1000.0) * 3600.0;
				ks.Add($"  {kgPerHour:+0.###############;-0.###############;0} kg/h").Break();
				double kgPer24H = kgPerHour * 24.0;
				ks.Add($"  {kgPer24H:+0.###############;-0.###############;0} kg/24h").Break();
			}
			else
			{
				double gperh = (rawBalance / 1000.0 / 1000.0) * 3600.0;
				ks.Add($"Process is conservating mass (balance : {gperh:+0.###############;-0.###############;0} g/h)").Break();
			}

			Lib.Log(ks.End());
		}
	}
}
