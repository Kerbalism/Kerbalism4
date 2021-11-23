using Flee.PublicTypes;
using System;
using System.Collections.Generic;

namespace KERBALISM
{
	public class LocalProcessHandler :
		KsmModuleHandler<ModuleKsmLocalProcess, LocalProcessHandler, LocalProcessDefinition>,
		IB9Switchable
	{
		private Recipe recipe;

		private ConfigNode abstractAmounts;
		private ConfigNode dumpSettings;
		private List<VesselResourceAbstract> abstractResources;

		private bool isRunning;
		public bool IsRunning
		{
			get => isRunning;
			set
			{
				if (!definition.canToggle)
					return;

				if (value != isRunning)
				{
					isRunning = value;

					if (IsLoaded)
						loadedModule.running = value;

					// refresh planner and VAB/SPH ui
					if (Lib.IsEditor) GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
				}
			}
		}

		public VesselResourceAbstract AbstractResource(string name)
		{
			if (abstractResources == null)
				return null;

			foreach (VesselResourceAbstract abstractResource in abstractResources)
			{
				if (abstractResource.name == name)
				{
					return abstractResource;
				}
			}

			return null;
		}

		public PartResourceWrapper LocalResource(int resId)
		{
			foreach (PartResourceWrapper localResource in partData.resources)
			{
				if (localResource.resId == resId)
				{
					return localResource;
				}
			}

			return null;
		}

		public VesselResource VesselResource(int resId)
		{
			return VesselData.ResHandler.GetResource(resId);
		}

		public override void OnFirstSetup()
		{
			isRunning = definition.running;
		}

		public override void OnStart()
		{
			SetupRecipe();
		}

		public bool SetupRecipe()
		{
			bool recipeCanExecute = true;
			abstractResources = null;
			recipe = new Recipe(definition.title, definition.recipeCategory);
			foreach (RecipeInputDefinition input in definition.recipe.inputs)
			{
				if (input is RecipeAbstractInputDefinition abstractInput)
				{
					VesselResourceAbstract abstractResource = VesselData.ResHandler.AddNewAbstractResourceToHandler();
					abstractResource.name = abstractInput.name;
					abstractResource.title = abstractInput.title;
					abstractResource.visible = abstractInput.isVisible;

					double amount = abstractInput.amount;
					if (abstractAmounts != null)
						abstractAmounts.TryGetValue(abstractInput.name, ref amount);

					abstractResource.SetAmountAndCapacity(amount);
					recipe.AddInput(abstractResource.id, abstractInput.rate);

					if (abstractResources == null)
						abstractResources = new List<VesselResourceAbstract>();

					abstractResources.Add(abstractResource);
				}
				else if (input is RecipeLocalInputDefinition localInput)
				{
					if (partData.resources.TryGetResourceWrapper(localInput.name, out PartResourceWrapper resWrapper))
					{
						if (localInput.overrideIsTweakable)
							resWrapper.IsTweakable = localInput.isTweakable;
						if (localInput.overrideIsVisible)
							resWrapper.IsVisible = localInput.isVisible;
						if (localInput.forceDisableFlow)
							resWrapper.FlowState = false;

						recipe.AddLocalInput(resWrapper, localInput.rate);
					}
					else
					{
						recipeCanExecute = false;
					}
				}
				else
				{
					recipe.AddInput(input.resourceDef.id, input.rate);
				}
			}

			foreach (RecipeOutputDefinition output in definition.recipe.outputs)
			{
				bool dump = output.dumped;
				if (dumpSettings != null)
					dumpSettings.TryGetValue(output.resourceDef.id.ToString(), ref dump);

				if (output is RecipeLocalOutputDefinition localOutput)
				{
					if (partData.resources.TryGetResourceWrapper(localOutput.name, out PartResourceWrapper resWrapper))
					{
						if (localOutput.overrideIsTweakable)
							resWrapper.IsTweakable = localOutput.isTweakable;
						if (localOutput.overrideIsVisible)
							resWrapper.IsVisible = localOutput.isVisible;
						if (localOutput.forceDisableFlow)
							resWrapper.FlowState = false;

						recipe.AddLocalOutput(resWrapper, localOutput.rate, dump, localOutput.dumpedIsTweakable);
					}
					else if(!localOutput.dumped)
					{
						recipeCanExecute = false;
					}
				}
				else
				{
					recipe.AddOutput(output.resourceDef.id, output.rate, dump, output.dumpedIsTweakable);
				}
			}

			abstractAmounts = null;
			dumpSettings = null;

			return recipeCanExecute;
		}

		public override void OnPartWasTransferred(VesselDataBase oldVD)
		{
			if (definition.recipe.hasAbstractInputs)
			{
				foreach (RecipeInputBase input in recipe.inputs)
				{
					if (input.vesselResource != null && input.vesselResource is VesselResourceAbstract abstractResource)
					{
						VesselData.ResHandler.CopyAbstractResourceToHandler(abstractResource, oldVD.ResHandler);
					}
				}
			}
		}

		public void OnSwitchChangeDefinition(KsmModuleDefinition previousDefinition)
		{
			isRunning = definition.running;

			foreach (RecipeInputBase input in recipe.inputs)
			{
				if (input.vesselResource != null && input.vesselResource is VesselResourceAbstract abstractResource)
				{
					VesselData.ResHandler.RemoveAbstractResource(abstractResource.id);
				}
			}

			recipe = null;
			if (definition.recipe != null)
			{
				if (!SetupRecipe())
				{
					isRunning = false;
				}
				else
				{
					if (IsLoaded)
					{
						loadedModule.PAWSetup();
					}
				}
			}
		}

		public void OnSwitchEnable() { }

		public void OnSwitchDisable() { }

		public string GetSubtypeDescription(KsmModuleDefinition subTypeDefinition, string techRequired)
		{
			return subTypeDefinition.ModuleDescription(modulePrefab);
		}

		public override void OnLoad(ConfigNode node)
		{
			isRunning = Lib.ConfigValue(node, "isRunning", true);
			abstractAmounts = node.GetNode("ABSTRACT_AMOUNTS");
			dumpSettings = node.GetNode("DUMP");
		}

		public override void OnSave(ConfigNode node)
		{
			if (recipe == null)
				return;

			node.AddValue("isRunning", isRunning);

			if (definition.recipe.hasAbstractInputs)
			{
				ConfigNode abstractAmounts = new ConfigNode("ABSTRACT_AMOUNTS");
				foreach (RecipeInputBase input in recipe.inputs)
				{
					if (input.vesselResource != null && input.vesselResource is VesselResourceAbstract)
					{
						abstractAmounts.AddValue(input.vesselResource.Name, input.vesselResource.Amount);
					}
				}

				if (abstractAmounts.values.Count > 0)
					node.AddNode(abstractAmounts);
			}

			if (recipe.outputs.Count > 0)
			{
				ConfigNode dumpSettings = new ConfigNode("DUMP");
				foreach (RecipeOutputBase output in recipe.outputs)
				{
					if (output.dumpIsTweakable)
					{
						dumpSettings.AddValue(output.resourceId.ToString(), output.dump);
					}
				}

				if (dumpSettings.values.Count > 0)
					node.AddNode(dumpSettings);
			}
		}

		public override void OnUpdate(double elapsedSec)
		{
			if (IsRunning)
			{
				double modifier = definition.recipeModifier;
				if (definition.hasModifier)
				{
					definition.modifierExpression.Owner = this;
					modifier *= definition.modifierExpression.Evaluate();
				}

				recipe.RequestExecution(VesselData.ResHandler, null, modifier);
			}
		}

		public override string ModuleTitle => definition.title;
	}
}
