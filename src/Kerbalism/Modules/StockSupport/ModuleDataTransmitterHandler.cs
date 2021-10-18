using System.Reflection;
using HarmonyLib;
using KSP.Localization;

namespace KERBALISM
{
	public class ModuleDataTransmitterHandler : TypedModuleHandler<ModuleDataTransmitter>, IPersistentModuleHandler
	{
		private static readonly FieldInfo transmitterEnabledInfo = AccessTools.Field(typeof(ModuleDataTransmitterHandler), nameof(transmitterEnabled));

		public override ActivationContext Activation => ActivationContext.Editor | ActivationContext.Loaded | ActivationContext.Unloaded;

		public ModuleHandler ModuleHandler => this;

		public int FlightId { get; set; }
		public int ShipId { get; set; }

		public bool transmitterEnabled = true;

		public void Load(ConfigNode node)
		{
			transmitterEnabled = Lib.ConfigValue(node, nameof(transmitterEnabled), true);
		}

		public void Save(ConfigNode node)
		{
			node.AddValue(nameof(transmitterEnabled), transmitterEnabled);
		}

		public override void OnStart()
		{
			if (!IsLoaded)
				return;

			UI_Toggle toggle = new UI_Toggle();
			BaseField field = new BaseField(toggle, transmitterEnabledInfo, this);
			field.guiName = "Antenna";
			field.guiActiveEditor = true;
			field.uiControlEditor = toggle;
			field.uiControlFlight = toggle;

			loadedModule.Fields.Add(field);
		}
	}

	[HarmonyPatch(typeof(ModuleDataTransmitter))]
	[HarmonyPatch(nameof(ModuleDataTransmitter.CanComm))]
	class ModuleDataTransmitter_CanComm
	{
		static void Postfix(ModuleDataTransmitter __instance, ref bool __result)
		{
			if (!__result)
				return;

			ModuleDataTransmitterHandler handler = __instance.GetModuleHandler<ModuleDataTransmitterHandler>();

			if (handler != null)
				__result = handler.transmitterEnabled;
		}
	}

	[HarmonyPatch(typeof(ModuleDataTransmitter))]
	[HarmonyPatch(nameof(ModuleDataTransmitter.OnStart))]
	class ModuleDataTransmitter_OnStart
	{
		static void Postfix(ModuleDataTransmitter __instance)
		{
			__instance.Actions[nameof(ModuleDataTransmitter.StartTransmissionAction)].active = false;
			__instance.Events[nameof(ModuleDataTransmitter.StartTransmission)].active = false;
			__instance.Events[nameof(ModuleDataTransmitter.StopTransmission)].active = false;
			__instance.Events[nameof(ModuleDataTransmitter.TransmitIncompleteToggle)].active = false;
			__instance.Fields[nameof(ModuleDataTransmitter.statusText)].guiActive = false;
			__instance.Fields[nameof(ModuleDataTransmitter.statusText)].guiActiveEditor = false;
			__instance.Fields[nameof(ModuleDataTransmitter.powerText)].guiName = Localizer.Format("#autoLOC_234196"); // "Antenna"
		}
	}

	[HarmonyPatch(typeof(ModuleDataTransmitter))]
	[HarmonyPatch(nameof(ModuleDataTransmitter.UpdatePowerText))]
	class ModuleDataTransmitter_UpdatePowerText
	{
		static bool Prefix(ModuleDataTransmitter __instance)
		{
			switch (__instance.antennaType)
			{
				case AntennaType.INTERNAL: __instance.powerText = $"Internal ({KSPUtil.PrintSI(__instance.CommPower, string.Empty)})"; break;
				case AntennaType.DIRECT: __instance.powerText = $"Direct ({KSPUtil.PrintSI(__instance.CommPower, string.Empty)}, {Lib.HumanReadableDataRate(__instance.DataRate)})"; break;
				case AntennaType.RELAY: __instance.powerText = $"Relay ({KSPUtil.PrintSI(__instance.CommPower, string.Empty)}, {Lib.HumanReadableDataRate(__instance.DataRate)})"; break;
			}

			return false;
		}
	}
}
