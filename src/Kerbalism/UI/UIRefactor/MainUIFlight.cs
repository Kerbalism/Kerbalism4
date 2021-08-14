using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KERBALISM.KsmGui;

namespace KERBALISM
{
	public class MainUIFlight
	{
		public MainUIFlight Instance { get; private set; }

		private VesselsManager vesselsManager;
		private VesselSummaryUI vesselSummary;
		private VesselDataBase selectedVessel;

		public MainUIFlight(KsmGuiWindow window)
		{
			Instance = this;
			vesselsManager = new VesselsManager(window);
			vesselsManager.onVesselSelected = OnVesselSelected;
			vesselSummary = new VesselSummaryUI(window, false);
			vesselSummary.Enabled = false;
		}

		private void OnVesselSelected(VesselData vd)
		{
			if (selectedVessel == vd)
			{
				selectedVessel = null;
				vesselSummary.Enabled = false;
				return;
			}

			selectedVessel = vd;

			if (vd.IsSimulated)
			{
				vesselSummary.SetVessel(vd);
				vesselSummary.Enabled = true;
			}
			else
			{
				vesselSummary.Enabled = false;
			}
		}
	}
}
