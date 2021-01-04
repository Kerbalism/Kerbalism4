using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	public abstract class ModuleAction
	{
		public abstract string Title { get; }

		public abstract bool OnAction();

		public virtual bool Interactable => true;

		public virtual string State => null;

		public void Action()
		{
			if (Interactable)
			{
				OnAction();
			}
		}
	}
}
