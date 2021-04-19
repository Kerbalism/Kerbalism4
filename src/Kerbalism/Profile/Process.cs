using Flee.PublicTypes;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace KERBALISM
{
	public sealed class Process
	{
		public class Resource
		{
			public string name;
			public string displayName;
			public string abbreviation;
			public double rate;
			public double ratio;
			public PartResourceDefinition resourceDef;

			public virtual bool Load(string processName, ConfigNode node)
			{
				name = Lib.ConfigValue(node, "name", string.Empty);
				if (name.Length == 0)
				{
					Lib.Log($"skipping INPUT definition with no name in process `{processName}`", Lib.LogLevel.Error);
					return false;
				}

				rate = Lib.ConfigValue(node, "rate", 0.0);
				ratio = Lib.ConfigValue(node, "ratio", 0.0);

				resourceDef = PartResourceLibrary.Instance.GetDefinition(name);
				if (resourceDef != null)
				{
					displayName = resourceDef.displayName;
					abbreviation = resourceDef.abbreviation;
				}
				else
				{
					displayName = name;
					abbreviation = name.Substring(0, 3);
				}

				return true;
			}
		}

		public class Input : Resource
		{
			public override bool Load(string processName, ConfigNode node)
			{
				if (!base.Load(processName, node))
					return false;

				if (rate <= 0.0)
				{
					Lib.Log($"skipping INPUT `{name}` with no rate in process `{processName}`", Lib.LogLevel.Error);
					return false;
				}

				return true;
			}
		}

		public class Output : Resource
		{
			public bool canDump;
			public bool dumpByDefault;

			public override bool Load(string processName, ConfigNode node)
			{
				if (!base.Load(processName, node))
					return false;

				canDump = Lib.ConfigValue(node, "canDump", true);

				if (canDump)
					dumpByDefault = Lib.ConfigValue(node, "dumpByDefault", false);
				else
					dumpByDefault = false;

				return true;
			}
		}

		private static StringBuilder sb = new StringBuilder();

		public string name;                           // unique name for the process
		public string title;                          // UI title
		public string desc;                           // UI description (long text)
		public bool canToggle;                        // defines if this process can be toggled
		public List<Input> inputs;
		public List<Output> outputs;
		public bool massConservation;

		public ResourceBroker broker;
		public bool hasModifier;
		private IGenericExpression<double> modifier;

		public string CapacityResourceName { get; private set; }
		public bool UseCapacityResource { get; private set; }
		public string PseudoResourceName { get; private set; }

		public Process(ConfigNode node)
		{
			name = Lib.ConfigValue(node, "name", string.Empty);
			if (name.Length == 0)
				throw new Exception("skipping unnammed process");

			PseudoResourceName = Lib.ConfigValue(node, "pseudoResourceName", name + "Process");

			UseCapacityResource = Lib.ConfigValue(node, "useCapacityResource", false);
			CapacityResourceName = Lib.ConfigValue(node, "capacityResourceName", name + "Capacity");

			title = Lib.ConfigValue(node, "title", string.Empty);
			desc = Lib.ConfigValue(node, "desc", string.Empty);
			canToggle = Lib.ConfigValue(node, "canToggle", true);
			massConservation = Lib.ConfigValue(node, "massConservation", false);

			broker = ResourceBroker.GetOrCreate(name, ResourceBroker.BrokerCategory.Converter, title);

			string modifierString = Lib.ConfigValue(node, "modifier", string.Empty);
			hasModifier = modifierString.Length > 0;
			if (hasModifier)
			{
				try
				{
					modifier = VesselDataBase.ExpressionBuilderInstance.ModifierContext.CompileGeneric<double>(modifierString);
				}
				catch (Exception e)
				{
					ErrorManager.AddError(false, $"Can't parse modifier for process '{name}'", $"expression: {modifierString}\n{e.Message}");
					hasModifier = false;
				}
			}

			inputs = new List<Input>();
			foreach (ConfigNode inputNode in node.GetNodes("INPUT"))
			{
				Input input = new Input();
				if (!input.Load(name, inputNode))
					continue;

				inputs.Add(input);
			}

			outputs = new List<Output>();
			foreach (ConfigNode outputNode in node.GetNodes("OUTPUT"))
			{
				Output output = new Output();
				if (!output.Load(name, outputNode))
					continue;

				outputs.Add(output);
			}

			
			double inputsMass = 0.0;
			Input mainInput = inputs.Find(p => p.rate > 0.0 && p.ratio > 0.0);

			foreach (Input input in inputs)
			{
				/*
				This allow to define inputs with a rate + ratio on a specific input, and
				others inputs having only a ratio and their rate being calculated. Example :
				INPUT
				{
					// only one input can have both a rate and ratio defined
					name = ore
					rate = 0.001
					ratio = 0.5
				}
				INPUT
				{
					// only ratio defined : rate will be 0.001 * (1.0 / 0.5) = 0.002
					name = water
					ratio = 1.0
				}
				INPUT
				{
					// only rate defined : no influence from or toward the other inputs
					name = ElectricCharge
					rate = 2.5 
				}
				*/
				if (mainInput != null && input.rate == 0.0)
				{
					input.rate = mainInput.rate * (input.ratio / mainInput.ratio);
				}

				if (input.resourceDef != null && input.resourceDef.density > 0f)
				{
					inputsMass += input.rate * input.resourceDef.density;
				}
			}


			if (massConservation)
			{
				/*
				using outputMassConservation :
				- require to have at least one non-massless input
				- if multiple non-massless outputs : use ratios on the outputs for which you want the sum of their mass being equal to the sum of the mass of all inputs
				- if there is a single non-massless output, you don't need to define the ratio
				- massless outputs can't use a ratio, they must define a rate
				- you can exclude a non-massless output from being mass-conservating by defining it's rate and not defining it's ratio.
				*/

				double outputsTotalRatios = 0.0;
				foreach (Output output in outputs)
				{
					if (output.rate == 0.0 && output.resourceDef != null && output.resourceDef.density > 0f)
					{
						if (output.ratio == 0.0)
							output.ratio = 1.0;

						outputsTotalRatios += output.ratio;
					}
				}

				foreach (Output output in outputs)
				{
					if (output.rate == 0.0)
					{
						if (output.resourceDef == null || output.resourceDef.density == 0f)
						{
							ErrorManager.AddError(false, $"Error parsing process '{name}'", $"Output '{output.name}' is massless but has no rate defined !");
							throw new Exception($"Error parsing process '{name}'");
						}

						double mass = inputsMass * (output.ratio / outputsTotalRatios);
						output.rate = mass / output.resourceDef.density;
					}
				}
			}

			if (inputs.Count == 0 && outputs.Count == 0)
				throw new Exception($"Process {name} has no valid input or output, skipping..");

			if (UseCapacityResource)
			{
				VirtualResourceDefinition.GetOrCreateDefinition(CapacityResourceName, false, VesselResHandler.ResourceType.PartVirtual);
			}

			LogProcessRates();
		}

		public double EvaluateModifier(VesselDataBase data)
		{
			if (hasModifier)
			{
				modifier.Owner = data;
				return Lib.Clamp(modifier.Evaluate(), 0.0, double.MaxValue);
			}
			else
			{
				return data.ResHandler.GetResource(PseudoResourceName).Amount;
			}
		}

		public void Execute(VesselDataBase vd, double elapsed_s)
		{
			// get product of all environment modifiers
			double k = EvaluateModifier(vd);

			// only execute processes if necessary
			if (k <= 0.0)
				return;

			vd.VesselProcesses.TryGetProcessData(name, out VesselProcess vesselProcess);

			Recipe recipe = new Recipe(broker);

			foreach (Input input in inputs)
			{
				recipe.AddInput(input.name, input.rate * k * elapsed_s);
			}

			foreach (Output output in outputs)
			{
				if (vesselProcess != null)
					recipe.AddOutput(output.name, output.rate * k * elapsed_s, vesselProcess.dumpedOutputs.Contains(output.name));
				else
					recipe.AddOutput(output.name, output.rate * k * elapsed_s, output.dumpByDefault);
			}

			vd.ResHandler.AddRecipe(recipe);

			if (vesselProcess != null)
				vesselProcess.lastRecipe = recipe;
		}

		public string GetInfo(double capacity, bool includeDescription)
		{
			sb.Clear();
			double selfConsumingRate = 0.0;

			if (includeDescription && desc.Length > 0)
			{
				sb.AppendKSPLine(desc);
				sb.AppendKSPNewLine();
			}
			int inputCount = inputs.Count;
			int outputCount = outputs.Count;

			for (int i = 0; i < outputCount; i++)
			{
				Output output = outputs[i];
				sb.Append(Lib.Color(Lib.HumanReadableRate(output.rate * capacity, "F3", string.Empty, true), Lib.Kolor.PosRate, true));
				sb.Append("\t");
				sb.Append(output.displayName);

				if (i < outputCount - 1 || inputCount > 0)
					sb.AppendKSPNewLine();
			}

			for (int i = 0; i < inputCount; i++)
			{
				Input input = inputs[i];
				sb.Append(Lib.Color(Lib.HumanReadableRate(-input.rate * capacity, "F3", string.Empty, true), Lib.Kolor.NegRate, true));
				sb.Append("\t");
				sb.Append(input.displayName);

				if (i < inputCount - 1)
					sb.AppendKSPNewLine();

				if (UseCapacityResource && input.name == CapacityResourceName)
				{
					selfConsumingRate = input.rate;
				}
			}

			if (selfConsumingRate > 0.0)
			{
				sb.AppendKSPNewLine();
				sb.AppendKSPNewLine();
				sb.Append(Local.ProcessController_info1); //"Half-life"
				sb.Append("\t");
				sb.Append(Lib.HumanReadableDuration(0.5 * (capacity / selfConsumingRate)));
			}

			return sb.ToString();
		}


		private void LogProcessRates()
		{
#if DEBUG || DEVBUILD
			double totalInputMass = 0;
			double totalOutputMass = 0;
			StringBuilder sb = new StringBuilder();

			// this will only be printed if the process looks suspicious
			sb.AppendKSPLine($"Process {name} changes total mass of vessel:");

			foreach (Input i in inputs)
			{
				PartResourceDefinition resourceDef = PartResourceLibrary.Instance.GetDefinition(i.name);
				if (resourceDef == null)
				{
					sb.AppendKSPLine($"Unknown input resource {i.name}");
				}
				else
				{
					double kilosPerUnit = resourceDef.density * 1000.0;
					double kilosPerHour = 3600.0 * i.rate * kilosPerUnit;
					totalInputMass += kilosPerHour;
					sb.AppendKSPLine($"Input {i.name}@{i.rate} = {kilosPerHour} kg/h");
				}
			}

			foreach (Output o in outputs)
			{
				PartResourceDefinition resourceDef = PartResourceLibrary.Instance.GetDefinition(o.name);
				if (resourceDef == null)
				{
					sb.AppendKSPLine($"$Unknown output resource {o.name}");
				}
				else
				{
					double kilosPerUnit = resourceDef.density * 1000.0;
					double kilosPerHour = 3600.0 * o.rate * kilosPerUnit;
					totalOutputMass += kilosPerHour;
					sb.AppendKSPLine($"Output {o.name}@{o.rate} = {kilosPerHour} kg/h");
				}
			}

			sb.AppendKSPLine($"Total input mass : {totalInputMass}");
			sb.AppendKSPLine($"Total output mass: {totalOutputMass}");

			// there will be some numerical errors involved in the simulation.
			// due to the very small numbers (very low rates per second, calculated 20 times per second and more),
			// the actual rates might be quite different when the simulation runs. here we just look at nominal process
			// inputs and outputs for one hour, eliminating the error that will be introduced when the simulation runs
			// at slower speeds.
			// you can't put floating point numbers into a computer and expect perfect results, so we ignore processes that are "good enough".
			double diff = totalOutputMass - totalInputMass;

			if (diff > 0.001) // warn if process generates > 1g/h
			{
				sb.AppendKSPLine($"Process is generating mass: {diff} kg/h ({(diff * 1000.0).ToString("F5")} g/h)");
				sb.AppendKSPLine("Note: this might be expected behaviour if external resources (like air) are used as an input.");
				Lib.Log(sb.ToString());
			}

			if (diff < -0.01) // warn if process looses > 10g/h
			{
				sb.AppendKSPLine($"Process looses more than 1g/h mass: {diff} kg/h ({(diff * 1000.0).ToString("F5")} g/h)");
				Lib.Log(sb.ToString());
			}
#endif
		}

		public override string ToString()
		{
			return name;
		}
	}

} // KERBALISM

