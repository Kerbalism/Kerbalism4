using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using KERBALISM.KsmGui;
using KSP.Localization;
using TMPro;
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

		private class VesselEntry : KsmGuiBase
		{
			private static StringBuilder sb = new StringBuilder();

			public VesselData VD { get; private set; }
			private KsmGuiIconButton gotoButton;
			private KsmGuiText vesselName;
			private KsmGuiText bodyName;
			private KsmGuiIcon situationIcon;
			private KsmGuiIcon sunlightIcon;
			private KsmGuiIcon ecIcon;
			private KsmGuiIcon suppliesIcon;
			private KsmGuiIcon rulesIcon;
			private KsmGuiIcon signalMainIcon;
			private KsmGuiIcon signalLinkIcon;
			private KsmGuiIcon signalDataIcon;

			private List<SituationTexture> situationTextures = new List<SituationTexture>();

			private struct SituationTexture
			{
				public Texture2D texture;
				public Color color;

				public SituationTexture(Texture2D texture, Color color)
				{
					this.texture = texture;
					this.color = color;
				}
			}

			// button : goto vessel with vessel type icon
			// label : vessel name
			// label : body (only in vessel type list mode)
			// icon : situation (orbiting, escape, landed), replaced by warning icon if applicable (storm, high rad...)
			// status icons + tooltips : sunlight, EC, supplies, rules, signal

			public VesselEntry(KsmGuiBase parent, VesselData vd) : base(parent)
			{
				VD = vd;
				SetLayoutElement(true, false, -1, -1, -1, 22);
				SetUpdateAction(Update);

				gotoButton = new KsmGuiIconButton(this, GetVesselTypeIcon(VD.Vessel), CreateGotoPopup);
				gotoButton.StaticLayout(20, 20);

				vesselName = new KsmGuiText(this);
				vesselName.TextComponent.alignment = TextAlignmentOptions.Left;
				vesselName.TextComponent.fontStyle = FontStyles.Bold;
				vesselName.UseEllipsisWithTooltip();
				vesselName.StaticLayout(130, 20, 25, 1);
				vesselName.Text = VD.VesselName;

				bodyName = new KsmGuiText(this);
				bodyName.TextComponent.alignment = TextAlignmentOptions.Left;
				bodyName.UseEllipsisWithTooltip();
				bodyName.StaticLayout(45, 20, 160, 1);

				situationIcon = new KsmGuiIcon(this, Textures.empty);
				situationIcon.StaticLayout(20, 20, 210, 1);
				situationIcon.SetTooltipText(SituationTooltip, TextAlignmentOptions.TopLeft);

				sunlightIcon = new KsmGuiIcon(this, Textures.ttSun);
				sunlightIcon.StaticLayout(20, 20, 235, 1);

				ecIcon = new KsmGuiIcon(this, Textures.ttBattery);
				ecIcon.StaticLayout(20, 20, 260, 1);

				suppliesIcon = new KsmGuiIcon(this, Textures.ttBox);
				suppliesIcon.StaticLayout(20, 20, 285, 1);

				rulesIcon = new KsmGuiIcon(this, Textures.ttHeart);
				rulesIcon.StaticLayout(20, 20, 310, 1);

				signalMainIcon = new KsmGuiIcon(this, Textures.ttSignalFull);
				signalMainIcon.StaticLayout(20, 20, 335, 1);
				signalMainIcon.SetTooltipText(CommsTooltip, TextAlignmentOptions.TopLeft);

				signalLinkIcon = new KsmGuiIcon(this, Textures.ttSignalDirect);
				signalLinkIcon.Image.raycastTarget = false;
				signalLinkIcon.StaticLayout(20, 20, 335, 1);
				
				signalDataIcon = new KsmGuiIcon(this, Textures.ttSignalData);
				signalDataIcon.Image.raycastTarget = false;
				signalDataIcon.StaticLayout(20, 20, 335, 1);

			}

			public void UpdateVesselName()
			{
				gotoButton.SetIconTextureWithLayout(GetVesselTypeIcon(VD.Vessel));
				vesselName.Text = VD.VesselName;
			}

			private void CreateGotoPopup()
			{
				KsmGuiPopup popup = new KsmGuiPopup(gotoButton);

				popup.AddButton("Switch to", () => GotoVessel.JumpToVessel(VD.Vessel));
				if (Lib.IsFlight && !VD.Vessel.isActiveVessel)
				{
					popup.AddButton("Target", () => GotoVessel.SetVesselAsTarget(VD.Vessel));
				}
				popup.AddButton("Rename", () => VD.Vessel.RenameVessel());
			}

			private void Update()
			{
				bodyName.Text = VD.Vessel.mainBody.name;
				situationTextures.Clear();

				if (VD.EnvStorm)
				{
					situationTextures.Add(new SituationTexture(Textures.ttStorm, Lib.KolorOrange));
				}

				if (VD.EnvInnerBelt || VD.EnvOuterBelt)
				{
					situationTextures.Add(new SituationTexture(Textures.ttBelt, Lib.KolorOrange));
				}

				if (VD.Habitat.radiationRate > 1.0 / 3600.0)
				{
					situationTextures.Add(new SituationTexture(Textures.ttRadioactive, Lib.KolorRed));
				}
				else if (VD.Habitat.radiationRate > 0.15 / 3600.0)
				{
					situationTextures.Add(new SituationTexture(Textures.ttRadioactive, Lib.KolorOrange));
				}
				else if (VD.Habitat.radiationRate > 0.0195 / 3600.0)
				{
					situationTextures.Add(new SituationTexture(Textures.ttRadioactive, Lib.KolorYellow));
				}

				switch (VD.Vessel.situation)
				{
					case Vessel.Situations.LANDED:
					case Vessel.Situations.SPLASHED:
					case Vessel.Situations.PRELAUNCH:
						situationTextures.Add(new SituationTexture(Textures.ttLanded, Lib.KolorNone));
						break;
					case Vessel.Situations.FLYING:
						situationTextures.Add(new SituationTexture(Textures.ttFlying, Lib.KolorNone));
						break;
					case Vessel.Situations.SUB_ORBITAL:
						situationTextures.Add(new SituationTexture(Textures.ttSuborbit, Lib.KolorNone));
						break;
					case Vessel.Situations.ORBITING:
					case Vessel.Situations.DOCKED:
						situationTextures.Add(new SituationTexture(Textures.ttOrbit, Lib.KolorNone));
						break;
					case Vessel.Situations.ESCAPING:
						situationTextures.Add(new SituationTexture(Textures.ttEscape, Lib.KolorNone));
						break;
				}

				SituationTexture currentTexture = situationTextures[(int) (Time.time % situationTextures.Count)];
				situationIcon.SetIconTexture(currentTexture.texture);
				situationIcon.SetIconColor(currentTexture.color);

				sunlightIcon.SetIconTexture(VD.InSunlight ? Textures.ttSun : Textures.ttSunStriked);

				switch (VD.supplies["ElectricCharge"])
				{
					case Supply.SupplyState.Empty:
						ecIcon.SetIconColor(Lib.Kolor.Red);
						break;
					case Supply.SupplyState.BelowThreshold:
						ecIcon.SetIconColor(Lib.Kolor.Yellow);
						break;
					case Supply.SupplyState.AboveThreshold:
					case Supply.SupplyState.Full:
						ecIcon.SetIconColor(Lib.Kolor.Green);
						break;
				}

				
				if (!VD.Connection.Linked)
				{
					signalMainIcon.SetIconColor(Lib.KolorRed);
					signalLinkIcon.Enabled = false;
					signalDataIcon.Enabled = false;

					switch (VD.Connection.Status)
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

					if (VD.ConnectionInfo.Strength < 0.2)
					{
						signalMainIcon.SetIconTexture(Textures.ttSignalLow);
						signalMainIcon.SetIconColor(Lib.KolorRed);
					}
					else if (VD.ConnectionInfo.Strength < 0.5)
					{
						signalMainIcon.SetIconTexture(Textures.ttSignalMid);
						signalMainIcon.SetIconColor(Lib.KolorYellow);
					}
					else
					{
						signalMainIcon.SetIconTexture(Textures.ttSignalFull);
						signalMainIcon.SetIconColor(Lib.KolorGreen);
					}

					switch (VD.Connection.Status)
					{
						case LinkStatus.direct_link:
							signalLinkIcon.SetIconTexture(Textures.ttSignalDirect);
							break;
						case LinkStatus.indirect_link:
							signalLinkIcon.SetIconTexture(Textures.ttSignalRelay);
							break;
					}

					if (VD.Connection.DataRate == 0.0)
					{
						signalDataIcon.SetIconTexture(Textures.ttSignalNoData);
						signalDataIcon.SetIconColor(Lib.KolorRed);
					}
					else 
					{
						signalDataIcon.SetIconTexture(Textures.ttSignalData);

						if (VD.filesTransmitted.Count > 0)
						{
							signalDataIcon.SetIconColor(Lib.KolorScience);
						}
						else if (VD.Connection.DataRate < 0.01)
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

			private string BodyTooltip()
			{
				sb.Clear();

				return sb.ToString();
			}

			private string SituationTooltip()
			{
				KsmString ks = KsmString.Get;

				switch (VD.Vessel.situation)
				{
					case Vessel.Situations.LANDED:
					case Vessel.Situations.SPLASHED:
					case Vessel.Situations.PRELAUNCH:
						ks.Info("Status", "Landed", 60);
						break;
					case Vessel.Situations.FLYING:
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

				ks.Info("Body", VD.Vessel.mainBody.name, 60);

				if (VD.IsSimulated)
				{
					if (!string.IsNullOrEmpty(VD.VesselSituations.BiomeTitle))
					{
						ks.Info("Biome", VD.VesselSituations.BiomeTitle, 60);
					}

					ks.Format(KF.BreakAfter);
					ks.Format("Science situations", KF.Center, KF.KolorYellow, KF.BreakAfter);

					ks.Format(VD.VesselSituations.FirstSituation.ScienceSituationTitle, KF.List);
					for (int i = 0; i < VD.VesselSituations.virtualBiomes.Count; i++)
					{
						if (VD.VesselSituations.virtualBiomes[i] == VirtualBiome.NoBiome)
							continue;

						ks.Format(VD.VesselSituations.virtualBiomes[i].Title(), KF.List);
					}
				}

				return ks.End();
			}

			private string CommsTooltip()
			{
				sb.Clear();

				if (!VD.Connection.Linked)
				{
					sb.Format("No signal", KF.KolorOrange);

					switch (VD.Connection.Status)
					{
						case LinkStatus.plasma:
							sb.Format("Communications blackout during reentry", KF.BreakBefore);
							break;
						case LinkStatus.storm:
							sb.Format("Communications blackout during solar storm", KF.BreakBefore);
							break;
					}
				}
				else
				{
					if (VD.ConnectionInfo.Strength < 0.2)
						sb.Info("Signal strength", VD.Connection.Strength.ToString("P2"), KF.KolorOrange, 100);
					else if (VD.ConnectionInfo.Strength < 0.5)
						sb.Info("Signal strength", VD.Connection.Strength.ToString("P2"), KF.KolorYellow, 100);
					else
						sb.Info("Signal strength", VD.Connection.Strength.ToString("P2"), KF.KolorGreen, 100);

					switch (VD.Connection.Status)
					{
						case LinkStatus.direct_link:
							sb.Info("Link type", "direct", 100);
							break;
						case LinkStatus.indirect_link:
							sb.Info("Link type", "relayed", 100);
							break;
					}

					if (VD.Connection.DataRate == 0.0)
					{
						sb.Info("Max data rate", Local.Generic_NONE, KF.KolorOrange, 100);
					}
					else
					{
						sb.Info("Max data rate", Lib.HumanReadableDataRate(VD.Connection.DataRate), 100);
					}

					if (VD.filesTransmitted.Count == 0)
					{
						sb.Info("Transmitting", Local.UI_telemetry, 100);
					}
					else
					{
						sb.Info("Transmitting", KF.Concat(VD.filesTransmitted.Count.ToString(), VD.filesTransmitted.Count > 1 ? " files" : " file"), 100);
					}
				}

				return sb.ToString();
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

		private List<VesselEntry> vesselEntries = new List<VesselEntry>(DB.VesselDatas.Count);

		public VesselsManager(KsmGuiBase parent) : base(parent)
		{
			SetDestroyCallback(OnDestroy);
			GameEvents.onVesselRename.Add(OnVesselRename);
			SetLayoutElement(false, true, -1, -1, 360);

			foreach (VesselData vd in DB.VesselDatas)
			{
				if (!vd.IsSimulated)
				{
					continue;
				}

				VesselEntry entry = new VesselEntry(this, vd);
				vesselEntries.Add(entry);
				entry.SetColor(entry.TopTransform.GetSiblingIndex() % 2 == 0 ? KsmGuiStyle.boxColor : Color.clear);
			}
		}

		private void OnVesselRename(GameEvents.HostedFromToAction<Vessel, string> data)
		{
			foreach (VesselEntry entry in vesselEntries)
			{
				if (entry.VD.Vessel == data.host)
				{
					entry.UpdateVesselName();
					break;
				}
			}
		}

		private void OnDestroy()
		{
			GameEvents.onVesselRename.Remove(OnVesselRename);
		}
	}
}
