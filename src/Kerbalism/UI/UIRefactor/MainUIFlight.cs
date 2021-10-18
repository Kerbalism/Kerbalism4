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
		public static MainUIFlight Instance { get; private set; }

		private VesselsManager vesselsManager;
		private VesselManager vesselManager;
		private VesselDataBase selectedVessel;

		public MainUIFlight(KsmGuiWindow window, bool vesselListAtTop)
		{
			Instance = this;

			vesselManager = new VesselManager(window);
			vesselsManager = new VesselsManager(window);

			if (vesselListAtTop)
			{
				vesselsManager.MoveAsFirstChild();
			}

			vesselsManager.onVesselSelected = SelectVessel;

			vesselManager.Enabled = false;
		}


		public void SelectVessel(VesselData vd)
		{
			if (vd == null || selectedVessel == vd)
			{
				selectedVessel = null;
				vesselManager.Enabled = false;
				return;
			}

			selectedVessel = vd;

			if (vd.IsSimulated)
			{
				vesselManager.SetVessel(vd);
				vesselManager.Enabled = true;
			}
			else
			{
				vesselManager.Enabled = false;
			}
		}
	}
}
