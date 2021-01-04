using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	// not done yet.
	// this will require some special handling in the ModuleHandlerType library and in the various methods using the activators
	// details TBD but I don't forsee any major roadblock 
	public class APIModuleHandler : ModuleHandler
	{
		private APIModuleTypeHandler typeHandler;

		public PartModule loadedModule;
		public PartModule prefabModule;

		public override PartModule LoadedModuleBase => loadedModule;
		public override PartModule PrefabModuleBase => prefabModule;

		public override string[] ModuleTypeNames => new string[] { "someModule" }; // TODO : implementation

		public override ActivationContext Activation => ActivationContext.Unloaded; // TODO : implementation

		private Action<double> fixedUpdate;
		private Action<double> plannerUpdate;

		// automation support mapped to IModuleStateInfo / IModuleToggleControl, available only on loaded vessels and in the editor
		private Func<string> shortState;
		private Func<string> longState;
		private Func<bool> toggleState;
		private Action<bool> toggleAction;

		public override void SetModuleReferences(PartModule prefabModule, PartModule loadedModule)
		{
			this.prefabModule = prefabModule;
			if (ReferenceEquals(loadedModule, null)) // bypass unity null equality overload
			{
				this.loadedModule = loadedModule;
			}
		}
	}

	// One instance for every module type that use the API
	public class APIModuleTypeHandler
	{
		// module type, ex : ModuleMyMod
		public string moduleType;

		// copied from a constant field in ModuleMyMod (note : we have to provide a 1:1 matching enum for ModuleHandler.Context in the API)
		public ModuleHandler.ActivationContext context;

		// delegate to a static method in ModuleMyMod with the signature : 
		// public static KsmBackgroundUpdate(PartModule prefab, ProtoPartModuleSnapshot protoModule, Part partPrefab, ProtoPartSnapshot protoPart, double elapsedSec)
		public Action<PartModule, ProtoPartModuleSnapshot, Part, ProtoPartSnapshot, double> backgroundUpdate;
	}
}
