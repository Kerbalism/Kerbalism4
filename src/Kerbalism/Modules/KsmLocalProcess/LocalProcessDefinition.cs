using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Flee.PublicTypes;

namespace KERBALISM
{
	public class LocalProcessDefinition : KsmModuleDefinition
	{
		[CFGValue] public string processName;
		[CFGValue] public string title = string.Empty;      // UI title
		[CFGValue] public string desc = string.Empty;       // UI description (long text)
		[CFGValue] public bool canToggle = true;    // can the process be toggled on/off
		[CFGValue] public bool canAdjust = true;    // can the process execution level be adjusted

		[CFGValue] public string categoryName;
		[CFGValue] public string categoryTitle;
		[CFGValue] public bool categoryExpand;

		[CFGValue] public string uiGroupName = null;         // internal name of the UI group
		[CFGValue] public string uiGroupDisplayName = null;  // display name of the UI group
		[CFGValue] public bool running = false; // will the process be running on part creation
		[CFGValue] public double recipeModifier = 1.0;

		public RecipeCategory recipeCategory;

		public LocalRecipeDefinition recipe;

		[CFGValue] public string localModifier;
		private ExpressionContext modifierContext;
		public IGenericExpression<double> modifierExpression;
		public bool hasModifier = false;


		public override void OnLoad(ConfigNode definitionNode)
		{
			if (string.IsNullOrEmpty(title))
				title = processName;

			if (string.IsNullOrEmpty(categoryName))
				categoryName = RecipeCategory.Others.name;

			if (!RecipeCategory.TryGet(categoryName, out recipeCategory))
			{
				recipeCategory = RecipeCategory.GetOrCreate(categoryName, categoryTitle, categoryExpand);
			}

			ConfigNode recipeNode = definitionNode.GetNode("LOCAL_PROCESS");
			if (recipeNode != null)
				recipe = new LocalRecipeDefinition(recipeNode, recipeCategory);

			if (!string.IsNullOrEmpty(localModifier))
			{
				LocalProcessHandler dummyHandler = new LocalProcessHandler();
				modifierContext = new ExpressionContext(dummyHandler);
				modifierContext.Options.CaseSensitive = true;
				modifierContext.Options.ParseCulture = System.Globalization.CultureInfo.InvariantCulture;
				modifierContext.Options.OwnerMemberAccess = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
				modifierContext.Imports.AddType(typeof(Math));

				try
				{
					ParseExpressionResourceCall(ref localModifier);
					modifierExpression = modifierContext.CompileGeneric<double>(localModifier);
					hasModifier = true;
				}
				catch (Exception e)
				{
					hasModifier = false;
					throw new Exception($"Can't parse ModuleKsmLocalProcess definition {DefinitionId}\nError in localModifier: '{localModifier}'\n{e.Message}");
				}
			}

		}

		public override string ModuleDescription<ModuleKsmProcessController>(ModuleKsmProcessController modulePrefab)
		{
			KsmString ks = KsmString.Get;

			if (desc.Length > 0)
			{
				ks.Add(desc).Break();
			}

			foreach (RecipeOutputDefinition output in recipe.outputs)
			{
				string title = output.resourceDef.displayName.Length > 10 ? output.resourceDef.abbreviation : output.resourceDef.displayName;
				ks.Info(title, KF.ReadableRate(output.rate * recipeModifier, false), KF.KolorPosRate, 80);
			}

			foreach (RecipeInputDefinition input in recipe.inputs)
			{
				string title;
				if (input is RecipeAbstractInputDefinition abstractInput)
					title = abstractInput.title;
				else
					title = input.resourceDef.displayName.Length > 10 ? input.resourceDef.abbreviation : input.resourceDef.displayName;

				ks.Info(title, KF.ReadableRate(input.rate * recipeModifier, false), KF.KolorNegRate, 80);
			}

			return ks.GetStringAndRelease();
		}

		public override string ModuleTitle => title;

		private static void ParseExpressionResourceCall(ref string expression)
		{
			Regex regex = new Regex(@"(Local|Vessel)Resource\(""(.*?)""\)");
			expression = regex.Replace(expression, ResourceNameEvaluator);
		}

		private static string ResourceNameEvaluator(Match match)
		{
			if (match.Groups.Count != 3)
				throw new Exception($"Error parsing Resource call : {match.Value}");

			if (!VesselResHandler.allKSPResourceIdsByName.TryGetValue(match.Groups[2].Value, out int resId))
				throw new Exception($"Error parsing Resource call : {match.Value}, resource {match.Groups[2].Value} not found !");

			switch (match.Groups[1].Value)
			{
				case "Local":
					return nameof(LocalProcessHandler.LocalResource) + "(" + resId + ")";
				case "Vessel":
					return nameof(LocalProcessHandler.VesselResource) + "(" + resId + ")";
			}

			throw new Exception($"Error parsing Resource call : {match.Value}");
		}
	}
}
