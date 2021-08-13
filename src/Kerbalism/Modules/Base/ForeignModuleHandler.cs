using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	/// <summary>
	/// Base class for implementing partmodule handlers for other mods modules
	/// </summary>
	public abstract class ForeignModuleHandler : ModuleHandler
	{
		public PartModule loadedModule;
		public PartModule prefabModule;

		public override PartModule LoadedModuleBase => loadedModule;
		public override PartModule PrefabModuleBase => prefabModule;

		public override void SetModuleReferences(PartModule prefabModule, PartModule loadedModule)
		{
			this.prefabModule = prefabModule;
			if (!ReferenceEquals(loadedModule, null)) // bypass unity null equality overload
			{
				this.loadedModule = loadedModule;
			}
		}
	}
}
