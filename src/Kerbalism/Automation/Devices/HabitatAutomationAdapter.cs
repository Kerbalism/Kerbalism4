using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;
using System.Text;

namespace KERBALISM
{
	public class HabitatAutomationAdapter : AutomationAdapter
	{
		public HabitatAutomationAdapter(KsmPartModule module, ModuleHandler moduleData) : base(module, moduleData) { }

		private HabitatHandler data => moduleData as HabitatHandler;

		public override string Status => ModuleKsmHabitat.PressureStateString(data);

		public override string Name => "habitat";

		public override string Tooltip => "Pressure: " + ModuleKsmHabitat.MainInfoString(module as ModuleKsmHabitat, data);

		public override void OnUpdate()
		{
			IsVisible = data.IsDeployed;
		}

		public override void Ctrl(bool value)
		{
			switch (data.pressureState)
			{
				case HabitatHandler.PressureState.Pressurized:
				case HabitatHandler.PressureState.Pressurizing:
					if(!value)
						data.DepressurizingStartEvt();
					break;
				case HabitatHandler.PressureState.Breatheable:
				case HabitatHandler.PressureState.Depressurized:
				case HabitatHandler.PressureState.DepressurizingAboveThreshold:
				case HabitatHandler.PressureState.DepressurizingBelowThreshold:
					if(value)
						data.PressurizingStartEvt();
					break;
			}
		}

		public override void Toggle()
		{
			ModuleKsmHabitat.TryTogglePressure(module as ModuleKsmHabitat, data);
		}
	}
} // KERBALISM
