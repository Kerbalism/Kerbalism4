using KERBALISM.KsmGui;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace KERBALISM
{
	public class ProcessPopup
	{
		VesselDataBase vd;
		Process process;

		// UI references
		KsmGuiWindow window;
		KsmGuiTextBox statusBox;
		KsmGuiSlider capacitySlider;
		KsmGuiButton enableButton;

		List<ResourceInputEntry> resources = new List<ResourceInputEntry>();
		List<ProcessControllerEntry> parts = new List<ProcessControllerEntry>();

		private static List<string> activePopups = new List<string>();
		private string popupId;

		public ProcessPopup(Process process, VesselDataBase vd)
		{
			popupId = process.definition.name;

			if (activePopups.Contains(popupId))
				return;

			activePopups.Add(popupId);

			this.process = process;
			this.vd = vd;

			// create the window
			window = new KsmGuiWindow(KsmGuiLib.Orientation.Vertical, true, KsmGuiStyle.defaultWindowOpacity, true, 0, TextAnchor.UpperLeft, 5f);
			window.OnClose = () => activePopups.Remove(popupId);
			window.SetLayoutElement(false, false, 300);

			// top header
			KsmGuiHeader topHeader = new KsmGuiHeader(window, process.definition.title, null, default, 120);
			if (vd.VesselName.Length > 0) topHeader.TextObject.SetTooltip(KsmString.Get.Add(Local.SCIENCEARCHIVE_onvessel, " : ").Format(vd.VesselName, KF.Bold).End);
			topHeader.AddButton(Textures.KsmGuiTexHeaderClose, () => window.Close(), Local.SCIENCEARCHIVE_closebutton);

			// content panel
			KsmGuiVerticalLayout content = new KsmGuiVerticalLayout(window, 5);
			content.SetLayoutElement(false, true, -1, -1);

			statusBox = new KsmGuiTextBox(content, "_");
			statusBox.TextObject.TextComponent.enableWordWrapping = false;
			statusBox.TextObject.TextComponent.overflowMode = TextOverflowModes.Ellipsis;

				if (process.definition.canAdjust || process.definition.canToggle)
			{
				KsmGuiHorizontalLayout buttons = new KsmGuiHorizontalLayout(content, 5);

				if (process.definition.canAdjust)
				{
					capacitySlider = new KsmGuiSlider(buttons, 0f, 1f, false, OnCapacityTweak, null, 200);
					capacitySlider.Value = (float)process.adjusterFactor;
				}

				if (process.definition.canToggle)
					enableButton = new KsmGuiButton(buttons, Local.Generic_ENABLED, OnToggle);
			}

			new KsmGuiHeader(content, Local.ProcessPopup_TITLE);

			KsmGuiVerticalLayout resourceList = new KsmGuiVerticalLayout(content);

			KsmGuiBase resListHeader = new KsmGuiBase(resourceList);
			resListHeader.SetLayoutElement(true, false, -1, 16);
			resListHeader.AddImageComponentWithColor(KsmGuiStyle.boxColor);

			KsmGuiText resHeaderText = new KsmGuiText(resListHeader,
				Local.ProcessPopup_NameTitle,
				TextAlignmentOptions.Left);
			resHeaderText.TextComponent.fontStyle = FontStyles.Bold;
			resHeaderText.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 5, 0);
			resHeaderText.TopTransform.SetSizeDelta(95, 16);

			KsmGuiText nominalHeaderText = new KsmGuiText(resListHeader,
				Local.ProcessPopup_MaxRateTitle,
				TextAlignmentOptions.Left);
			nominalHeaderText.TextComponent.fontStyle = FontStyles.Bold;
			nominalHeaderText.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 105, 0);
			nominalHeaderText.TopTransform.SetSizeDelta(65, 16);

			KsmGuiText statusHeaderText = new KsmGuiText(resListHeader,
				Local.ProcessPopup_StatusTitle,
				TextAlignmentOptions.Left);
			statusHeaderText.TextComponent.fontStyle = FontStyles.Bold;
			statusHeaderText.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 175, 0);
			statusHeaderText.TopTransform.SetSizeDelta(80, 16);

			KsmGuiText dumpHeaderText = new KsmGuiText(resListHeader,
				Local.ProcessPopup_DumpTitle,
				TextAlignmentOptions.Center);
			dumpHeaderText.TextComponent.color = Kolor.Yellow;
			dumpHeaderText.TextComponent.fontStyle = FontStyles.Bold;
			dumpHeaderText.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 250, 0);
			dumpHeaderText.TopTransform.SetSizeDelta(40, 16);

			for (int i = 0; i < process.recipe.outputs.Count; i++)
			{
				//VesselResource resource = vd.ResHandler.GetResource(output.Key);
				//if (!resource.Visible) // don't display invisible resources
				//	continue;
				resources.Add(new ResourceOutputEntry(resourceList, this, process.recipe.outputs[i], process.definition.outputs[i]));
			}

			for (int i = 0; i < process.recipe.inputs.Count; i++)
			{
				//VesselResource resource = vd.ResHandler.GetResource(output.Key);
				//if (!resource.Visible) // don't display invisible resources
				//	continue;
				resources.Add(new ResourceInputEntry(resourceList, this, process.recipe.inputs[i]));
			}

			if (process.definition.isControlled)
			{
				new KsmGuiHeader(content, Local.ProcessPopup_PARTS); // "PARTS"

				KsmGuiVerticalLayout partList = new KsmGuiVerticalLayout(content);

				KsmGuiBase partListHeader = new KsmGuiBase(partList);
				partListHeader.SetLayoutElement(true, false, -1, 16);
				partListHeader.AddImageComponentWithColor(KsmGuiStyle.boxColor);

				KsmGuiText partListPartName = new KsmGuiText(partListHeader, Local.ProcessPopup_NameTitle, TextAlignmentOptions.Left, false, TextOverflowModes.Ellipsis);
				partListPartName.TextComponent.fontStyle = FontStyles.Bold;
				partListPartName.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 5, 0);
				partListPartName.TopTransform.SetSizeDelta(150, 16);

				KsmGuiText partListCapacity = new KsmGuiText(partListHeader, Local.ProcessPopup_Capacity, TextAlignmentOptions.Center);
				partListCapacity.TextComponent.fontStyle = FontStyles.Bold;
				partListCapacity.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 160, 0);
				partListCapacity.TopTransform.SetSizeDelta(55, 16);

				KsmGuiText partListEnabled = new KsmGuiText(partListHeader, Lib.UppercaseFirst(Local.Generic_ENABLED), TextAlignmentOptions.Center);
				partListEnabled.TextComponent.color = Kolor.Yellow;
				partListEnabled.TextComponent.fontStyle = FontStyles.Bold;
				partListEnabled.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 230, 0);
				partListEnabled.TopTransform.SetSizeDelta(60, 16);

				// TODO : check controllers list changes on update
				foreach (ProcessControllerHandler pcd in process.controllers)
				{
					parts.Add(new ProcessControllerEntry(partList, pcd));
				}
			}

			window.SetUpdateAction(Update);
			window.RebuildLayout();
		}

		private void Update()
		{
			KsmString ks = KsmString.Get;

			if (process.definition.isControlled)
			{
				ks.Info("Total capacity", process.controllersCapacity.ToString("F1"));
				ks.Info("Enabled capacity", process.controllersEnabledCapacity.ToString("F1"));
			}

			if (process.definition.hasModifier)
			{
				ks.Info("Capacity modifier", process.modifierFactor.ToString("P1"));
			}

			if (process.definition.isControlled || process.definition.canAdjust || !process.enabled)
			{
				ks.Info("Requested capacity", process.modifierFactor.ToString("F1"));
			}

			ks.Info("Execution level", process.recipe.ExecutedFactor.ToString("P1"));

			statusBox.Text = ks.End();

			if (enableButton != null)
				enableButton.Text = process.enabled ? Local.Generic_ENABLED : Local.Generic_DISABLED;

		}

		private void OnToggle()
		{
			process.enabled = !process.enabled;
		}

		private void OnCapacityTweak(float capacity)
		{
			process.adjusterFactor = Lib.Clamp(capacity, 0f, 1f);
		}

		public class ResourceOutputEntry : ResourceInputEntry
		{
			KsmGuiTextButton resDumpText;

			private RecipeOutputBase output;

			protected override double NominalRate => -base.NominalRate;

			public ResourceOutputEntry(KsmGuiBase parent, ProcessPopup window, RecipeOutputBase output, RecipeOutputDefinition outputDefinition)
				: base(parent, window, output, true)
			{
				output = this.output;

				resDumpText = new KsmGuiTextButton(this, "", null, null, TextAlignmentOptions.Center);
				resDumpText.TextComponent.color = Kolor.Yellow;
				resDumpText.TextComponent.fontStyle = FontStyles.Bold;
				resDumpText.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 250, 0);
				resDumpText.TopTransform.SetSizeDelta(40, 16);
				resDumpText.SetButtonOnClick(OnToggleDump);
				resDumpText.Interactable = outputDefinition.dumpedIsTweakable;
			}

			protected override void Update()
			{
				base.Update();
				resDumpText.Text = output.dump ? Local.Generic_YES : Local.Generic_NO;
			}

			private void OnToggleDump()
			{
				output.dump = !output.dump;
			}
		}

		public class ResourceInputEntry : KsmGuiBase
		{
			protected ProcessPopup window;
			protected VesselResource resource;
			KsmGuiText resNameText;
			KsmGuiText resRateText;
			KsmGuiText resStatusText;
			
			protected RecipeIO io;

			protected virtual double NominalRate
			{
				get
				{
					if (window.process.definition.isControlled)
						return window.process.controllersCapacity * -io.NominalRate;

					return -io.NominalRate;
				}
			}

			public ResourceInputEntry(KsmGuiBase parent, ProcessPopup window, RecipeIO io, bool isOutput = false) : base(parent)
			{
				this.window = window;
				this.io = io;

				resource = window.vd.ResHandler.GetResource(io.resourceId);

				SetLayoutElement(true, false, -1, 16);
				this.AddImageComponentWithColor(KsmGuiStyle.boxColor);
				SetUpdateAction(Update);

				resNameText = new KsmGuiText(this, resource.Title, TextAlignmentOptions.Left, false, TextOverflowModes.Ellipsis);
				resNameText.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 5, 0);
				resNameText.TopTransform.SetSizeDelta(95, 16);
				resNameText.SetTooltip(resource.BrokerListTooltip());

				resRateText = new KsmGuiText(this, "", TextAlignmentOptions.Left);
				resRateText.TextComponent.color = isOutput ? Kolor.PosRate : Kolor.NegRate;
				resRateText.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 105, 0);
				resRateText.TopTransform.SetSizeDelta(65, 16);

				resStatusText = new KsmGuiText(this, "", TextAlignmentOptions.Left);
				resStatusText.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 175, 0);
				resStatusText.TopTransform.SetSizeDelta(80, 16);
			}

			protected virtual void Update()
			{
				resRateText.Text = KsmString.Get.Format(KF.ReadableRate(NominalRate), KF.Color(NominalRate > 0.0 ? Kolor.PosRate : Kolor.NegRate)).End();
				resStatusText.Text = KsmString.Get.Format(KF.ReadableRate(io.SignedExecutedRate), KF.Color(io.SignedExecutedRate > 0.0 ? Kolor.PosRate : Kolor.NegRate)).End();

				//if (isInput && resource.AvailabilityFactor == 0.0)
				//{
				//	resStatusText.Text = Local.Generic_notAvailable; //  "n/a";
				//	resStatusText.TextComponent.color = Lib.KolorToColor(Lib.Kolor.Red);
				//}
				//else if (!isInput && !dump && resource.ProduceRequests == 0.0 && (resource.Capacity == 0.0 || resource.Level == 1.0))
				//{
				//	resStatusText.Text = Local.ProcessPopup_NoStorage; // "no storage";
				//	if(canDump)
				//		resStatusText.TextComponent.color = Lib.KolorToColor(Lib.Kolor.Red);
				//	else
				//		resStatusText.TextComponent.color = Lib.KolorToColor(Lib.Kolor.Yellow);
				//}
			}
		}

		public class ProcessControllerEntry : KsmGuiBase
		{
			KsmGuiText resCapText;
			KsmGuiTextButton resToggleText;
			ProcessControllerHandler controller;

			public ProcessControllerEntry(KsmGuiBase parent, ProcessControllerHandler controller) : base(parent)
			{
				this.controller = controller;

				SetLayoutElement(true, false, -1, 16);
				this.AddImageComponentWithColor(KsmGuiStyle.boxColor);
				SetUpdateAction(Update);

				KsmGuiText resNameText = new KsmGuiText(this, controller.partData.Title, TextAlignmentOptions.Left, false, TextOverflowModes.Ellipsis);
				resNameText.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 5, 0);
				resNameText.TopTransform.SetSizeDelta(150, 16);

				resCapText = new KsmGuiText(this, controller.definition.capacity.ToString("F1"), TextAlignmentOptions.Center);
				resCapText.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 160, 0);
				resCapText.TopTransform.SetSizeDelta(55, 16);

				resToggleText = new KsmGuiTextButton(this, "", null, null, TextAlignmentOptions.Center);
				resToggleText.TextComponent.color = Kolor.Yellow;
				resToggleText.TextComponent.fontStyle = FontStyles.Bold;
				resToggleText.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 230, 0);
				resToggleText.TopTransform.SetSizeDelta(60, 16);
				resToggleText.SetButtonOnClick(OnTogglePart);
			}

			private void Update()
			{
				resCapText.Text = controller.definition.capacity.ToString("F1");
				resCapText.TextComponent.color = Color.white;
				resToggleText.Enabled = true;
				resToggleText.Text = controller.IsRunning ? Local.Generic_YES : Local.Generic_NO;
			}

			private void OnTogglePart()
			{
				controller.IsRunning = !controller.IsRunning;
			}
		}
	}
}
