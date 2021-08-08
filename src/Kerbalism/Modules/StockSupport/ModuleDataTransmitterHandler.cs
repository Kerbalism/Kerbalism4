using System.Reflection;
using HarmonyLib;

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

			switch (loadedModule.antennaType)
			{
				case AntennaType.INTERNAL: field.guiName = "Internal antenna"; break;
				case AntennaType.DIRECT: field.guiName = "Direct antenna"; break;
				case AntennaType.RELAY: field.guiName = "Relay antenna"; break;
			}

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
}
