using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KERBALISM.KsmGui;
using TMPro;
using UnityEngine;

namespace KERBALISM
{
	public class VesselSummaryUI : KsmGuiVerticalLayout
	{
		private StringBuilder sb = new StringBuilder();

		private bool isPopup;
		private bool isEditor;
		private VesselDataBase vdBase;
		private VesselData vd;

		private KsmGuiHorizontalLayout summmarySpace;

		private KsmGuiText signal; // "45 %, x.xx kB/s", tooltip : current distance, max distance, control path list
		private KsmGuiText transmit; // "X files, x.xx kB/s" / "telemetry", tooltip : total science transmitted, list of files / rate
		private KsmGuiText storedData; // "X/X Mb", tooltip : science value
		private KsmGuiText samples; // "X/X", tooltip : science value, weight

		private KsmGuiText bodyAndBiome; // "Kerbin Highlands", tooltip : full biome name
		private KsmGuiText situations; // "Space high (+2)", tooltip : other situations
		private KsmGuiText temperature; // "326 K", tooltip : flux details
		private KsmGuiText radiation; // "1.2 rad", tooltip : rad sources details


		private KsmGuiVerticalLayout crewSpace;
		private List<KerbalEntry> kerbalEntries = new List<KerbalEntry>();

		private class KerbalEntry : KsmGuiHorizontalLayout
		{
			private class RuleEntry : KsmGuiText
			{
				private StringBuilder sb = new StringBuilder();
				private KerbalRule rule;
				public RuleEntry(KsmGuiBase parent, KerbalRule rule) : base(parent, rule.Definition.title)
				{
					this.rule = rule;
					SetUpdateAction(Update);
					SetTooltipText(UpdateTooltip);
				}

				private void Update()
				{
					Text = rule.Definition.title + ": " + rule.Level.ToString("P2");
				}

				private string UpdateTooltip()
				{
					sb.Clear();

					sb.AppendKSPLine(rule.Value.ToString("F2") + "/" + rule.MaxValue.ToString("F2"));
					for (int i = 0; i < rule.Modifiers.Count; i++)
					{
						sb.AppendKSPLine(rule.Definition.modifiers[i].title + ": " + rule.Modifiers[i].currentRate.ToString("F5"));
					}

					return sb.ToString();
				}
			}


			private KerbalData kd;
			private KsmGuiText name;
			private List<RuleEntry> rules = new List<RuleEntry>();

			public KerbalEntry(KsmGuiBase parent, KerbalData kd) : base(parent, 0, 0, 0, 0, 0, TextAnchor.UpperLeft)
			{
				this.kd = kd;
				name = new KsmGuiText(this, kd.stockKerbal.displayName);

				foreach (KerbalRule rule in kd.rules)
				{
					rules.Add(new RuleEntry(this, rule));
				}
			}
		}


		public VesselSummaryUI(KsmGuiBase parent, bool isPopup, VesselDataBase vd) : base(parent, 0, 0, 0, 0, 0, TextAnchor.UpperLeft)
		{
			this.vdBase = vd;
			this.isPopup = isPopup;

			if (vdBase is VesselData)
			{
				this.vd = (VesselData) vdBase;
				isEditor = false;
			}
			else
			{
				isEditor = true;
			}

			KsmGuiHeader topHeader = new KsmGuiHeader(this, vd.VesselName);
			new KsmGuiIconButton(topHeader, Textures.KsmGuiTexHeaderClose, () => ((KsmGuiWindow) parent).Close(), Local.SCIENCEARCHIVE_closebutton); //"close"
			topHeader.Enabled = isPopup;

			summmarySpace = new KsmGuiHorizontalLayout(this, 10);
			summmarySpace.SetUpdateAction(UpdateSummary);

			KsmGuiVerticalLayout commsAndScience = new KsmGuiVerticalLayout(summmarySpace);
			new KsmGuiHeader(commsAndScience, "COMMS & SCIENCE");
			signal = new KsmGuiText(commsAndScience, "Signal"); // "45 %, x.xx kB/s", tooltip : current distance, max distance, control path list
			signal.SetTooltipText(SignalTooltip, TextAlignmentOptions.TopLeft, 350f);
			transmit = new KsmGuiText(commsAndScience, "Upload"); // "X files, x.xx kB/s" / "telemetry", tooltip : total science transmitted, list of files / rate
			transmit.SetTooltipText(TransmitTooltip);
			storedData = new KsmGuiText(commsAndScience, "Data"); // "X/X Mb", tooltip : science value
			samples = new KsmGuiText(commsAndScience, "Samples"); // "X/X", tooltip : science value, weight

			KsmGuiVerticalLayout environment = new KsmGuiVerticalLayout(summmarySpace);
			new KsmGuiHeader(environment, "ENVIRONMENT");
			bodyAndBiome = new KsmGuiText(environment, "Location"); // "Kerbin Highlands", tooltip : full biome name
			situations = new KsmGuiText(environment, "Situation"); // "Space high (+2)", tooltip : other situations
			situations.SetTooltipText(SituationTooltip);
			temperature = new KsmGuiText(environment, "Temperature"); // "326 K", tooltip : flux details
			radiation = new KsmGuiText(environment, "Radiation"); // "1.2 rad", tooltip : rad sources details

			crewSpace = new KsmGuiVerticalLayout(this, 10);
			crewSpace.SetUpdateAction(UpdateCrew);
			new KsmGuiHeader(crewSpace, "CREW");
		}

		private void UpdateCrew()
		{
			for (int i = 0; i < vd.Crew.Count; i++)
			{
				if (kerbalEntries.Count - 1 < i)
				{
					kerbalEntries.Add(new KerbalEntry(crewSpace, vd.Crew[i]));
				}



			}
		}

		private void UpdateSummary()
		{

			signal.Text = "Signal<pos=20em>" + vd.Connection.strength.ToString("P1") + " (" + Lib.HumanReadableDataRate(vd.Connection.DataRate) + ")";
			transmit.Text = "Upload<pos=20em>" + vd.filesTransmitted.Count + " files" + " (" + Lib.HumanReadableDataRate(vd.filesTransmitted.Sum(i => i.transmitRate)) + ")";
			storedData.Text = "Data<pos=20em>" + Lib.HumanReadableDataSize(vd.DrivesCapacity - vd.DrivesFreeSpace) + "/" + Lib.HumanReadableDataSize(vd.DrivesCapacity);
			samples.Text = "Samples<pos=20em>";

			bodyAndBiome.Text = "Location<pos=25em>" + vd.VesselSituations.BodyTitle + " (" + vd.VesselSituations.BiomeTitle + ")";

			sb.Clear();
			sb.Append("Situation<pos=25em>" + vd.VesselSituations.FirstSituation.ScienceSituationTitle);
			if (vd.VesselSituations.situations.Count > 1)
				sb.Append(" (+" + (vd.VesselSituations.situations.Count - 1) + ")");
			situations.Text = sb.ToString();

			temperature.Text = "Temperature<pos=25em>" + Lib.HumanReadableTemp(vd.EnvTemperature);
			radiation.Text = "Radiation<pos=25em>" + Lib.HumanReadableRadiation(vd.EnvRadiation);

		}

		private string SignalTooltip()
		{
			sb.Clear();
			sb.AppendKSPLine("<align=center>Control path :</align>");
			sb.AppendKSPLine("<pos=5em>Strength<pos=25em>Target<pos=50em>Details");
			for (int i = 0; i < vd.Connection.control_path.Count; i++)
			{
				sb.AppendKSPLine((i + 1) + ".<pos=5em>" + vd.Connection.control_path[i][1] + "<pos=25em>" + vd.Connection.control_path[i][0] + "<pos=50em>" + vd.Connection.control_path[i][2]);
			}

			return sb.ToString();
		}

		private string TransmitTooltip()
		{
			sb.Clear();
			sb.AppendKSPLine("Total science transmitted : " + Lib.HumanReadableScience(vd.scienceTransmitted, true, true));

			if (vd.filesTransmitted.Count > 0)
			{
				sb.AppendKSPLine("Transmitting :");
				for (int i = 0; i < vd.filesTransmitted.Count; i++)
				{
					sb.AppendKSPLine("> " + Lib.HumanReadableDataRate(vd.filesTransmitted[i].transmitRate) + "<pos=20em>" + Lib.Ellipsis(vd.filesTransmitted[i].subjectData.FullTitle, 20));
				}
			}

			return sb.ToString();
		}

		private string SituationTooltip()
		{
			sb.Clear();
			sb.AppendKSPLine("Available situations :");

			for (int i = 0; i < vd.VesselSituations.SituationsTitle.Length; i++)
			{
				sb.AppendKSPLine("> " + vd.VesselSituations.SituationsTitle[i]);
			}

			return sb.ToString();
		}
	}
}
