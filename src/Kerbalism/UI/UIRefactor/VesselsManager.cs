using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KERBALISM.KsmGui;
using UnityEngine;

namespace KERBALISM
{
	public class VesselsManager : KsmGuiVerticalLayout
	{
		// 2 modes :
		// - sort by vessel group, then by body
		// - sort by body, then by vessel group
		// allow to show non-simulated vessels (debris...)
		// allow to define custom groups (custom color on the vessel type icon)

		public VesselsManager(KsmGuiBase parent) : base(parent, 5, 0, 0, 0, 0, TextAnchor.UpperLeft)
		{

		}

		private class VesselEntry
		{
			// button : goto vessel with vessel type icon
			// label : vessel name
			// label : body (only in vessel type list mode)
			// icon : situation (orbiting, escape, landed), replaced by warning icon if applicable (storm, high rad
			// status icons + tooltips : sunlight, EC, supplies, rules
		}
	}
}
