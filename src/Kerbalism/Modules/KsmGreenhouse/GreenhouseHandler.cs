namespace KERBALISM
{
	public class GreenhouseHandler :
		KsmModuleHandler<ModuleKsmGreenhouse, GreenhouseHandler, GreenhouseDefinition>,
		IB9Switchable
	{

		public Process GrowthProcess { get; private set; } // the process associated with the process name, for convenience
		public Process SetupProcess { get; private set; } // the process associated with the process name, for convenience

		public double growthRate; // Current max. rate [0..1] of growth process
		private PartResourceWrapper setupResource;

		private bool setupRunning;
		public bool SetupRunning
		{
			get => setupRunning;
			set
			{
				if (SetupProcess != null && value != setupRunning)
				{
					setupRunning = value;

					if (setupResource != null)
						setupResource.FlowState = value;

					if (IsLoaded)
						loadedModule.setupRunning = value;

					// refresh planner and VAB/SPH ui
					if (Lib.IsEditor) GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
				}
			}
		}

		private bool growthRunning;
		public bool GrowthRunning
		{
			get => growthRunning;
			set
			{
				if (value != growthRunning)
				{
					growthRunning = value;

					if (IsLoaded)
						loadedModule.growthRunning = value;

					// refresh planner and VAB/SPH ui
					if (Lib.IsEditor) GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
				}
			}
		}

		public override void OnPrefabCompilation()
		{
			GetProcesses();
		}

		public override void OnFirstSetup()
		{
			GetProcesses();
			AddSetupResource();
		}

		public override void OnStart()
		{
			GetProcesses();
			GetSetupResource();
		}

		public void OnSwitchChangeDefinition(KsmModuleDefinition previousDefinition)
		{
			RemoveSetupResource();
			GetProcesses();
			AddSetupResource();

			if (IsLoaded)
				loadedModule.Setup();
		}

		public void OnSwitchEnable() { }

		public void OnSwitchDisable() { }

		private void GetProcesses()
		{
			GrowthProcess = Profile.processes.Find(p => p.name == definition.growthProcessName);
			SetupProcess = Profile.processes.Find(p => p.name == definition.setupProcessName);
		}

		private void GetSetupResource()
		{
			if (definition.setupResourceCapacity > 0.0)
			{
				setupResource = partData.resources.Find(p => p.ResName == definition.setupResourceName);
			}
		}

		private void AddSetupResource()
		{
			if (SetupProcess != null && string.IsNullOrEmpty(definition.setupResourceName) && definition.setupResourceCapacity > 0.0)
			{
				setupResource = partData.resources.AddResource(definition.setupResourceName, 0.0, definition.setupResourceCapacity);
			}
		}

		private void RemoveSetupResource()
		{
			if (setupResource != null)
			{
				partData.resources.RemoveResource(setupResource.ResName);
			}
		}

		public override void OnLoad(ConfigNode node)
		{
			growthRunning = Lib.ConfigValue(node, "growthRunning", false);
			setupRunning = Lib.ConfigValue(node, "setupRunning", false);
		}

		public override void OnSave(ConfigNode node)
		{
			node.AddValue("growthRunning", growthRunning);
			node.AddValue("setupRunning", setupRunning);
		}

		public override void OnFixedUpdate(double elapsedSec)
		{
			growthRate = setupResource != null ? setupResource.Level : 1.0;
		}

		public override void OnVesselDataUpdate()
		{
			// TODO account for max radiation and min light
			if (setupResource != null)
			{
				VesselData.VesselProcesses.GetOrCreateProcessData(SetupProcess).RegisterProcessControllerCapacity(setupRunning, definition.setupProcessCapacity);
				VesselData.VesselProcesses.GetOrCreateProcessData(GrowthProcess).RegisterProcessControllerCapacity(growthRunning, definition.growthProcessCapacity * setupResource.Level);
			}
			else
			{
				VesselData.VesselProcesses.GetOrCreateProcessData(GrowthProcess).RegisterProcessControllerCapacity(growthRunning, definition.growthProcessCapacity);
			}
		}

		public string GetSubtypeDescription(KsmModuleDefinition subTypeDefinition, string techRequired)
		{
			GreenhouseDefinition processControllerDefinition = (GreenhouseDefinition)subTypeDefinition;
			Process setupProcess = Profile.processes.Find(p => p.name == processControllerDefinition.setupProcessName);
			Process growthProcess = Profile.processes.Find(p => p.name == processControllerDefinition.growthProcessName);

			if (growthProcess == null)
				return null;

			if (setupProcess != null)
			{
				return Lib.BuildString(
					setupProcess.GetInfo(processControllerDefinition.setupProcessCapacity, true), "\n",
					growthProcess.GetInfo(processControllerDefinition.growthProcessCapacity, true));
			}

			return growthProcess.GetInfo(processControllerDefinition.growthProcessCapacity, true);
		}
	}
}
