using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using KERBALISM.KsmGui;
using KSP.Localization;
using KSP.UI;
using KSP.UI.TooltipTypes;
using Steamworks;
using TMPro;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;

namespace KERBALISM
{
	public class VesselSummaryUI : KsmGuiVerticalLayout
	{
		private const int contentWidth = 360;
		public static int Width => contentWidth + 10;
		private const int crewNameColumnWidth = 100;

		private bool isPopup;
		private bool isEditor;
		private VesselDataBase vd;
		private VesselData vdFlight;

		private KsmGuiHorizontalLayout summmarySpace;

		private KsmGuiText signal; // "45 %, x.xx kB/s", tooltip : current distance, max distance, control path list
		private KsmGuiTextButton transmit; // "X files, x.xx kB/s" / "telemetry", tooltip : total science transmitted, list of files / rate
		private KsmGuiText storedData; // "X/X Mb", tooltip : science value
		private KsmGuiText samples; // "X/X", tooltip : science value, weight

		private KsmGuiText bodyAndBiome; // "Kerbin Highlands", tooltip : full biome name
		private KsmGuiText situations; // "Space high (+2)", tooltip : other situations
		private KsmGuiText temperature; // "326 K", tooltip : flux details
		private KsmGuiText radiation; // "1.2 rad", tooltip : rad sources details


		private KsmGuiVerticalLayout crewSpace;
		private KsmGuiVerticalScrollView crewScrollView;
		private List<KerbalEntry> kerbalEntries = new List<KerbalEntry>();

		private KsmGuiVerticalLayout vesselSpace;
		private KsmGuiBase solarExposure;
		private KsmGuiText solarExposureText;
		private KsmGuiHorizontalLayout habitatSpace;

		private KsmGuiText habLivingSpace; // living space comfort % -> tooltip : pressurized volume, volume/crew, pressure
		private KsmGuiText habGravity; // gravity comfort % -> tooltip : planetary G, gravity rings G, gravity rings seats
		private KsmGuiText habExercice; // exercice comfort % -> tooltip : available seats
		private KsmGuiText habRadiation; // habitat radiation -> tooltip : high radiation, low radiation, shielding %, blocked storm radiation...
		private KsmGuiText habStormProtection;
		private KsmGuiText habComforts; // extra comforts % : firm ground, not alone, call home, panorama, mess room, plants, tv...
		private KsmGuiText habCO2; // hab CO2
		private KsmGuiText habPressure; // hab pressure

		private KsmGuiTextButton habRadShelterToggle; // button : toggle auto-radiation shelters
		private KsmGuiTextButton habRadShelterConfigure; // buttons : configure radiation shelters


		private KsmGuiVerticalLayout suppliesSpace;
		private KsmGuiVerticalScrollView suppliesScrollView;
		private List<SupplyEntry> suppliesEntries = new List<SupplyEntry>();


		private class KerbalEntry : KsmGuiBase
		{
			private class RuleEntry : KsmGuiText
			{
				private StringBuilder sb = new StringBuilder();
				private KerbalRule rule;
				public RuleEntry(KsmGuiBase parent, KerbalRule rule) : base(parent, rule.Definition.title, null, TextAlignmentOptions.Center, false, TextOverflowModes.Truncate)
				{
					this.rule = rule;
					SetUpdateAction(Update);
					SetTooltipText(string.Empty, TextAlignmentOptions.TopLeft, -1f, CreateTooltip);
				}

				private void Update()
				{
					sb.Clear();

					if (rule.Level > rule.Definition.dangerThreshold)
						sb.Append(Lib.Color(rule.Level.ToString("P1"), Lib.Kolor.Red));
					else if (rule.Level > rule.Definition.warningThreshold)
						sb.Append(Lib.Color(rule.Level.ToString("P1"), Lib.Kolor.Yellow));
					else
						sb.Append(rule.Level.ToString("P1"));


					if (rule.LevelChangeRate > 0.01)
						sb.Append(Lib.Color(" (++)", Lib.Kolor.Red, true));
					else if (rule.LevelChangeRate > 0.0)
						sb.Append(Lib.Color(" (+)", Lib.Kolor.Yellow, true));
					else if (rule.LevelChangeRate < 0.0)
						sb.Append(Lib.Color(" (-)", Lib.Kolor.Green, true));

					Text = sb.ToString();
				}

				private KsmGuiBase CreateTooltip()
				{
					return new RuleEntryTooltip(rule);
				}

				private class RuleEntryTooltip : KsmGuiVerticalLayout
				{
					private List<KsmGuiText> modifiers = new List<KsmGuiText>();
					private KsmGuiText currentRate;

					private KerbalRule rule;

					public RuleEntryTooltip(KerbalRule rule) : base(null)
					{
						this.rule = rule;
						SetUpdateAction(Update);

						// todo : parametrized localization string
						new KsmGuiText(this, Lib.BuildString(Lib.Color(rule.Definition.title, Lib.Kolor.Yellow, true), " ", "for", " ", Lib.Color(rule.KerbalData.stockKerbal.name, Lib.Kolor.Yellow, true)), null, TextAlignmentOptions.Top);

						currentRate = new KsmGuiText(this, string.Empty, null, TextAlignmentOptions.Top);

						new KsmGuiText(this, "\n" + Lib.Color("Modifiers", Lib.Kolor.Yellow, true), null, TextAlignmentOptions.Top);
						KsmGuiHorizontalLayout modifiersTable = new KsmGuiHorizontalLayout(this, 5);
						KsmGuiVerticalLayout modifiersNameColumn = new KsmGuiVerticalLayout(modifiersTable);
						KsmGuiVerticalLayout modifiersValueColumn = new KsmGuiVerticalLayout(modifiersTable);
						for (int i = 0; i < rule.Modifiers.Count; i++)
						{
							double rate = rule.Modifiers[i].currentRate / rule.MaxValue;
							new KsmGuiText(modifiersNameColumn, rule.Definition.modifiers[i].title, null, TextAlignmentOptions.TopRight);
							modifiers.Add(new KsmGuiText(modifiersValueColumn, string.Empty));
						}

						if (rule.MaxValueInfo.Count > 0)
						{
							new KsmGuiText(this, "\n" + Lib.Color("Kerbal bonuses", Lib.Kolor.Yellow), null, TextAlignmentOptions.Top);
							KsmGuiHorizontalLayout bonusTable = new KsmGuiHorizontalLayout(this, 5);
							KsmGuiVerticalLayout bonusNameColumn = new KsmGuiVerticalLayout(bonusTable);
							KsmGuiVerticalLayout bonusValueColumn = new KsmGuiVerticalLayout(bonusTable);
							foreach (string[] entry in rule.MaxValueInfo)
							{
								new KsmGuiText(bonusNameColumn, entry[0], null, TextAlignmentOptions.TopRight);
								new KsmGuiText(bonusValueColumn, entry[1]);
							}
						}
					}

					private void Update()
					{
						currentRate.Text = Lib.BuildString(
							"Rate of change : ", Lib.Color(rule.LevelChangeRate >= 0.0, Lib.HumanReadableRate(rule.LevelChangeRate, "P1", "", true), Lib.Kolor.NegRate, Lib.Kolor.PosRate), "\n",
							"100% reached in ", Lib.Color(Lib.HumanReadableDuration((rule.MaxValue - rule.Value) / rule.ChangeRate), Lib.Kolor.Green));

						for (int i = 0; i < modifiers.Count; i++)
						{
							double rate = rule.Modifiers[i].currentRate / rule.MaxValue;
							modifiers[i].Text = Lib.Color(rate > 0.0, Lib.HumanReadableRate(rate, "P1", "", true), Lib.Kolor.NegRate, Lib.Kolor.PosRate);
						}
					}
				}
			}


			public KerbalData kd;
			private KsmGuiText name;
			private List<RuleEntry> rules = new List<RuleEntry>();

			public KerbalEntry(KsmGuiBase parent, KerbalData kd) : base(parent)
			{
				this.kd = kd;

				SetLayoutElement(true, false, -1, 18);

				name = new KsmGuiText(this, kd.stockKerbal.displayName, null, TextAlignmentOptions.Left, false, TextOverflowModes.Ellipsis);
				name.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 0);
				name.TopTransform.SetSizeDelta(crewNameColumnWidth, 16);

				name.SetTooltipText(string.Empty, TextAlignmentOptions.Top, -1f, () => new KerbalStockTooltip(kd.stockKerbal));

				int ruleEntryWidth = (contentWidth - crewNameColumnWidth) / kd.rules.Count;

				for (int i = 0; i < kd.rules.Count; i++)
				{
					KerbalRule rule = kd.rules[i];
					RuleEntry entry = new RuleEntry(this, rule);
					rules.Add(entry);
					entry.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, crewNameColumnWidth + i * ruleEntryWidth, 0);
					entry.TopTransform.SetSizeDelta(ruleEntryWidth, 16);
				}
			}

			private class KerbalStockTooltip : KsmGuiText
			{
				private static StringBuilder sb = new StringBuilder();

				public KerbalStockTooltip(ProtoCrewMember pcm) : base(null)
				{
					TooltipController_CrewAC stockController = TopObject.AddComponent<TooltipController_CrewAC>();
					stockController.SetTooltip(pcm);
					stockController.enabled = false;

					sb.Clear();
					sb.Format(stockController.titleString, KF.KolorYellow, KF.Center, KF.Bold, KF.BreakAfter);
					sb.Info(Localizer.Format("#autoLOC_6002246"), KF.Concat(pcm.experienceLevel.ToString(), " / 5"));
					sb.Info(Localizer.Format("#autoLOC_900297"), pcm.courage.ToString("P0")); // courage
					sb.Info(Localizer.Format("#autoLOC_900298"), pcm.stupidity.ToString("P0"), KF.BreakAfter); // stupidity
					sb.Append(stockController.descriptionString);
					Text = sb.ToString();
				}
			}
		}

		private class SupplyEntry : KsmGuiHorizontalLayout
		{
			static ProfilerMarker profilerSB = new ProfilerMarker("SupplyEntry.Update.SB");
			static ProfilerMarker profilerSetText = new ProfilerMarker("SupplyEntry.Update.SetText");

			private static StringBuilder sb = new StringBuilder();
			public VesselKSPResource Resource { get; private set; }
			public Supply Supply { get; private set; }
			private KsmGuiText textComponent;

			public SupplyEntry(KsmGuiBase parent, VesselKSPResource resource, Supply supply = null) : base(parent)
			{
				this.Resource = resource;
				this.Supply = supply;
				SetLayoutElement(true, false, -1, -1, -1, 18);
				SetUpdateAction(Update);
				textComponent = new KsmGuiText(this, "", null, TextAlignmentOptions.Left);
				textComponent.SetLayoutElement(true, false, -1, -1, -1, 18);

				SetTooltipText(() => Resource.BrokerListTooltipTMP(false));

			}

			private void Update()
			{
				using (profilerSB.Auto())
				{
					sb.Clear();
					sb.Append(Resource.Title);
					sb.Format(Lib.HumanReadableAmountCompact(Resource.Amount), KF.Position(100));
					sb.Format(Resource.Level.ToString("P1"), KF.Position(160));

					sb.Format(KF.Position(220));
					bool showAvailabilityFactor = Resource.AvailabilityFactor > 0.0 && Resource.AvailabilityFactor < 1.0;

					if (showAvailabilityFactor)
					{
						sb.Format(Resource.AvailabilityFactor.ToString("P1"), KF.KolorRed, KF.Bold);
						sb.Concat(KF.WhiteSpace, "availability");
					}
					else
					{
						if (Resource.Rate > -1e-09 && Resource.Rate < 1e-09)
						{
							if (Resource.ResourceBrokers.Count == 0)
							{
								sb.Append("none");
							}
							else
							{
								sb.Format("stable", KF.KolorGreen);
							}
						}
						else
						{
							sb.Format(Lib.HumanReadableRate(Resource.Rate, "F3", string.Empty, true), KF.Color(Resource.Rate > 0.0, Kolor.PosRate, Kolor.NegRate));
						}
					}

					if (!showAvailabilityFactor)
					{
						sb.Append("<pos=280px>");
						double depletion = Resource.Depletion;
						if (depletion > Lib.SecondsInYearExact * 100.0) // more than 100 years = perpetual
						{
							sb.Append(Lib.Color(Local.Generic_PERPETUAL, Lib.Kolor.Green));
						}
						else if (depletion == 0.0)
						{
							sb.Append(Lib.Color(Local.Monitor_depleted, Lib.Kolor.Orange));
						}
						else
						{
							if (Supply != null && Resource.Level < Supply.levelThreshold)
								sb.Append(Lib.Color(Lib.HumanReadableDuration(depletion), Lib.Kolor.Orange));
							else
								sb.Append(Lib.Color(Lib.HumanReadableDuration(depletion), Lib.Kolor.Green));
						}
					}
				}

				using (profilerSetText.Auto())
				{
					textComponent.Text = sb.ToString();
				}
			}
		}

		public VesselSummaryUI(KsmGuiBase parent, bool isPopup, VesselDataBase vesselDataBase) : base(parent, 5, 0, 0, 0, 0, TextAnchor.UpperLeft)
		{
			vd = vesselDataBase;
			this.isPopup = isPopup;

			if (vd is VesselData)
			{
				vdFlight = (VesselData)vd;
				isEditor = false;
			}
			else
			{
				isEditor = true;
			}

			KsmGuiHeader topHeader = new KsmGuiHeader(this, vesselDataBase.VesselName);
			new KsmGuiIconButton(topHeader, Textures.KsmGuiTexHeaderClose, () => ((KsmGuiWindow)parent).Close(), Local.SCIENCEARCHIVE_closebutton); //"close"
																																					//topHeader.Enabled = isPopup;

			summmarySpace = new KsmGuiHorizontalLayout(this, 10);
			summmarySpace.SetLayoutElement(true, false, contentWidth);
			summmarySpace.SetUpdateAction(UpdateSummary);

			KsmGuiVerticalLayout commsAndScience = new KsmGuiVerticalLayout(summmarySpace, 5);
			commsAndScience.SetLayoutElement(false, false, -1, -1, contentWidth / 2 - 5);
			new KsmGuiHeader(commsAndScience, "COMMS & SCIENCE");
			KsmGuiVerticalLayout commsAndScienceContent = new KsmGuiVerticalLayout(commsAndScience, 0, 3);
			commsAndScienceContent.SetBoxColor();

			signal = new KsmGuiText(commsAndScienceContent, null, null, TextAlignmentOptions.TopLeft, false, TextOverflowModes.Ellipsis);  // "45 %, x.xx kB/s", tooltip : current distance, max distance, control path list
			signal.SetTooltipText(SignalTooltip, TextAlignmentOptions.TopLeft, 350f);
			transmit = new KsmGuiTextButton(commsAndScienceContent, null, null, null, TextAlignmentOptions.TopLeft, false, TextOverflowModes.Ellipsis); // "X files, x.xx kB/s" / "telemetry", tooltip : total science transmitted, list of files / rate
			transmit.SetTooltipText(TransmitTooltip);
			transmit.SetButtonOnClick(() => vd.DeviceTransmit = !vd.DeviceTransmit);
			storedData = new KsmGuiText(commsAndScienceContent, null, null, TextAlignmentOptions.TopLeft, false, TextOverflowModes.Ellipsis); // "X/X Mb", tooltip : science value
			samples = new KsmGuiText(commsAndScienceContent, null, null, TextAlignmentOptions.TopLeft, false, TextOverflowModes.Ellipsis); // "X/X", tooltip : science value, weight

			KsmGuiVerticalLayout environment = new KsmGuiVerticalLayout(summmarySpace, 5);
			environment.SetLayoutElement(false, false, -1, -1, contentWidth / 2 - 5);
			new KsmGuiHeader(environment, "ENVIRONMENT");
			KsmGuiVerticalLayout environmentContent = new KsmGuiVerticalLayout(environment, 0, 3);
			environmentContent.SetBoxColor();

			bodyAndBiome = new KsmGuiText(environmentContent, null, null, TextAlignmentOptions.TopLeft, false, TextOverflowModes.Ellipsis); // "Kerbin Highlands", tooltip : full biome name
			bodyAndBiome.UseEllipsisWithTooltip();
			situations = new KsmGuiText(environmentContent, null, null, TextAlignmentOptions.TopLeft, false, TextOverflowModes.Ellipsis); // "Space high (+2)", tooltip : other situations
			situations.SetTooltipText(SituationTooltip);
			temperature = new KsmGuiText(environmentContent, null, null, TextAlignmentOptions.TopLeft, false, TextOverflowModes.Ellipsis); // "326 K", tooltip : flux details
			temperature.SetTooltipText(TemperatureTooltip, TextAlignmentOptions.TopLeft);
			radiation = new KsmGuiText(environmentContent, null, null, TextAlignmentOptions.TopLeft, false, TextOverflowModes.Ellipsis); // "1.2 rad", tooltip : rad sources details
			radiation.SetTooltipText(string.Empty, TextAlignmentOptions.Top, -1f, () => new RadiationTooltip(this.vd));

			// CREW SPACE

			crewSpace = new KsmGuiVerticalLayout(this, 5);
			crewSpace.SetLayoutElement(true);
			crewSpace.SetUpdateAction(UpdateCrew);

			new KsmGuiHeader(crewSpace, "CREW");

			KsmGuiVerticalLayout crewSpaceContent = new KsmGuiVerticalLayout(crewSpace, 0, 3);
			crewSpaceContent.SetBoxColor();

			KsmGuiHorizontalLayout crewHeader = new KsmGuiHorizontalLayout(crewSpaceContent);
			KsmGuiBase titlespacer = new KsmGuiBase(crewHeader);
			titlespacer.SetLayoutElement(false, false, crewNameColumnWidth);
			foreach (KerbalRuleDefinition rule in Profile.rules)
			{
				KsmGuiBase spacer = new KsmGuiBase(crewHeader);
				spacer.SetLayoutElement(true, false, -1, 24);
				KsmGuiIcon icon = new KsmGuiIcon(spacer, rule.icon, null, 24, 24);
				icon.SetTooltipText(rule.TooltipText(), TextAlignmentOptions.Left, 250f);
				icon.TopTransform.anchorMin = new Vector2(0.5f, 0.5f);
				icon.TopTransform.anchorMax = new Vector2(0.5f, 0.5f);
				icon.TopTransform.sizeDelta = new Vector2(24f, 24f);
			}

			crewScrollView = new KsmGuiVerticalScrollView(crewSpaceContent, 0, 0, 0, 0, 0);
			crewScrollView.SetLayoutElement(true, false, -1, 200);
			crewScrollView.SetBackgroundColor(false);

			// VESSEL SPACE

			new KsmGuiHeader(this, "VESSEL");

			vesselSpace = new KsmGuiVerticalLayout(this, 5);
			vesselSpace.SetLayoutElement(true);
			vesselSpace.SetUpdateAction(UpdateVessel);

			// "solar panels average exposure"
			solarExposure = new KsmGuiBase(vesselSpace);
			solarExposure.SetLayoutElement(true, false, -1, -1, -1, 18);
			solarExposure.SetBoxColor();

			solarExposureText = new KsmGuiText(solarExposure, string.Empty, null, TextAlignmentOptions.Center, false, TextOverflowModes.Ellipsis);
			solarExposureText.NoLayoutStretchInParent();
			// Tooltip : "Exposure ignoring bodies occlusion" + "Won't change on unloaded vessels\nMake sure to optimize it before switching"
			solarExposureText.SetTooltipText(Lib.Bold(Local.TELEMETRY_Exposureignoringbodiesocclusion) + "\n" + Lib.Italic(Local.TELEMETRY_Exposureignoringbodiesocclusion_desc));

			// todo : solar storm average protection %

			// habitat space :
			// living space comfort % -> tooltip : pressurized volume, volume/crew, pressure
			// gravity comfort % -> tooltip : planetary G, gravity rings G, gravity rings seats
			// exercice comfort % -> tooltip : available seats
			// habitat radiation -> tooltip : high radiation, low radiation, shielding %, blocked storm radiation...
			// extra comforts % : firm ground, not alone, call home, panorama, mess room, plants, tv...
			// hab CO2
			// hab pressure
			// buttons : enable auto-radiation shelters / configure radiation shelters
			habitatSpace = new KsmGuiHorizontalLayout(vesselSpace, 5);
			habitatSpace.SetLayoutElement(true);

			KsmGuiVerticalLayout habCol1 = new KsmGuiVerticalLayout(habitatSpace, 0, 3);
			habCol1.SetLayoutElement(false, false, -1, -1, contentWidth / 2 - 5);
			habCol1.SetBoxColor();
			KsmGuiVerticalLayout habCol2 = new KsmGuiVerticalLayout(habitatSpace, 0, 3);
			habCol2.SetLayoutElement(false, false, -1, -1, contentWidth / 2 - 5);
			habCol2.SetBoxColor();

			habRadiation = new KsmGuiText(habCol1, null, null, TextAlignmentOptions.Left, false, TextOverflowModes.Truncate); // habitat radiation -> tooltip : high radiation, low radiation, shielding %, blocked storm radiation...
			habRadiation.SetTooltipText(HabRadiationTooltip, TextAlignmentOptions.TopLeft);
			habStormProtection = new KsmGuiText(habCol1, null, null, TextAlignmentOptions.Left, false, TextOverflowModes.Truncate);
			habStormProtection.SetTooltipText(Lib.BuildString("Storm radiation blocked at current vessel orientation", "\n", Lib.Italic("Won't change on unloaded vessels\nMake sure to optimize it before leaving the vessel.")));
			habPressure = new KsmGuiText(habCol1, null, null, TextAlignmentOptions.Left, false, TextOverflowModes.Truncate);
			habPressure.SetTooltipText(HabPressureTooltip);
			habCO2 = new KsmGuiText(habCol1, null, null, TextAlignmentOptions.Left, false, TextOverflowModes.Truncate); // hab CO2
			habCO2.SetTooltipText(HabCO2Tooltip);

			habLivingSpace = new KsmGuiText(habCol2, null, null, TextAlignmentOptions.Left, false, TextOverflowModes.Truncate); // living space comfort % -> tooltip : pressurized volume, volume/crew, pressure
			habLivingSpace.SetTooltipText(HabLivingSpaceTooltip);
			habGravity = new KsmGuiText(habCol2, null, null, TextAlignmentOptions.Left, false, TextOverflowModes.Truncate); // gravity comfort % -> tooltip : planetary G, gravity rings G, gravity rings seats
			habExercice = new KsmGuiText(habCol2, null, null, TextAlignmentOptions.Left, false, TextOverflowModes.Truncate); // exercice comfort % -> tooltip : available seats
			habComforts = new KsmGuiText(habCol2, null, null, TextAlignmentOptions.Left, false, TextOverflowModes.Truncate); // extra comforts % : firm ground, not alone, call home, panorama, mess room, plants, tv...
			habComforts.SetTooltipText(() => ComfortInfoBase.GetComfortsInfo(vesselDataBase.Habitat.comforts.Values), TextAlignmentOptions.TopLeft);

			KsmGuiHorizontalLayout shelterConfig = new KsmGuiHorizontalLayout(vesselSpace);
			shelterConfig.SetLayoutElement(true, false, -1, -1, -1, 18);
			shelterConfig.SetBoxColor();
			new KsmGuiText(shelterConfig, "Radiation shelter");
			new KsmGuiTextButton(shelterConfig, "Enabled", null);
			new KsmGuiTextButton(shelterConfig, "Auto", null);
			new KsmGuiTextButton(shelterConfig, "Configure", null);



			// SUPPLIES SPACE

			suppliesSpace = new KsmGuiVerticalLayout(this, 5);
			suppliesSpace.SetLayoutElement(true);
			suppliesSpace.SetUpdateAction(UpdateSupplies);
			suppliesSpace.SetBoxColor();
			new KsmGuiHeader(suppliesSpace, "SUPPLIES");
			KsmGuiVerticalLayout suppliesSpaceContent = new KsmGuiVerticalLayout(suppliesSpace, 0, 3);


			KsmGuiBase suppliesHeader = new KsmGuiBase(suppliesSpaceContent);
			suppliesHeader.SetLayoutElement(true, false, -1, -1, -1, 16);
			KsmGuiText supplyName = new KsmGuiText(suppliesHeader, Lib.Bold("Resource"));
			supplyName.TopTransform.SetAnchorsAndPosition(TextAnchor.UpperLeft, TextAnchor.UpperLeft, 0);
			supplyName.TopTransform.sizeDelta = new Vector2(100f, 16f);
			KsmGuiText supplyAmount = new KsmGuiText(suppliesHeader, Lib.Bold("Amount"));
			supplyAmount.TopTransform.SetAnchorsAndPosition(TextAnchor.UpperLeft, TextAnchor.UpperLeft, 100);
			supplyAmount.TopTransform.sizeDelta = new Vector2(60f, 16f);
			KsmGuiText supplyFull = new KsmGuiText(suppliesHeader, Lib.Bold("Level"));
			supplyFull.TopTransform.SetAnchorsAndPosition(TextAnchor.UpperLeft, TextAnchor.UpperLeft, 160);
			supplyFull.TopTransform.sizeDelta = new Vector2(60f, 16f);
			KsmGuiText supplyChange = new KsmGuiText(suppliesHeader, Lib.Bold("Rate"));
			supplyChange.TopTransform.SetAnchorsAndPosition(TextAnchor.UpperLeft, TextAnchor.UpperLeft, 220);
			supplyChange.TopTransform.sizeDelta = new Vector2(60f, 16f);
			KsmGuiText supplyETA = new KsmGuiText(suppliesHeader, Lib.Bold("Depletion"));
			supplyETA.TopTransform.SetAnchorsAndPosition(TextAnchor.UpperLeft, TextAnchor.UpperLeft, 280);
			supplyETA.TopTransform.sizeDelta = new Vector2(80f, 16f);

			suppliesScrollView = new KsmGuiVerticalScrollView(suppliesSpaceContent, 0, 0, 0, 0, 0);
			suppliesScrollView.SetLayoutElement(true, false, -1, 200);
			suppliesScrollView.SetBackgroundColor(false);
		}

		private void UpdateVessel()
		{
			if (vd.SolarPanelsAverageExposure >= 0.0)
			{
				if (!solarExposure.Enabled)
				{
					solarExposure.Enabled = true;
				}

				solarExposureText.Text = KsmString.Get
					.Add(Local.TELEMETRY_SolarPanelsAverageExposure, " : ")
					.Format(vd.SolarPanelsAverageExposure.ToString("P1"), KF.Color(vd.SolarPanelsAverageExposure < 0.2, Kolor.Orange), KF.Bold)
					.End();
			}
			else if (solarExposure.Enabled == true)
			{
				solarExposure.Enabled = false;
			}

			if (vd.Habitat.totalVolume == 0.0 && habitatSpace.Enabled)
			{
				habitatSpace.Enabled = false;
			}
			else if (vd.Habitat.totalVolume > 0.0 && !habitatSpace.Enabled)
			{
				habitatSpace.Enabled = true;
			}

			if (habitatSpace.Enabled)
			{
				habRadiation.Text = Lib.BuildString("Hab radiation", "<pos=50%>", Lib.HumanReadableRadiation(vd.Habitat.radiationRate, false, true)); // habitat radiation -> tooltip : high radiation, low radiation, shielding %, blocked storm radiation...
				habStormProtection.Text = Lib.BuildString("Sun shielding", "<pos=50%>", (1.0 - vd.Habitat.sunRadiationFactor).ToString("P1"));
				habPressure.Text = Lib.BuildString("Avg. pressure", "<pos=50%>", vd.Habitat.pressure.ToString("P1"));
				habCO2.Text = Lib.BuildString("CO2 level", "<pos=50%>", vd.Habitat.poisoningLevel.ToString("P2"));

				habLivingSpace.Text = Lib.BuildString("Living space", "<pos=50%>", Lib.HumanReadableVolume(vd.Habitat.volumePerCrew), " / kerbal"); // living space comfort % -> tooltip : pressurized volume, volume/crew, pressure
				habGravity.Text = Lib.BuildString("Gravity", "<pos=50%>", Math.Max(vd.Habitat.gravity, vd.Habitat.artificialGravity).ToString("0.00 g"));
				habExercice.Text = Lib.BuildString("Exercise", "<pos=50%>", vd.Habitat.comforts["exercise"].Level.ToString("P0")); // exercice comfort % -> tooltip : available seats
				habComforts.Text = Lib.BuildString("Comforts", "<pos=50%>", vd.Habitat.comfortsTotalBonus.ToString("P1"), " (", vd.Habitat.comfortsActiveCount.ToString(), ")"); // extra comforts % : firm ground, not alone, call home, panorama, mess room, plants, tv...
			}
		}

		private string HabRadiationTooltip()
		{
			KsmString ks = KsmString.Get;

			ks.Format("Habitat radiation protection", KF.KolorYellow, KF.Bold, KF.Center, KF.BreakAfter);
			ks.Info("Shielding level", (vd.Habitat.shieldingAmount / vd.Habitat.shieldingSurface).ToString("P0"));
			ks.Info("Ambiant radiation occlusion", vd.Habitat.radiationAmbiantOcclusion.ToString("P1"));

			if (vd.Habitat.radiationEmittersOcclusion > 0.0)
			{
				ks.Info("Emitters radiation occlusion", vd.Habitat.radiationEmittersOcclusion.ToString("P1"));
			}

			ks.Format("Radiation sources", KF.KolorYellow, KF.Bold, KF.Center, KF.BreakBefore, KF.BreakAfter);

			ks.Info("External radiation", Lib.HumanReadableRadiation(vd.EnvRadiation * (1.0 - vd.Habitat.radiationAmbiantOcclusion), false, true));
			if (vd.Habitat.emittersRadiation > 0.0)
				ks.Info("Local emitters", Lib.HumanReadableRadiation(vd.Habitat.emittersRadiation, false, true));
			if (vd.Habitat.activeRadiationShielding > 0.0)
				ks.Info("Active shielding", "-" + Lib.HumanReadableRadiation(vd.Habitat.activeRadiationShielding, false, false));

			return ks.End();
		}

		private string HabPressureTooltip()
		{
			KsmString ks = KsmString.Get;

			ks.Info("Pressurized volume", Lib.HumanReadableVolume(vd.Habitat.pressurizedVolume));
			if (vd.ResHandler.TryGetResource(Settings.HabitatAtmoResource, out VesselKSPResource atmoResource))
			{
				ks.Format(atmoResource.BrokerListTooltipTMP(), KF.BreakBefore);
			}

			return ks.End();
		}

		private string HabCO2Tooltip()
		{
			if (vd.ResHandler.TryGetResource(Settings.HabitatWasteResource, out VesselKSPResource wasteResource))
			{
				return wasteResource.BrokerListTooltipTMP();
			}

			return string.Empty;
		}

		private string HabLivingSpaceTooltip()
		{
			KsmString ks = KsmString.Get;

			ks.Info("Total volume", Lib.HumanReadableVolume(vd.Habitat.totalVolume));
			ks.Info("Enabled and pressurized volume", Lib.HumanReadableVolume(vd.Habitat.livingVolume));

			return ks.End();
		}

		private void UpdateSupplies()
		{
			int resCount = 0;
			bool changed = false;
			foreach (VesselResource handlerResource in vd.ResHandler.Resources)
			{
				if (handlerResource is VesselKSPResource resource && resource.Capacity > 0.0 && resource.Visible)
				{
					Supply supply = Profile.supplies.Find(s => s.resource == resource.Name);

					if (suppliesEntries.Count < resCount + 1)
					{
						changed = true;
						SupplyEntry entry = new SupplyEntry(suppliesScrollView, resource, supply);
						suppliesEntries.Add(entry);
					}
					else if (suppliesEntries[resCount].Resource.Name != resource.Name)
					{
						changed = true;
						suppliesEntries[resCount].TopObject.DestroyGameObject();
						SupplyEntry entry = new SupplyEntry(suppliesScrollView, resource, supply);
						suppliesEntries[resCount] = entry;
					}

					resCount++;
				}
			}

			while (suppliesEntries.Count > resCount)
			{
				changed = true;
				int index = suppliesEntries.Count - 1;
				suppliesEntries[index].TopObject.DestroyGameObject();
				suppliesEntries.RemoveAt(suppliesEntries.Count - 1);
			}

			if (changed)
			{
				foreach (SupplyEntry entry in suppliesEntries)
				{
					if (entry.Supply == null)
					{
						entry.MoveAsLastChild();
					}
				}

				foreach (SupplyEntry entry in suppliesEntries)
				{
					entry.SetColor(entry.TopTransform.GetSiblingIndex() % 2 == 0 ? KsmGuiStyle.boxColor : Color.clear);
				}

				suppliesScrollView.LayoutElement.preferredHeight = Math.Min(resCount * 18f + 5f, 165f);

				RebuildLayout();
			}
		}

		private void UpdateCrew()
		{
			bool changed = false;
			for (int i = 0; i < vd.Crew.Count; i++)
			{
				if (kerbalEntries.Count < i + 1)
				{
					changed = true;
					kerbalEntries.Add(new KerbalEntry(crewScrollView, vd.Crew[i]));
				}
				else if (kerbalEntries[i].kd != vd.Crew[i])
				{
					changed = true;
					kerbalEntries[i].TopObject.DestroyGameObject();
					kerbalEntries[i] = new KerbalEntry(crewScrollView, vd.Crew[i]);
				}
			}

			while (kerbalEntries.Count > vd.Crew.Count)
			{
				changed = true;
				int index = kerbalEntries.Count - 1;
				kerbalEntries[index].TopObject.DestroyGameObject();
				kerbalEntries.RemoveAt(kerbalEntries.Count - 1);
			}


			if (changed)
			{
				crewScrollView.LayoutElement.preferredHeight = Math.Min(vd.Crew.Count * 18f + 5f, 105f);

				foreach (KerbalEntry entry in kerbalEntries)
				{
					entry.SetColor(entry.TopTransform.GetSiblingIndex() % 2 == 0 ? KsmGuiStyle.boxColor : Color.clear);
				}

				RebuildLayout();
			}
		}

		private void UpdateSummary()
		{
			KsmString ks;
			int commsLabelColumnWidth = 60;

			if (vd.Crew.Count == 0)
			{
				if (crewSpace.Enabled)
				{
					crewSpace.Enabled = false;
				}
			}
			else if (!crewSpace.Enabled)
			{
				crewSpace.Enabled = true;
			}

			if (!vd.ConnectionInfo.Linked)
			{
				signal.Text = KsmString.Get.Info("Signal", "No connecton", KF.KolorOrange, KF.Bold, commsLabelColumnWidth).End();
			}
			else
			{
				ks = KsmString.Get;
				KF color;
				if (vd.ConnectionInfo.Strength < 0.05)
					color = KF.KolorRed;
				else if (vd.ConnectionInfo.Strength < 0.2)
					color = KF.KolorOrange;
				else
					color = KF.KolorGreen;

				ks.Add("Signal");
				ks.Format(vd.ConnectionInfo.Strength, "P1", color, KF.Bold, KF.Position(commsLabelColumnWidth));

				if (vd.ConnectionInfo.DataRate > 0.0)
					ks.Add(" (", Lib.HumanReadableDataRate(vd.ConnectionInfo.DataRate), ")");

				signal.Text = ks.End();
			}

			ks = KsmString.Get;
			ks.Add("Upload");
			ks.Format(KF.Position(commsLabelColumnWidth));

			if (!vd.DeviceTransmit)
			{
				ks.Format("Disabled", KF.KolorOrange, KF.Bold);
			}
			else
			{
				if (vd.ConnectionInfo.DataRate > 0.0)
				{
					if (isEditor)
					{
						ks.Format("Enabled", KF.KolorGreen);
					}
					else
					{
						ks.Add(vdFlight.filesTransmitted.Count.ToString(), KF.WhiteSpace, "files", " (", Lib.HumanReadableDataRate(vdFlight.filesTransmitted.Sum(i => i.transmitRate)), ")");
					}
				}
				else
				{
					ks.Format("Enabled", KF.KolorOrange);
				}
			}

			transmit.Text = ks.End();

			DriveHandler.GetDrivesInfo(vd, out int filesCount, out double filesSize, out double filesCapacity, out double filesScience,
				out int samplesCount, out int samplesSlots, out int slotsCapacity, out double samplesScience, out double samplesMass);

			ks = KsmString.Get;
			ks.Add("Data");
			ks.Format(KF.Position(commsLabelColumnWidth));
			if (filesCapacity == 0.0)
			{
				ks.Format("no drive", KF.KolorOrange);

			}
			else
			{
				ks.Add(Lib.HumanReadableDataSize(filesSize), "/", filesCapacity < 0.0 ? "unlimited" : Lib.HumanReadableDataSize(filesCapacity));

			}

			storedData.Text = ks.End();

			if (filesCount > 0)
				storedData.SetTooltipText(KsmString.Get.Add(filesCount.ToString(), " ", "file(s)", "\n", "Science value", " : ", Lib.HumanReadableScience(filesScience, true, true)).End());
			else
				storedData.SetTooltipText(string.Empty);


			ks = KsmString.Get;
			ks.Add("Samples");
			ks.Format(KF.Position(commsLabelColumnWidth));

			if (slotsCapacity == 0.0)
			{
				ks.Format("no storage", KF.KolorOrange);
			}
			else
			{
				ks.Add(samplesSlots.ToString(), "/", slotsCapacity < 0.0 ? "unlimited" : slotsCapacity.ToString(), KF.WhiteSpace, "slots");

				if (samplesMass > 0.0)
				{
					ks.Add(" (", Lib.HumanReadableMass(samplesMass), ")");
				}
			}

			samples.Text = ks.End();

			if (samplesCount > 0)
				samples.SetTooltipText(Lib.BuildString(samplesCount.ToString(), " ", "sample(s)", "\n", "Science value", " : ", Lib.HumanReadableScience(samplesScience, true, true)));
			else
				samples.SetTooltipText(string.Empty);

			int envLabelColumnWidth = 75;

			ks = KsmString.Get;
			ks.Add("Body").Format(vd.VesselSituations.BodyTitle, KF.Position(envLabelColumnWidth));
			if (!string.IsNullOrEmpty(vd.VesselSituations.BiomeTitle))
			{
				ks.Add(" (", vd.VesselSituations.BiomeTitle, ")");
			}

			bodyAndBiome.Text = ks.End();
			//if (bodyAndBiome.TextComponent.isTextTruncated)
			//	bodyAndBiome.SetTooltipText(sb.ToString());
			//else
			//	bodyAndBiome.SetTooltipText(string.Empty);

			ks = KsmString.Get;
			ks.Add("Situation").Format(vd.VesselSituations.FirstSituation.ScienceSituationTitle, KF.Position(envLabelColumnWidth));

			// ignore the "global" situation
			if (vd.VesselSituations.virtualBiomes.Count > 1)
				ks.Add(" (+" + (vd.VesselSituations.virtualBiomes.Count - 1) + ")");
			situations.Text = ks.End();

			temperature.Text = KsmString.Get.Add("Temperature").Format(KF.ReadableTemperature(vd.EnvTemperature), KF.Position(envLabelColumnWidth)).End();
			radiation.Text = KsmString.Get.Add("Radiation").Format(KF.ReadableRadiation(vd.EnvRadiation), KF.Position(envLabelColumnWidth)).End();

		}

		private string SignalTooltip()
		{
			if (isEditor)
				return null;

			if (vdFlight.Connection.control_path.Count == 0)
				return null;

			KsmString ks = KsmString.Get;
			ks.Format(KF.Concat("Control path", " :"), KF.Center, KF.BreakAfter);
			ks.Format("Strength", KF.Position(20)).Format("Target", KF.Position(120)).Format("Details", KF.Position(220), KF.BreakAfter);
			for (int i = 0; i < vdFlight.Connection.control_path.Count; i++)
			{
				ks.Format(i + 1)
					.Format(vdFlight.Connection.control_path[i][1], KF.Position(20))
					.Format(vdFlight.Connection.control_path[i][0], KF.Position(120))
					.Format(vdFlight.Connection.control_path[i][2], KF.Position(220), KF.BreakAfter);
			}

			return ks.End();
		}

		private string TransmitTooltip()
		{
			if (isEditor)
				return null;

			KsmString ks = KsmString.Get;

			if (vd.DeviceTransmit)
				ks.Format("Click to disable data transmissions", KF.Italic, KF.BreakAfter);
			else
				ks.Format("Click to enable data transmissions", KF.Italic, KF.BreakAfter);

			if (vdFlight.scienceTransmitted > 0.0)
			{
				ks.Info("Total science transmitted", Lib.HumanReadableScience(vdFlight.scienceTransmitted, true, true));
			}

			if (vdFlight.filesTransmitted.Count > 0)
			{
				ks.Add("Transmitting", " :");
				for (int i = 0; i < vdFlight.filesTransmitted.Count; i++)
				{
					ks.Add("> ")
						.Format(Lib.HumanReadableDataRate(vdFlight.filesTransmitted[i].transmitRate), KF.Position(80))
						.Format(Lib.Ellipsis(vdFlight.filesTransmitted[i].subjectData.FullTitle, 30), KF.Position(120))
						.Break();
				}
			}

			return ks.End();
		}

		private string SituationTooltip()
		{
			if (isEditor)
				return null;

			KsmString ks = KsmString.Get;

			ks.Format("Current situations", KF.KolorYellow, KF.Bold, KF.Center, KF.BreakAfter);

			ks.Format(vd.VesselSituations.FirstSituation.ScienceSituationTitle, KF.List);

			for (int i = 0; i < vd.VesselSituations.virtualBiomes.Count; i++)
			{
				ks.Format(vd.VesselSituations.virtualBiomes[i].Title(), KF.List);
			}

			return ks.End();
		}

		private string TemperatureTooltip()
		{
			return KsmString.Get
				.Format("Black body temperature at vessel position", KF.KolorYellow, KF.Bold, KF.Center, KF.BreakAfter)
				.Info("Total irradiance", KF.ReadableIrradiance(vd.IrradianceTotal), 100)
				.Info("Sun irradiance", KF.ReadableIrradiance(vd.IrradianceStarTotal), 100)
				.Info("Bodies albedo", KF.ReadableIrradiance(vd.IrradianceAlbedo), 100)
				.Info("Bodies core", KF.ReadableIrradiance(vd.IrradianceBodiesCore), 100)
				.Info("Bodies emissive", KF.ReadableIrradiance(vd.IrradianceBodiesEmissive), 100)
				.End();
		}

		private class RadiationTooltip : KsmGuiVerticalLayout
		{
			private VesselDataBase vd;

			private KsmGuiText atmoProtectionLabel;
			private KsmGuiText atmoProtectionValue;

			private KsmGuiText stormLabel;
			private KsmGuiText stormValue;

			private KsmGuiText innerLabel;
			private KsmGuiText innerValue;

			private KsmGuiText outerLabel;
			private KsmGuiText outerValue;

			private KsmGuiText pauseLabel;
			private KsmGuiText pauseValue;

			private KsmGuiText bodiesLabel;
			private KsmGuiText bodiesValue;

			private KsmGuiText starsLabel;
			private KsmGuiText starsValue;

			private KsmGuiText backgroundValue;

			public RadiationTooltip(VesselDataBase vd) : base(null)
			{
				SetUpdateAction(Update);
				this.vd = vd;

				new KsmGuiText(this, Lib.Color("Radiation sources", Lib.Kolor.Yellow, true), null, TextAlignmentOptions.Top);

				KsmGuiHorizontalLayout table = new KsmGuiHorizontalLayout(this, 5);
				KsmGuiVerticalLayout namesColumn = new KsmGuiVerticalLayout(table);
				KsmGuiVerticalLayout valuesColumn = new KsmGuiVerticalLayout(table);

				bodiesLabel = new KsmGuiText(namesColumn, "Nearby bodies", null, TextAlignmentOptions.TopRight);
				bodiesValue = new KsmGuiText(valuesColumn, string.Empty);

				starsLabel = new KsmGuiText(namesColumn, "Solar wind", null, TextAlignmentOptions.TopRight);
				starsValue = new KsmGuiText(valuesColumn, string.Empty);

				stormLabel = new KsmGuiText(namesColumn, "Solar storm", null, TextAlignmentOptions.TopRight);
				stormValue = new KsmGuiText(valuesColumn, string.Empty);

				innerLabel = new KsmGuiText(namesColumn, "Inner belt", null, TextAlignmentOptions.TopRight);
				innerValue = new KsmGuiText(valuesColumn, string.Empty);

				outerLabel = new KsmGuiText(namesColumn, "Outer belt", null, TextAlignmentOptions.TopRight);
				outerValue = new KsmGuiText(valuesColumn, string.Empty);

				new KsmGuiText(namesColumn, "Background", null, TextAlignmentOptions.TopRight);
				backgroundValue = new KsmGuiText(valuesColumn, string.Empty);

				pauseLabel = new KsmGuiText(namesColumn, "Magnetosphere", null, TextAlignmentOptions.TopRight);
				pauseValue = new KsmGuiText(valuesColumn, string.Empty);

				atmoProtectionLabel = new KsmGuiText(namesColumn, Lib.Color("Atmosphere protection", Lib.Kolor.Yellow), null, TextAlignmentOptions.TopRight);
				atmoProtectionValue = new KsmGuiText(valuesColumn, string.Empty);
			}

			private void Update()
			{
				if ((vd.EnvGammaTransparency < 1.0) != atmoProtectionLabel.Enabled)
				{
					atmoProtectionLabel.Enabled = !atmoProtectionLabel.Enabled;
					atmoProtectionValue.Enabled = !atmoProtectionValue.Enabled;
				}

				if (vd.EnvGammaTransparency < 1.0)
				{
					atmoProtectionValue.Text = (1.0 - vd.EnvGammaTransparency).ToString("P2");
				}

				if (vd.EnvStorm != stormLabel.Enabled)
				{
					stormLabel.Enabled = !stormLabel.Enabled;
					stormValue.Enabled = !stormValue.Enabled;
				}

				if (vd.EnvStorm)
				{
					stormValue.Text = Lib.HumanReadableRadiation(vd.EnvStormRadiation, false);
				}

				if (vd.EnvInnerBelt != innerLabel.Enabled)
				{
					innerLabel.Enabled = !innerLabel.Enabled;
					innerValue.Enabled = !innerValue.Enabled;
				}

				if (vd.EnvInnerBelt)
				{
					innerValue.Text = Lib.HumanReadableRadiation(vd.EnvRadiationInnerBelt, false);
				}

				if (vd.EnvOuterBelt != outerLabel.Enabled)
				{
					outerLabel.Enabled = !outerLabel.Enabled;
					outerValue.Enabled = !outerValue.Enabled;
				}

				if (vd.EnvOuterBelt)
				{
					outerValue.Text = Lib.HumanReadableRadiation(vd.EnvRadiationOuterBelt, false);
				}

				if ((vd.EnvRadiationMagnetopause < 0.0) != pauseLabel.Enabled)
				{
					pauseLabel.Enabled = !pauseLabel.Enabled;
					pauseValue.Enabled = !pauseValue.Enabled;
				}

				if (vd.EnvRadiationMagnetopause < 0.0)
				{
					pauseValue.Text = Lib.Color(Lib.BuildString("-", Lib.HumanReadableRadiation(Math.Abs(vd.EnvRadiationMagnetopause), false, false)), Lib.Kolor.Green);
				}

				if ((vd.EnvRadiationBodies > 0.0) != bodiesLabel.Enabled)
				{
					bodiesLabel.Enabled = !bodiesLabel.Enabled;
					bodiesValue.Enabled = !bodiesValue.Enabled;
				}

				if (vd.EnvRadiationBodies > 0.0)
				{
					bodiesValue.Text = Lib.HumanReadableRadiation(vd.EnvRadiationBodies, false);
				}

				if ((vd.EnvRadiationSolar > 0.0) != starsLabel.Enabled)
				{
					starsLabel.Enabled = !starsLabel.Enabled;
					starsValue.Enabled = !starsValue.Enabled;
				}

				if (vd.EnvRadiationSolar > 0.0)
				{
					starsValue.Text = Lib.HumanReadableRadiation(vd.EnvRadiationSolar, false);
				}

				backgroundValue.Text = Lib.HumanReadableRadiation(Settings.ExternRadiation * vd.EnvGammaTransparency, false);

			}

		}
	}
}
