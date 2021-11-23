using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KERBALISM.KsmGui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KERBALISM
{
	public class VesselManager : KsmGuiVerticalLayout
	{
		private VesselSummaryUI summary;
		private DataManager dataManager;

		public VesselManager(KsmGuiBase parent) : base(parent, 0, 0, 0, 0, 0, TextAnchor.UpperLeft)
		{
			KsmGuiToggleList<KsmGuiBase> tabs = new KsmGuiToggleList<KsmGuiBase>(this, KsmGuiLib.Orientation.Horizontal, OnTabSelected);
			tabs.SetLayoutElement(true, false, -1, 18);

			summary = new VesselSummaryUI(this, false);
			dataManager = new DataManager(this);

			KsmGuiToggleListElement<KsmGuiBase>summaryElement = new KsmGuiToggleListElement<KsmGuiBase>(tabs, summary, "SUMMARY");
			summaryElement.TextObject.TextComponent.alignment = TextAlignmentOptions.Center;
			summaryElement.TextObject.TextComponent.fontStyle = FontStyles.Bold;
			KsmGuiToggleListElement<KsmGuiBase> controlElement = new KsmGuiToggleListElement<KsmGuiBase>(tabs, null, "CONTROL");
			controlElement.TextObject.TextComponent.alignment = TextAlignmentOptions.Center;
			controlElement.TextObject.TextComponent.fontStyle = FontStyles.Bold;
			KsmGuiToggleListElement<KsmGuiBase> dataElement = new KsmGuiToggleListElement<KsmGuiBase>(tabs, dataManager, "DATA");
			dataElement.TextObject.TextComponent.alignment = TextAlignmentOptions.Center;
			controlElement.TextObject.TextComponent.fontStyle = FontStyles.Bold;

		}

		public void SetVessel(VesselDataBase vessel)
		{
			summary.SetVessel(vessel);
			dataManager.SetVessel(vessel);
		}

		private void OnTabSelected(KsmGuiBase tabContent, bool selected)
		{
			tabContent.Enabled = selected;
			LayoutOptimizer.SetDirty();
			LayoutOptimizer.RebuildLayout();
		}
	}
}
