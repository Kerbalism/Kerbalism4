using System;
using System.Collections.Generic;
using KERBALISM.KsmGui;
using KSP.Localization;
using KSP.UI.Screens;
using TMPro;
using UnityEngine;

namespace KERBALISM
{
	public class VesselsManager : KsmGuiVerticalLayout
	{
		private const int filterButtonWidth = 55;

		public enum SortMode {type, name}

		// TODO : get ride of the current group thing :
		// use static vessel type filters (row 1 : filters)
		// then row 2 : groups, with hardcoded groups then customizable ones:
		// - loaded vessels
		// - pinned vessels
		// - hidden vessels

		private List<VesselEntry> vesselEntries = new List<VesselEntry>(DB.VesselDatas.Count);
		private HashSet<Guid> vesselEntriesId = new HashSet<Guid>();
		private List<BodyEntry> bodyEntries = new List<BodyEntry>();

		private SortMode sortMode = SortMode.type;

		private KsmGuiBase filtersParent;
		private KsmGuiTextButton filterButton;
		private List<VesselTypeFilter> filters = new List<VesselTypeFilter>();
		private TypeFilters filterValues = new TypeFilters();

		private KsmGuiVerticalScrollView scrollView;

		private KsmGuiBase activeVesselsHeader;
		private KsmGuiVerticalLayout activeVessels;
		private KsmGuiBase pinnedVesselsHeader;
		private KsmGuiVerticalLayout pinnedVessels;
		private KsmGuiVerticalLayout flatVesselList;

		public Action<VesselData> onVesselSelected;

		private bool listChanged = true;

		private CelestialBody bodyFilter;

		private class TypeFilters
		{
			private bool[] values;

			public TypeFilters()
			{
				int[] typeValues = (int[])Enum.GetValues(typeof(VesselType));
				int max = 0;
				foreach (int typeValue in typeValues)
				{
					max = Math.Max(max, typeValue);
				}

				values = new bool[max + 1];
			}

			public bool IsTypeVisible(VesselType type)
			{
				return values[(int) type];
			}

			public void SetFilter(VesselType type, bool value)
			{
				values[(int)type] = value;
			}
		}

		public VesselsManager(KsmGuiBase parent) : base(parent)
		{
			SetLayoutElement(false, true, -1, -1, 360);
			SetUpdateAction(Update);

			filtersParent = new KsmGuiBase(this);
			filtersParent.SetLayoutElement(true, false, -1, -1, -1, 20);

			filterButton = new KsmGuiTextButton(filtersParent, "Filters", FilterMenu);
			filterButton.StaticLayout(filterButtonWidth - 5, 20);

			filters.Add(new VesselTypeFilter(filtersParent, this, 0, Textures.vesselTypeShip, Localizer.Format("#autoLOC_900684"), VesselType.Ship));
			filters.Add(new VesselTypeFilter(filtersParent, this, 1, Textures.vesselTypeStation, Localizer.Format("#autoLOC_900679"), VesselType.Station));
			filters.Add(new VesselTypeFilter(filtersParent, this, 2, Textures.vesselTypeProbe, Localizer.Format("#autoLOC_900681"), VesselType.Probe));
			filters.Add(new VesselTypeFilter(filtersParent, this, 3, Textures.vesselTypeCommsRelay, Localizer.Format("#autoLOC_900687"), VesselType.Relay));
			filters.Add(new VesselTypeFilter(filtersParent, this, 4, Textures.vesselTypeLander, Localizer.Format("#autoLOC_900686"), VesselType.Lander));
			filters.Add(new VesselTypeFilter(filtersParent, this, 5, Textures.vesselTypeRover, Localizer.Format("#autoLOC_900683"), VesselType.Rover));
			filters.Add(new VesselTypeFilter(filtersParent, this, 6, Textures.vesselTypeBase, Localizer.Format("#autoLoc_6002178"), VesselType.Base));
			filters.Add(new VesselTypeFilter(filtersParent, this, 7, Textures.vesselTypeDeployScience, "Deployed ground part", VesselType.DeployedGroundPart, VesselType.DeployedScienceController, VesselType.DeployedSciencePart));
			filters.Add(new VesselTypeFilter(filtersParent, this, 8, Textures.vesselTypeAircraft, Localizer.Format("#autoLOC_900685"), VesselType.Plane));
			filters.Add(new VesselTypeFilter(filtersParent, this, 9, Textures.vesselTypeEVA, Localizer.Format("#autoLOC_6003088"), VesselType.EVA));
			filters.Add(new VesselTypeFilter(filtersParent, this, 10, Textures.vesselTypeSpaceObj, Localizer.Format("#autoLoc_6002177"), VesselType.SpaceObject, VesselType.Unknown));
			filters.Add(new VesselTypeFilter(filtersParent, this, 11, Textures.vesselTypeDebris, Localizer.Format("#autoLOC_900676"), VesselType.Debris, VesselType.DroppedPart));

			FiltersDefaults();

			scrollView = new KsmGuiVerticalScrollView(this, 0, 0, 0, 0, 0);
			scrollView.SetLayoutElement(true, false, -1, 250, -1, 200);

			activeVesselsHeader = new KsmGuiBase(scrollView);
			activeVesselsHeader.SetLayoutElement(true, false, -1, -1, -1, 18);
			activeVesselsHeader.SetColor(Color.black);
			KsmGuiText headerText = new KsmGuiText(activeVesselsHeader, KsmString.Get.Format("Loaded vessels", KF.Bold, KF.KolorYellow).End());
			headerText.StaticLayoutStretchInParent();
			
			activeVessels = new KsmGuiVerticalLayout(scrollView);

			pinnedVesselsHeader = new KsmGuiBase(scrollView);
			pinnedVesselsHeader.SetLayoutElement(true, false, -1, -1, -1, 18);
			pinnedVesselsHeader.SetColor(Color.black);
			KsmGuiText pinnedText = new KsmGuiText(pinnedVesselsHeader, KsmString.Get.Format("Pinned vessels", KF.Bold, KF.KolorYellow).End());
			pinnedText.StaticLayoutStretchInParent();

			pinnedVessels = new KsmGuiVerticalLayout(scrollView);

			flatVesselList = new KsmGuiVerticalLayout(scrollView);

			int bodyCount = FlightGlobals.Bodies.Count;
			while (bodyCount > 0)
			{
				foreach (CelestialBody body in FlightGlobals.Bodies)
				{
					if (bodyEntries.Exists(p => p.Body == body))
						continue;

					if (body.referenceBody == body)
					{
						BodyEntry sunEntry = new BodyEntry(scrollView, this, body, null);
						bodyEntries.Add(sunEntry);
						bodyCount--;
						break;
					}
					else
					{
						foreach (BodyEntry listEntry in bodyEntries)
						{
							if (body.referenceBody == listEntry.Body)
							{
								BodyEntry childEntry = new BodyEntry(listEntry.childsLayout, this, body, listEntry);
								listEntry.ChildBodies.Add(childEntry);
								bodyEntries.Add(childEntry);
								bodyCount--;
								break;
							}
						}
					}
				}
			}

			foreach (BodyEntry listEntry in bodyEntries)
			{
				listEntry.ChildBodies.Sort(OrderChildBodies);

				for (int i = 0; i < listEntry.ChildBodies.Count; i++)
				{
					listEntry.ChildBodies[i].MoveToSiblingIndex(i);
				}
			}
		}

		private int OrderChildBodies(BodyEntry a, BodyEntry b)
		{
			if (a.Body.orbit == null && b.Body.orbit == null) return 0;
			else if (a.Body.orbit == null) return -1;
			else if (b.Body.orbit == null) return 1;
			else return a.Body.orbit.semiMajorAxis.CompareTo(b.Body.orbit.semiMajorAxis);
		}

		private void Update()
		{
			for (int i = vesselEntries.Count - 1; i >= 0 ; i--)
			{
				if (!DB.VesselExist(vesselEntries[i].VdId))
				{
					vesselEntriesId.Remove(vesselEntries[i].VdId);
					vesselEntries[i].Destroy();
					vesselEntries.RemoveAt(i);
					listChanged = true;
				}
			}

			if (vesselEntries.Count < DB.VesselDatas.Count)
			{
				foreach (VesselData vd in DB.VesselDatas)
				{
					if (!vesselEntriesId.Contains(vd.VesselId))
					{
						VesselEntry entry = new VesselEntry(scrollView, this, vd);
						vesselEntriesId.Add(entry.VdId);
						vesselEntries.Add(entry);
						listChanged = true;
					}
				}
			}

			foreach (VesselEntry entry in vesselEntries)
			{
				if (entry.MainBody != entry.Vd.Vessel.mainBody)
				{
					entry.MainBody = entry.Vd.Vessel.mainBody;
					listChanged = true;
				}
			}

			if (listChanged)
			{
				if (sortMode == SortMode.name)
				{
					vesselEntries.Sort((a, b) => string.Compare(a.Vd.Vessel.DiscoveryInfo.displayName.Value, b.Vd.Vessel.DiscoveryInfo.displayName.Value, StringComparison.OrdinalIgnoreCase));
				}
				else
				{
					vesselEntries.Sort((a, b) => a.Vd.Vessel.vesselType.CompareTo(b.Vd.Vessel.vesselType));
				}
			}

			if (listChanged)
			{
				listChanged = false;
				pinnedVesselsHeader.Enabled = false;
				activeVesselsHeader.Enabled = false;

				foreach (BodyEntry body in bodyEntries)
				{
					body.Vessels.Clear();
				}

				foreach (VesselEntry entry in vesselEntries)
				{
					entry.Enabled = filterValues.IsTypeVisible(entry.Vd.Vessel.vesselType);

					if (entry.Vd.Vessel.loaded)
					{
						activeVesselsHeader.Enabled = true;
						entry.SetParent(activeVessels);

						if (!entry.Vd.Vessel.isActiveVessel)
						{
							entry.MoveAsLastChild();
						}
					}
					else if (entry.IsPinned)
					{
						entry.SetParent(pinnedVessels);
						pinnedVesselsHeader.Enabled = true;
					}
					else
					{
						BodyEntry body = bodyEntries.Find(p => p.Body == entry.Vd.Vessel.mainBody);
						entry.SetParent(body.vesselsLayout);
						body.Vessels.Add(entry);
					}
				}

				//foreach (VesselEntry entry in vesselEntries)
				//{
				//	entry.SetColor(entry.TopTransform.GetSiblingIndex() % 2 == 0 ? KsmGuiStyle.boxColor : Color.clear);
				//}

				// suppliesScrollView.LayoutElement.preferredHeight = Math.Min(entryCount * 18f + 5f, 165f);

				RebuildLayout();
			}
		}

		private void FilterMenu()
		{
			KsmGuiContextMenu.Instance.Create(filterButton);
			KsmGuiContextMenu.Instance.AddButton("Show all", ShowAllFilters);
			KsmGuiContextMenu.Instance.AddButton("Hide all", HideAllFilters);
			KsmGuiContextMenu.Instance.AddButton("Reset to default", FiltersDefaults);
		}

		private void ShowAllFilters()
		{
			foreach (VesselTypeFilter filter in filters)
			{
				filter.Value = true;
			}
		}

		private void HideAllFilters()
		{
			foreach (VesselTypeFilter filter in filters)
			{
				filter.Value = false;
			}
		}

		private void FiltersDefaults()
		{
			foreach (VesselTypeFilter filter in filters)
			{
				bool enabled = !(filter.IsTypeFiltered(VesselType.Unknown) || filter.IsTypeFiltered(VesselType.Debris) || filter.IsTypeFiltered(VesselType.SpaceObject));

				if (enabled != filter.Value)
				{
					filter.Value = enabled;
				}
				else
				{
					filter.UpdateState();
					filter.ValueChanged(enabled);
				}
			}
		}

		private class VesselTypeFilter : KsmGuiIconToggle
		{
			public bool FilterState => Value;
			public VesselType[] types;
			private VesselsManager manager;

			public VesselTypeFilter(KsmGuiBase parent, VesselsManager manager, int position, Texture2D texture, string name, params VesselType[] types)
				: base(parent, texture, Kolor.Yellow, Kolor.Orange, true, null, 20, 20)
			{
				this.types = types;
				this.manager = manager;
				SetTooltip(name);
				SetValueChangedAction(ValueChanged);
				StaticLayout(25, 20, filterButtonWidth + (position * 25));
			}

			public void ValueChanged(bool value)
			{
				for (int i = 0; i < types.Length; i++)
				{
					manager.filterValues.SetFilter(types[i], value);
				}

				manager.listChanged = true;
			}

			public bool IsTypeFiltered(VesselType type)
			{
				for (int i = 0; i < types.Length; i++)
				{
					if (types[i] == type)
					{
						return true;
					}
				}

				return false;
			}
		}

		private class VesselEntry : KsmGuiBase
		{
			public VesselData Vd { get; private set; }
			public Guid VdId { get; private set; }

			private VesselsManager manager;

			private KsmGuiIconButton gotoButton;
			private KsmGuiTextButton vesselName;
			private KsmGuiBase bodyInfo;
			private KsmGuiText bodyName;
			private KsmGuiImage situationIcon;
			//private KsmGuiIcon situationIconOverlay;
			private KsmGuiImage infoIcon;
			private KsmGuiImage ecIcon;
			private KsmGuiImage suppliesIcon;
			private KsmGuiImage rulesIcon;
			private KsmGuiImage signalMainIcon;
			private KsmGuiImage signalLinkIcon;
			private KsmGuiImage signalDataIcon;

			private bool infoEnabled;
			private bool ecEnabled;
			private bool suppliesEnabled;
			private bool rulesEnabled;
			private bool signalEnabled;

			public bool IsPinned
			{
				get => Vd.IsUIPinned;
				private set
				{
					if (value != Vd.IsUIPinned)
					{
						Vd.IsUIPinned = value;
						manager.listChanged = true;
					}
				}
			}

			public CelestialBody MainBody { get; set; }

			public bool IsOwned { get; private set; }
			public bool IsTracked { get; private set; } // is the vessel and it's orbit shown
			public bool IsSimulated { get; private set; } // are we simulating that vessel ?

		//private string Name => isOwned ? VD.VesselName : VD.Vessel.DiscoveryInfo.displayName

			private List<InfoTexture> infoTextures = new List<InfoTexture>();

			private class InfoTexture
			{
				public readonly string tooltipText;
				public readonly Texture2D texture;
				public readonly Kolor color;

				public InfoTexture(Texture2D texture, Kolor color, string tooltipText = null)
				{
					this.texture = texture;
					this.color = color;
					this.tooltipText = tooltipText;
				}

				public static InfoTexture sunlight = new InfoTexture(Textures.ttSun, Kolor.Green);
				public static InfoTexture noSunlight = new InfoTexture(Textures.ttSunStriked, Kolor.Yellow);
				public static InfoTexture storm = new InfoTexture(Textures.ttStorm, Kolor.Orange, KsmString.Get.Format("Solar storm in progress !", KF.Bold, KF.KolorOrange).End());
				public static InfoTexture belt = new InfoTexture(Textures.ttBelt, Kolor.Orange, KsmString.Get.Format("Inside the radiation belt !", KF.Bold, KF.KolorOrange).End());
				public static InfoTexture plasma = new InfoTexture(Textures.ttPlasma, Kolor.Yellow);
				public static InfoTexture radLow = new InfoTexture(Textures.ttRadioactive, Kolor.Yellow, KsmString.Get.Format(Local.Monitor_ExposedRadiation3, KF.Bold, KF.KolorYellow).End()); //"Exposed to moderate radiation"
				public static InfoTexture radMed = new InfoTexture(Textures.ttRadioactive, Kolor.Orange, KsmString.Get.Format(Local.Monitor_ExposedRadiation2, KF.Bold, KF.KolorOrange).End()); //"Exposed to intense radiation"
				public static InfoTexture radHigh = new InfoTexture(Textures.ttRadioactive, Kolor.Red, KsmString.Get.Format(Local.Monitor_ExposedRadiation1, KF.Bold, KF.KolorRed).End()); //"Exposed to extreme radiation"
			}

			// button : goto vessel with vessel type icon
			// label : vessel name
			// label : body (only in vessel type list mode)
			// icon : situation (orbiting, escape, landed), replaced by warning icon if applicable (storm, high rad...)
			// status icons + tooltips : sunlight, EC, supplies, rules, signal

			public VesselEntry(KsmGuiBase parent, VesselsManager manager, VesselData vd) : base(parent)
			{
				this.manager = manager;
				Vd = vd;
				VdId = vd.VesselId;
				MainBody = vd.Vessel.mainBody;
				IsPinned = vd.IsUIPinned;
				SetLayoutElement(true, false, -1, -1, -1, 22);
				SetUpdateAction(Update);

				int hPos = 0;
				gotoButton = new KsmGuiIconButton(this, GetVesselTypeIcon(Vd.Vessel), ContextMenu, 20, 20);
				gotoButton.StaticLayout(20, 22);
				hPos += (20 + 5);

				vesselName = new KsmGuiTextButton(this, null, OnVesselSelected, null, TextAlignmentOptions.Left, false, TextOverflowModes.Ellipsis);
				vesselName.StaticLayout(125, 22, hPos);
				hPos += (125 + 5);
				vesselName.TextComponent.fontStyle = FontStyles.Bold;
				//vesselName.UseEllipsisWithTooltip();
				vesselName.SetTooltip(VesselTooltip, TextAlignmentOptions.Top, 250);

				bodyInfo = new KsmGuiBase(this);
				bodyInfo.StaticLayout(65, 22, hPos);
				hPos += (65 + 5);
				bodyInfo.SetTooltip(SituationTooltip, TextAlignmentOptions.TopLeft, 250);

				situationIcon = new KsmGuiImage(bodyInfo, null, 20, 20);
				situationIcon.StaticLayout(20, 22);

				// map view visibility toggle, not implemented
				//situationIconOverlay = new KsmGuiIcon(bodyInfo, Textures.ttSignalNoData, 20, 20);
				//situationIconOverlay.SetIconColor(Kolor.Red);
				//situationIconOverlay.StaticLayout(20, 22);

				bodyName = new KsmGuiText(bodyInfo);
				bodyName.StaticLayout(40, 22, 25);
				bodyName.TextComponent.alignment = TextAlignmentOptions.Left;
				bodyName.TextComponent.enableWordWrapping = false;
				bodyName.TextComponent.overflowMode = TextOverflowModes.Truncate;
				//bodyName.UseEllipsisWithTooltip();

				infoIcon = new KsmGuiImage(this, Textures.ttSun, 20, 20);
				infoIcon.StaticLayout(20, 22, hPos);
				hPos += (20 + 5);
				infoIcon.SetTooltip(InfoTooltip);

				ecIcon = new KsmGuiImage(this, Textures.ttBattery, 20, 20);
				ecIcon.StaticLayout(20, 22, hPos);
				hPos += (20 + 5);
				ecIcon.SetTooltip(EcTooltip);

				suppliesIcon = new KsmGuiImage(this, Textures.ttBox, 20, 20);
				suppliesIcon.StaticLayout(20, 22, hPos);
				hPos += (20 + 5);
				suppliesIcon.SetTooltip(SuppliesTooltip);

				rulesIcon = new KsmGuiImage(this, Textures.ttHeart, 20, 20);
				rulesIcon.StaticLayout(20, 22, hPos);
				hPos += (20 + 5);
				rulesIcon.SetTooltip(() => new RulesTooltip(Vd));

				signalMainIcon = new KsmGuiImage(this, Textures.ttSignalFull, 20, 20);
				signalMainIcon.StaticLayout(20, 22, hPos);
				signalMainIcon.SetTooltip(CommsTooltip, TextAlignmentOptions.TopLeft);

				signalLinkIcon = new KsmGuiImage(signalMainIcon, Textures.ttSignalDirect);
				signalLinkIcon.Image.raycastTarget = false;
				signalLinkIcon.StaticLayout(20, 20, 0, 1);

				signalDataIcon = new KsmGuiImage(signalMainIcon, Textures.ttSignalData);
				signalDataIcon.Image.raycastTarget = false;
				signalDataIcon.StaticLayout(20, 20, 0, 1);

				Update();
			}

			private void OnVesselSelected()
			{
				manager.onVesselSelected?.Invoke(Vd);
			}

			private void ContextMenu()
			{
				KsmGuiContextMenu.Instance.Create(gotoButton);

				if (Vd.IsPersisted)
				{
					if (IsPinned)
						KsmGuiContextMenu.Instance.AddButton("Unpin", () => IsPinned = false);
					else
						KsmGuiContextMenu.Instance.AddButton("Pin", () => IsPinned = true);
				}

				bool ownedAndTracked = IsOwned && IsTracked;

				if (ownedAndTracked && !Vd.Vessel.isActiveVessel)
					KsmGuiContextMenu.Instance.AddButton("Switch to", () => GotoVessel.JumpToVessel(Vd.Vessel));

				if (!ownedAndTracked)
					KsmGuiContextMenu.Instance.AddButton("Start tracking", () => SpaceTracking.StartTrackingObject(Vd.Vessel));

				if (ownedAndTracked && Lib.IsFlight && !Vd.Vessel.isActiveVessel)
					KsmGuiContextMenu.Instance.AddButton("Target", () => GotoVessel.SetVesselAsTarget(Vd.Vessel));

				if (ownedAndTracked)
					KsmGuiContextMenu.Instance.AddButton("Rename", () => Vd.Vessel.RenameVessel());

				if (Lib.CanRecoverVessel(Vd.Vessel))
					KsmGuiContextMenu.Instance.AddButton("Recover", () => Lib.RecoverVesselPopup(Vd.Vessel));

				if (ownedAndTracked && !Vd.Vessel.loaded)
					KsmGuiContextMenu.Instance.AddButton(KsmString.Get.Format(Vd.Vessel.DiscoveryInfo.Level == DiscoveryLevels.Owned ? "Terminate" : "Stop tracking", KF.KolorOrange).End(), () => Lib.TerminateVesselPopup(Vd.Vessel));
			}

			private void Update()
			{
				IsTracked = Vd.Vessel.DiscoveryInfo.HaveKnowledgeAbout(DiscoveryLevels.StateVectors);
				IsOwned = Vd.Vessel.DiscoveryInfo.HaveKnowledgeAbout(DiscoveryLevels.Owned);
				IsSimulated = Vd.IsSimulated;

				if (IsTracked)
				{
					vesselName.Text = Vd.Vessel.DiscoveryInfo.displayName.Value;
				}
				else
				{
					vesselName.Text = KsmString.Get.Add("Class", KF.WhiteSpace, Vd.Vessel.DiscoveryInfo.size.Value, KF.WhiteSpace, "object").End();
				}

				gotoButton.SetIconTexture(GetVesselTypeIcon(Vd.Vessel));

				bodyName.Text = Vd.Vessel.mainBody.name;

				switch (Vd.Vessel.situation)
				{
					case Vessel.Situations.LANDED:
					case Vessel.Situations.SPLASHED:
					case Vessel.Situations.PRELAUNCH:
						situationIcon.SetIconTexture(Textures.ttLanded);
						break;
					case Vessel.Situations.FLYING:
						situationIcon.SetIconTexture(Textures.ttFlying);
						break;
					case Vessel.Situations.SUB_ORBITAL:
						situationIcon.SetIconTexture(Textures.ttSuborbit);
						break;
					case Vessel.Situations.ORBITING:
					case Vessel.Situations.DOCKED:
						situationIcon.SetIconTexture(Textures.ttOrbit);
						break;
					case Vessel.Situations.ESCAPING:
						situationIcon.SetIconTexture(Textures.ttEscape);
						break;
				}



				infoEnabled = Vd.IsSimulated;
				if (!infoEnabled)
				{
					infoIcon.SetIconTexture(null);
					infoIcon.SetIconColor(Color.clear);
				}
				else
				{
					infoTextures.Clear();

					if (Vd.InSunlight)
					{
						infoTextures.Add(InfoTexture.sunlight);
					}
					else
					{
						infoTextures.Add(InfoTexture.noSunlight);
					}

					if (Vd.EnvStorm)
					{
						infoTextures.Add(InfoTexture.storm);
					}

					if (Vd.EnvInnerBelt || Vd.EnvOuterBelt)
					{
						infoTextures.Add(InfoTexture.belt);
					}

					if (Vd.Habitat.radiationRate > 1.0 / 3600.0)
					{
						infoTextures.Add(InfoTexture.radHigh);
					}
					else if (Vd.Habitat.radiationRate > 0.15 / 3600.0)
					{
						infoTextures.Add(InfoTexture.radMed);
					}
					else if (Vd.Habitat.radiationRate > 0.0195 / 3600.0)
					{
						infoTextures.Add(InfoTexture.radLow);
					}

					if (Vd.VesselSituations.virtualBiomes.Contains(VirtualBiome.Reentry))
						infoTextures.Add(InfoTexture.plasma);

					// Cycle between each icon every second
					InfoTexture currentTexture = infoTextures[(int)(Time.unscaledTime % infoTextures.Count)];
					infoIcon.SetIconTexture(currentTexture.texture);
					infoIcon.SetIconColor(currentTexture.color);
				}

				ecEnabled = Vd.IsSimulated && Vd.ResHandler.ElectricCharge.Capacity > 0.0;
				if (!ecEnabled)
				{
					ecIcon.SetIconColor(Kolor.DarkGrey);
				}
				else
				{
					ecIcon.SetIconColor(Vd.ResHandler.ElectricCharge.Supply?.Kolor ?? Kolor.Green);
				}

				suppliesEnabled = Vd.IsSimulated;
				if (!suppliesEnabled)
				{
					suppliesIcon.SetIconColor(Kolor.DarkGrey);
				}
				else
				{
					int severityVal = 0;
					foreach (VesselResource resource in Vd.ResHandler.Resources)
					{
						if (!resource.IsSupply || resource == Vd.ResHandler.ElectricCharge)
							continue;

						severityVal = Math.Max(severityVal, (int)resource.Supply.Severity);
					}

					Severity severity = (Severity)severityVal;

					switch (severity)
					{
						case Severity.warning: suppliesIcon.SetIconColor(Kolor.Yellow); break;
						case Severity.danger: suppliesIcon.SetIconColor(Kolor.Red); break;
						default: suppliesIcon.SetIconColor(Kolor.Green); break;
					}
				}

				rulesEnabled = Vd.IsSimulated && Vd.CrewCount > 0;
				if (!rulesEnabled)
				{
					rulesIcon.SetIconColor(Kolor.DarkGrey);
					rulesIcon.SetTooltipEnabled(false);
				}
				else
				{
					rulesIcon.SetTooltipEnabled(true);
					KerbalRule.WarningState warning = KerbalRule.WarningState.none;
					foreach (KerbalData kd in Vd.Crew)
					{
						foreach (KerbalRule rule in kd.rules)
						{
							if (rule.State > warning)
							{
								warning = rule.State;
							}
						}
					}

					switch (warning)
					{
						case KerbalRule.WarningState.none: rulesIcon.SetIconColor(Kolor.Green); break;
						case KerbalRule.WarningState.warning: rulesIcon.SetIconColor(Kolor.Yellow); break;
						case KerbalRule.WarningState.danger: rulesIcon.SetIconColor(Kolor.Red); break;
					}
				}


				signalEnabled = Vd.IsSimulated && Vd.Connection.HasActiveAntenna;
				if (!signalEnabled)
				{
					signalMainIcon.SetIconTexture(Textures.ttSignalFull);
					signalMainIcon.SetIconColor(Kolor.DarkGrey);
					signalLinkIcon.Enabled = false;
					signalDataIcon.Enabled = false;
				}
				else
				{
					if (!Vd.Connection.Linked)
					{
						signalMainIcon.SetIconColor(Lib.KolorRed);
						signalLinkIcon.Enabled = false;
						signalDataIcon.Enabled = false;

						switch (Vd.Connection.Status)
						{
							case LinkStatus.no_link:
								signalMainIcon.SetIconTexture(Textures.ttSignalFull);
								break;
							case LinkStatus.plasma:
								signalMainIcon.SetIconTexture(Textures.ttPlasma);
								break;
							case LinkStatus.storm:
								signalMainIcon.SetIconTexture(Textures.ttStorm);
								break;
						}
					}
					else
					{
						signalLinkIcon.Enabled = true;
						signalDataIcon.Enabled = true;

						if (Vd.ConnectionInfo.Strength < 0.2)
						{
							signalMainIcon.SetIconTexture(Textures.ttSignalLow);
							signalMainIcon.SetIconColor(Lib.KolorRed);
						}
						else if (Vd.ConnectionInfo.Strength < 0.5)
						{
							signalMainIcon.SetIconTexture(Textures.ttSignalMid);
							signalMainIcon.SetIconColor(Lib.KolorYellow);
						}
						else
						{
							signalMainIcon.SetIconTexture(Textures.ttSignalFull);
							signalMainIcon.SetIconColor(Lib.KolorGreen);
						}

						switch (Vd.Connection.Status)
						{
							case LinkStatus.direct_link:
								signalLinkIcon.SetIconTexture(Textures.ttSignalDirect);
								break;
							case LinkStatus.indirect_link:
								signalLinkIcon.SetIconTexture(Textures.ttSignalRelay);
								break;
						}

						if (Vd.Connection.DataRate == 0.0)
						{
							signalDataIcon.SetIconTexture(Textures.ttSignalNoData);
							signalDataIcon.SetIconColor(Lib.KolorRed);
						}
						else
						{
							signalDataIcon.SetIconTexture(Textures.ttSignalData);

							if (Vd.filesTransmitted.Count > 0)
							{
								signalDataIcon.SetIconColor(Lib.KolorScience);
							}
							else if (Vd.Connection.DataRate < 0.01)
							{
								signalDataIcon.SetIconColor(Lib.KolorYellow);
							}
							else
							{
								signalDataIcon.SetIconColor(Lib.KolorGreen);
							}
						}
					}
				}
			}

			private string VesselTooltip()
			{
				if (IsTracked)
				{
					KsmString ks = KsmString.Get;
					ks.Format(Vd.Vessel.DiscoveryInfo.displayName.Value, KF.KolorYellow, KF.Bold, KF.BreakAfter);
					ks.Info("Mission time", KF.ReadableDuration(Planetarium.GetUniversalTime() - Vd.Vessel.launchTime), KF.Bold);
					return ks.End();
				}
				else
				{
					return DiscoveryInfo.GetSizeClassDescription(Vd.Vessel.DiscoveryInfo.objectSize);
				}
			}

			private string SituationTooltip()
			{
				KsmString ks = KsmString.Get;

				ks.Info("Body", Vd.Vessel.mainBody.name, 60);

				switch (Vd.Vessel.situation)
				{
					case Vessel.Situations.LANDED:
					case Vessel.Situations.SPLASHED:
					case Vessel.Situations.PRELAUNCH:
						ks.Info("Status", "Landed", 60);
						break;
					case Vessel.Situations.FLYING:
						if (Vd.VesselSituations.virtualBiomes.Contains(VirtualBiome.Reentry))
							ks.Info("Status", "Reentry", 60);
						else
							ks.Info("Status", "Flying", 60);
						break;
					case Vessel.Situations.SUB_ORBITAL:
						ks.Info("Status", "Suborbital", 60);
						break;
					case Vessel.Situations.ORBITING:
						ks.Info("Status", "Orbiting", 60);
						break;
					case Vessel.Situations.ESCAPING:
						ks.Info("Status", "Escaping", 60);
						break;
				}

				ks.Info("Altitude", KF.ReadableDistance(Vd.Vessel.altitude), KF.Bold, 60);

				if (Vd.IsSimulated)
				{
					if (!string.IsNullOrEmpty(Vd.VesselSituations.BiomeTitle))
					{
						ks.Info("Biome", Vd.VesselSituations.BiomeTitle, 60);
					}

					ks.Format(KF.BreakAfter);
					ks.Format("Science situations", KF.Center, KF.KolorYellow, KF.Bold, KF.BreakAfter);

					ks.Format(Vd.VesselSituations.FirstSituation.ScienceSituationTitle, KF.List);
					for (int i = 0; i < Vd.VesselSituations.virtualBiomes.Count; i++)
					{
						if (Vd.VesselSituations.virtualBiomes[i] == VirtualBiome.NoBiome)
							continue;

						ks.Format(Vd.VesselSituations.virtualBiomes[i].Title(), KF.List);
					}
				}

				return ks.End();
			}

			private string InfoTooltip()
			{
				if (!infoEnabled)
					return null;

				KsmString ks = KsmString.Get;

				if (Vd.InSunlight)
					ks.Format("In sunlight", KF.Bold, KF.KolorGreen, KF.BreakAfter);
				else
					ks.Format("In shadow", KF.Bold, KF.KolorYellow, KF.BreakAfter);

				ks.Info("Radiation", KF.ReadableRadiation(Vd.EnvRadiation), KF.Bold);

				if (Vd.CrewCount > 0)
				{
					ks.Info("Hab radiation", KF.ReadableRadiation(Vd.Habitat.radiationRate), KF.Bold);
				}

				foreach (InfoTexture infoTexture in infoTextures)
				{
					if (!string.IsNullOrEmpty(infoTexture.tooltipText))
					{
						ks.Format(infoTexture.tooltipText, KF.BreakBefore);
					}
				}

				return ks.End();
			}

			private string EcTooltip()
			{
				if (!ecEnabled)
					return null;

				KsmString ks = KsmString.Get;

				if (Vd.SolarPanelsAverageExposure >= 0.0)
				{
					ks.Info("Solar panels exposure", Vd.SolarPanelsAverageExposure.ToString("P1"), KF.Color(Vd.SolarPanelsAverageExposure < 0.2, Kolor.Orange), KF.Bold);
				}

				Vd.ResHandler.ElectricCharge.BrokerListTooltip(ks);

				return ks.End();
			}

			private string SuppliesTooltip()
			{
				if (!suppliesEnabled)
					return null;

				KsmString ks = KsmString.Get;

				ks.AlignLeft();

				ks.Format("Resource", KF.Bold)
					.Format("Level", KF.Bold, KF.Position(100))
					.Format("Depletion", KF.Bold, KF.Position(175))
					.Break();

				int resCount = 0;

				foreach (VesselResource handlerResource in Vd.ResHandler.Resources)
				{
					if (handlerResource is VesselResourceKSP resource && resource.Capacity > 0.0 && resource.Visible)
					{
						resCount++;
						if (resCount > 10)
						{
							continue;
						}

						ks.Add(resource.Title.Length > 15 ? resource.Abbreviation : resource.Title);
						ks.Format(resource.Level.ToString("P1"), KF.Position(100));

						//if (resource.AvailabilityFactor > 0.0 && resource.AvailabilityFactor < 1.0)
						//{
						//	ks.Format(KF.Concat(resource.AvailabilityFactor.ToString("P0"), KF.WhiteSpace, "availability"),
						//		KF.Color(resource.CriticalConsumptionSatisfied, Kolor.Yellow, Kolor.Orange), KF.Position(225));
						//}
						//else if (resource.AvailabilityFactor == 0.0)
						//{
						//	ks.Format(resource.DepletionInfo, KF.KolorRed, KF.Position(175));
						//}
						//else
						//{
							ks.Format(resource.DepletionInfo, KF.Position(175));
						//}
						
						ks.Break();
					}
				}

				if (resCount > 10)
				{
					ks.Format(KF.Concat((resCount - 10).ToString(), KF.WhiteSpace, "more..."), KF.Italic, KF.KolorLightGrey);
				}

				return ks.End();
			}

			private class RulesTooltip : KsmGuiVerticalLayout
			{
				private const int nameColumnWidth = 100;
				private const int ruleColumnWidth = 35;

				private readonly List<KerbalEntry> kerbalEntries = new List<KerbalEntry>();
				private readonly VesselData vd;

				public RulesTooltip(VesselData vd) : base(null)
				{
					this.vd = vd;

					SetUpdateAction(Update);

					KsmGuiBase header = new KsmGuiBase(this);
					header.SetLayoutElement(false, false, nameColumnWidth + (KerbalRuleDefinition.definitions.Count * ruleColumnWidth), 18);

					for (int i = 0; i < KerbalRuleDefinition.definitions.Count; i++)
					{
						KsmGuiImage icon = new KsmGuiImage(header, KerbalRuleDefinition.definitions[i].icon, 18, 18);
						icon.StaticLayout(ruleColumnWidth, 18, nameColumnWidth + (i * ruleColumnWidth));
					}

					Update();
				}

				public void Update()
				{

					bool changed = false;
					for (int i = 0; i < vd.Crew.Count; i++)
					{
						if (kerbalEntries.Count < i + 1)
						{
							changed = true;
							kerbalEntries.Add(new KerbalEntry(this, vd.Crew[i]));
						}
						else if (kerbalEntries[i].kd != vd.Crew[i])
						{
							changed = true;
							kerbalEntries[i].Destroy();
							kerbalEntries[i] = new KerbalEntry(this, vd.Crew[i]);
						}
					}

					while (kerbalEntries.Count > vd.Crew.Count)
					{
						changed = true;
						int index = kerbalEntries.Count - 1;
						kerbalEntries[index].Destroy();
						kerbalEntries.RemoveAt(kerbalEntries.Count - 1);
					}


					if (changed)
					{
						foreach (KerbalEntry entry in kerbalEntries)
						{
							entry.SetColor(entry.TopTransform.GetSiblingIndex() % 2 == 0 ? Color.clear : Kolor.NearBlack.color);
						}

						RebuildLayout();
					}
				}

				private class KerbalEntry : KsmGuiBase
				{
					public KerbalData kd;
					private KsmGuiText name;
					private List<RuleEntry> rules = new List<RuleEntry>();

					public KerbalEntry(KsmGuiBase parent, KerbalData kd) : base(parent)
					{
						this.kd = kd;

						SetLayoutElement(false, false, nameColumnWidth + (kd.rules.Count * ruleColumnWidth), 18);

						name = new KsmGuiText(this, kd.stockKerbal.displayName, TextAlignmentOptions.Left, false, TextOverflowModes.Ellipsis);
						name.StaticLayout(100, 18);

						for (int i = 0; i < kd.rules.Count; i++)
						{
							KerbalRule rule = kd.rules[i];
							RuleEntry entry = new RuleEntry(this, rule);
							entry.StaticLayout(ruleColumnWidth, 18, nameColumnWidth + (i * ruleColumnWidth));
							rules.Add(entry);
						}
					}

					private class RuleEntry : KsmGuiText
					{
						private KerbalRule rule;
						public RuleEntry(KsmGuiBase parent, KerbalRule rule) : base(parent, rule.Definition.title, TextAlignmentOptions.Center, false, TextOverflowModes.Truncate)
						{
							this.rule = rule;
							SetUpdateAction(Update);
							Update();
						}

						private void Update()
						{
							KsmString ks = KsmString.Get;

							Kolor kolor;

							if (rule.Level > rule.Definition.dangerThreshold)
								kolor = Kolor.Red;
							else if (rule.Level > rule.Definition.warningThreshold)
								kolor = Kolor.Yellow;
							else
								kolor = Kolor.White;

							if (rule.ChangeRate <= 0.0)
								ks.Format(rule.Level, "P0", KF.Color(kolor));
							else
								ks.Format(KF.ReadableDuration(rule.TimeToMaxValue, KF.Precision.Tiny), KF.Color(kolor));

							Text = ks.End();
						}
					}
				}
			}

			private string CommsTooltip()
			{
				if (!signalEnabled)
					return null;

				KsmString ks = KsmString.Get;

				if (!Vd.Connection.Linked)
				{
					ks.Format("No signal", KF.KolorOrange, KF.Bold);

					switch (Vd.Connection.Status)
					{
						case LinkStatus.plasma:
							ks.Format("Communications blackout during reentry", KF.BreakBefore);
							break;
						case LinkStatus.storm:
							ks.Format("Communications blackout during solar storm", KF.BreakBefore);
							break;
					}
				}
				else
				{
					if (Vd.ConnectionInfo.Strength < 0.2)
						ks.Info("Signal strength", Vd.Connection.Strength.ToString("P2"), KF.KolorOrange, KF.Bold, 100);
					else if (Vd.ConnectionInfo.Strength < 0.5)
						ks.Info("Signal strength", Vd.Connection.Strength.ToString("P2"), KF.KolorYellow, KF.Bold, 100);
					else
						ks.Info("Signal strength", Vd.Connection.Strength.ToString("P2"), KF.KolorGreen, KF.Bold, 100);

					switch (Vd.Connection.Status)
					{
						case LinkStatus.direct_link:
							ks.Info("Link type", "direct", 100);
							break;
						case LinkStatus.indirect_link:
							ks.Info("Link type", "relayed", 100);
							break;
					}

					if (Vd.Connection.DataRate == 0.0)
					{
						ks.Info("Max data rate", Local.Generic_NONE, KF.KolorOrange, KF.Bold, 100);
					}
					else
					{
						ks.Info("Max data rate", Lib.HumanReadableDataRate(Vd.Connection.DataRate), 100);
					}

					if (Vd.filesTransmitted.Count == 0)
					{
						ks.Info("Transmitting", Local.UI_telemetry, 100);
					}
					else
					{
						ks.Info("Transmitting", KF.Concat(Vd.filesTransmitted.Count.ToString(), Vd.filesTransmitted.Count > 1 ? " files" : " file"), KF.Bold, 100);
					}
				}

				return ks.End();
			}

			private Texture2D GetVesselTypeIcon(Vessel vessel)
			{
				switch (vessel.vesselType)
				{
					case VesselType.Debris:
						return Textures.vesselTypeDebris;
					case VesselType.SpaceObject:
						return Textures.vesselTypeSpaceObj;
					case VesselType.Probe:
						return Textures.vesselTypeProbe;
					case VesselType.Relay:
						return Textures.vesselTypeCommsRelay;
					case VesselType.Rover:
						return Textures.vesselTypeRover;
					case VesselType.Lander:
						return Textures.vesselTypeLander;
					case VesselType.Ship:
						return Textures.vesselTypeShip;
					case VesselType.Plane:
						return Textures.vesselTypeAircraft;
					case VesselType.Station:
						return Textures.vesselTypeStation;
					case VesselType.Base:
						return Textures.vesselTypeBase;
					case VesselType.DeployedScienceController:
					case VesselType.DeployedSciencePart:
						return Textures.vesselTypeDeployScience;
					default:
						return Textures.vesselTypeSpaceObj;
				}
			}
		}

		private class BodyEntry : KsmGuiVerticalLayout
		{
			private VesselsManager manager;

			public CelestialBody Body { get; private set; }
			public BodyEntry ParentBody { get; private set; }
			public List<BodyEntry> ChildBodies { get; private set; } = new List<BodyEntry>();
			public List<VesselEntry> Vessels { get; private set; } = new List<VesselEntry>();
			public bool IsHeaderVisible { get; set; }

			private KsmGuiBase header;
			public KsmGuiVerticalLayout vesselsLayout;
			public KsmGuiVerticalLayout childsLayout;

			public RectTransform ChildBodiesParent => childsLayout.TopTransform;
			public RectTransform VesselsParent => vesselsLayout.TopTransform;

			public BodyEntry(KsmGuiBase parent, VesselsManager manager, CelestialBody body, BodyEntry parentEntry) : base(parent)
			{
				this.manager = manager;
				this.Body = body;
				this.ParentBody = parentEntry;
				SetLayoutElement(true, true);
				SetUpdateAction(Update);

				KsmString bodyName = KsmString.Get;
				bodyName.Format(Body.name, KF.KolorYellow, KF.Bold);
				BodyEntry nextParent = ParentBody;
				while (nextParent != null)
				{
					bodyName.Insert(0, nextParent.Body.name + " :: ");
					nextParent = nextParent.ParentBody;
				}

				header = new KsmGuiBase(this);
				header.SetLayoutElement(true, false, -1, -1, -1, 18);
				header.SetColor(Color.black);
				KsmGuiText headerText = new KsmGuiText(header, bodyName.End());
				headerText.StaticLayoutStretchInParent();

				vesselsLayout = new KsmGuiVerticalLayout(this);
				childsLayout = new KsmGuiVerticalLayout(this);
			}

			private void Update()
			{
				foreach (VesselEntry vessel in Vessels)
				{
					if (manager.filterValues.IsTypeVisible(vessel.Vd.Vessel.vesselType))
					{
						header.Enabled = true;
						return;
					}
				}

				header.Enabled = false;
			}
		}
	}
}
