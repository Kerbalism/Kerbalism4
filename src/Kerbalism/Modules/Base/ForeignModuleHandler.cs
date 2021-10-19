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
		public override PartModule LoadedModuleBase { get; set; }
		public override PartModule PrefabModuleBase { get; set; }
	}
}
