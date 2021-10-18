using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Flee.PublicTypes;

namespace KERBALISM
{
	public class RecipeIODefinition
	{
		[CFGValue] public readonly string name = string.Empty;
		[CFGValue] public double ratio;
		[CFGValue] public double rate;

		public readonly PartResourceDefinition resourceDef;

		public RecipeIODefinition(ConfigNode node, bool isVirtualResource)
		{
			CFGValue.Parse(this, node);

			if (isVirtualResource)
			{
				if (string.IsNullOrEmpty(name))
					throw new Exception($"virtual input has no name !");
			}
			else
			{
				resourceDef = PartResourceLibrary.Instance.GetDefinition(name);
				if (resourceDef == null)
					throw new Exception($"resource {name} doesn't exists");
			}
		}

		public override string ToString() => name;

	}

	public class RecipeInputDefinition : RecipeIODefinition
	{
		public RecipeInputDefinition(ConfigNode node, bool isVirtualResource = false) : base(node, isVirtualResource) { }
	}

	public class RecipeOutputDefinition : RecipeIODefinition
	{
		[CFGValue] public readonly bool dumped = false;
		[CFGValue] public readonly bool dumpedIsTweakable = true;

		public RecipeOutputDefinition(ConfigNode node, bool isVirtualResource = false) : base(node, isVirtualResource) { }
	}

	public class RecipeLocalInputDefinition : RecipeInputDefinition
	{
		[CFGValue] public readonly bool overrideIsVisible;
		[CFGValue] public readonly bool isVisible;
		[CFGValue] public readonly bool overrideIsTweakable;
		[CFGValue] public readonly bool isTweakable;
		[CFGValue] public readonly bool forceDisableFlow;

		public RecipeLocalInputDefinition(ConfigNode node) : base(node) { }
	}

	public class RecipeLocalOutputDefinition : RecipeOutputDefinition
	{
		[CFGValue] public readonly bool overrideIsVisible;
		[CFGValue] public readonly bool isVisible;
		[CFGValue] public readonly bool overrideIsTweakable;
		[CFGValue] public readonly bool isTweakable;
		[CFGValue] public readonly bool forceDisableFlow;

		public RecipeLocalOutputDefinition(ConfigNode node) : base(node) { }
	}

	public class RecipeAbstractInputDefinition : RecipeInputDefinition
	{
		[CFGValue] public readonly string title;
		[CFGValue] public readonly bool isVisible = false;
		[CFGValue] public readonly double amount;

		public RecipeAbstractInputDefinition(ConfigNode node) : base(node, true)
		{
			if (amount.IsZeroOrNegativeOrNaN())
				throw new Exception($"VIRTUAL_INPUT {name} must define a positive amount");

			if (string.IsNullOrEmpty(title))
				title = name;
		}
	}

	public class RecipeDefinition
	{
		public List<RecipeInputDefinition> inputs = new List<RecipeInputDefinition>();
		public List<RecipeOutputDefinition> outputs = new List<RecipeOutputDefinition>();
		[CFGValue] public bool massConservation;
		public readonly bool hasAbstractInputs = false;

		public RecipeDefinition(ConfigNode recipeNode)
		{
			CFGValue.Parse(this, recipeNode);

			foreach (ConfigNode ioNode in recipeNode.nodes)
			{
				switch (ioNode.name)
				{
					case "INPUT":
						inputs.Add(new RecipeInputDefinition(ioNode));
						continue;
					case "OUTPUT":
						outputs.Add(new RecipeOutputDefinition(ioNode));
						continue;
					case "LOCAL_INPUT":
						inputs.Add(new RecipeLocalInputDefinition(ioNode));
						continue;
					case "LOCAL_OUTPUT":
						outputs.Add(new RecipeLocalOutputDefinition(ioNode));
						continue;
					case "ABSTRACT_INPUT":
						inputs.Add(new RecipeAbstractInputDefinition(ioNode));
						hasAbstractInputs = true;
						continue;
					default:
						throw new Exception($"node {ioNode.name} isn't a valid resource I/O");
				}
			}

			double inputsMass = 0.0;
			RecipeIODefinition mainInput = inputs.Find(p => p.rate > 0.0 && p.ratio > 0.0);

			foreach (RecipeIODefinition input in inputs)
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
				using massConservation :
				- require to have at least one non-massless input
				- if multiple non-massless outputs : use ratios on the outputs for which you want the sum of their mass being equal to the sum of the mass of all inputs
				- if there is a single non-massless output, you don't need to define the ratio
				- massless outputs can't use a ratio, they must define a rate
				- you can exclude a non-massless output from being mass-conservating by defining it's rate and not defining it's ratio.
				*/

				double outputsRatioSum = 0.0;
				foreach (RecipeIODefinition output in outputs)
				{
					if (output.resourceDef != null && output.resourceDef.density > 0f && output.rate == 0.0)
					{
						if (output.ratio == 0.0)
							output.ratio = 1.0;

						outputsRatioSum += output.ratio;
					}
				}

				foreach (RecipeIODefinition output in outputs)
				{
					if (output.rate == 0.0)
					{
						if (output.resourceDef == null || output.resourceDef.density == 0f)
						{
							throw new Exception($"Output '{output.name}' is massless but has no rate defined !");
						}

						double mass = inputsMass * (output.ratio / outputsRatioSum);
						output.rate = mass / output.resourceDef.density;
					}
				}
			}
		}
	}


}
