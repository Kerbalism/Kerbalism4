using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM.ModuleUI
{
	public abstract class ModuleInteractableBase<THandler> : ModuleUIBase<THandler>, IModuleUILabel, IModuleUIInteractable where THandler : ModuleHandler
	{
		public virtual bool IsInteractable => true;
		public abstract string GetLabel();
	}
}
