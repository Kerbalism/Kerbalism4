using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	// Base classes that derive from KsmPartModule and ModuleHandler.
	// Have to be abstract so the activator doesn't get confused.
	// Could exclude generics as an alternative, but this isn't meant to be instantiated anyway.
	public abstract class ModuleKsmAnimal<TModule, THandler, TDefinition> : KsmPartModule<TModule, THandler, TDefinition>
	where TModule : ModuleKsmAnimal<TModule, THandler, TDefinition>
	where THandler : AnimalHandler<TModule, THandler, TDefinition>
		where TDefinition : AnimalDefinition
	{
		public override void OnStart(StartState state)
		{
			Lib.Log($"This animal has {moduleHandler.legCount} legs");
		}
	}

	public abstract class AnimalHandler<TModule, THandler, TDefinition> : KsmModuleHandler<TModule, THandler, TDefinition>
	where TModule : ModuleKsmAnimal<TModule, THandler, TDefinition>
	where THandler : AnimalHandler<TModule, THandler, TDefinition>
		where TDefinition : AnimalDefinition
	{
		public int legCount;
	}

	public class AnimalDefinition : KsmModuleDefinition
	{
		public override void OnLoad(ConfigNode definitionNode)
		{
			//throw new NotImplementedException();
		}
	}

	// Non-generic, non abstract versions of the base classes, necessary so :
	// - KSP can instantiate it as partmodules (it can't instantiate generic types)
	// - Our activator search find them (it can't find abstract types)
	public class ModuleKsmAnimal : ModuleKsmAnimal<ModuleKsmAnimal, AnimalHandler, AnimalDefinition> { }
	public class AnimalHandler : AnimalHandler<ModuleKsmAnimal, AnimalHandler, AnimalDefinition> { }

	// derivative classes
	public class ModuleKsmCat : ModuleKsmAnimal<ModuleKsmCat, CatHandler, AnimalDefinition>
	{
		public override void OnStart(StartState state)
		{
			moduleHandler.legCount = 4;
			moduleHandler.isHungry = true;

			base.OnStart(state);

			if (moduleHandler.isHungry)
				Lib.Log($"But this is in fact a Cat ! And he's hungry !");
		}
	}

	public class CatHandler : AnimalHandler<ModuleKsmCat, CatHandler, AnimalDefinition>
	{
		public bool isHungry;
	}
}
