using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KERBALISM
{
	public class Process
	{
		public ProcessDefinition definition;

		internal Recipe recipe;

		internal bool enabled;
		private bool executedEnabled;

		internal double adjusterFactor;
		private double executedAdjuster;

		internal List<ProcessControllerHandler> controllers;

		public double modifierFactor;
		public double controllersCapacity;
		public double controllersEnabledCapacity;
		public double requestedFactor;

		public double ExecutedFactor => executedEnabled ? recipe.ExecutedFactor * executedAdjuster : 0.0;

		public override string ToString() => $"{definition.name}, enabled={enabled}, executedFactor={ExecutedFactor}";

		public Process(ProcessDefinition definition)
		{
			this.definition = definition;
			recipe = definition.CreateRecipe();

			if (definition.isControlled)
				controllers = new List<ProcessControllerHandler>();

			enabled = true;
			adjusterFactor = 1.0;
		}

		public void Load(ConfigNode node)
		{
			enabled = Lib.ConfigValue(node, nameof(enabled), true);
			adjusterFactor = Lib.ConfigValue(node, nameof(adjusterFactor), 1.0);

			ConfigNode dumpSettingsNode = node.GetNode("DUMP");
			if (dumpSettingsNode != null)
			{
				foreach (ConfigNode.Value value in node.values)
				{
					if (!int.TryParse(value.name, out int resId))
						continue;

					for (int i = 0; i < recipe.outputs.Count; i++)
					{
						if (!definition.outputs[i].dumpedIsTweakable)
							continue;

						if (recipe.outputs[i].resourceId == resId && bool.TryParse(value.value, out bool dump))
						{
							recipe.outputs[i].dump = dump;
							break;
						}
					}
				}
			}
		}

		public void Save(ConfigNode node)
		{
			node.AddValue(nameof(definition.name), definition.name);
			node.AddValue(nameof(enabled), enabled);
			node.AddValue(nameof(adjusterFactor), adjusterFactor);

			if (recipe.outputs.Count > 0)
			{
				ConfigNode dumpNode = node.AddNode("DUMP");
				for (int i = 0; i < recipe.outputs.Count; i++)
				{
					if (definition.outputs[i].dumpedIsTweakable)
					{
						RecipeOutputBase recipeOutput = recipe.outputs[i];
						dumpNode.AddValue(recipeOutput.resourceId.ToString(), recipeOutput.dump);
					}
				}
			}
		}

		public void ResetBeforeModulesUpdate()
		{
			if (definition.isControlled)
			{
				controllersCapacity = 0.0;
				controllersEnabledCapacity = 0.0;
				controllers.Clear();
			}
		}

		public void Execute(VesselDataBase vd)
		{
			executedEnabled = enabled;
			executedAdjuster = adjusterFactor;
			modifierFactor = 0.0;
			requestedFactor = adjusterFactor;

			if (definition.isControlled)
			{
				controllersCapacity = 0.0;
				controllersEnabledCapacity = 0.0;

				foreach (ProcessControllerHandler processController in controllers)
				{
					controllersCapacity += processController.definition.capacity;

					if (processController.IsRunning)
					{
						controllersEnabledCapacity += processController.definition.capacity;
					}
				}

				requestedFactor *= controllersEnabledCapacity;
			}

			if (!enabled || requestedFactor == 0.0)
				return;

			if (definition.hasModifier)
			{
				modifierFactor = definition.EvaluateModifier(vd);
				requestedFactor *= modifierFactor;
			}

			if (requestedFactor == 0.0)
				return;

			recipe.RequestExecution(vd.ResHandler, null, requestedFactor);
		}
	}
}
