using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KERBALISM.KsmGui;
using UnityEngine;
using UnityEngine.UI;

namespace KERBALISM
{
	public class VesselManager : KsmGuiVerticalLayout
	{
		VesselSummaryUI summary;

		public VesselManager(KsmGuiBase parent) : base(parent, 0, 0, 0, 0, 0, TextAnchor.UpperLeft)
		{
			KsmGuiToggleList<KsmGuiBase> tabs = new KsmGuiToggleList<KsmGuiBase>(this, KsmGuiLib.Orientation.Horizontal, OnTabSelected);
			tabs.SetLayoutElement(true, false, -1, 18);

			summary = new VesselSummaryUI(this, false);
			new KsmGuiToggleListElement<KsmGuiBase>(tabs, summary, "Summary");
			new KsmGuiToggleListElement<KsmGuiBase>(tabs, null, "Control");
			new KsmGuiToggleListElement<KsmGuiBase>(tabs, null, "Data");

		}

		public void SetVessel(VesselDataBase vessel)
		{
			summary.SetVessel(vessel);
		}

		private void OnTabSelected(KsmGuiBase tabContent, bool selected)
		{
			tabContent.Enabled = selected;
			LayoutOptimizer.SetDirty();
			LayoutOptimizer.RebuildLayout();
		}
	}
}
