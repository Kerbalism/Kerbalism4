using UnityEngine;

namespace KERBALISM
{
	/*
	public class ModuleKsmGreenhouse :
		KsmPartModule<ModuleKsmGreenhouse, GreenhouseHandler, GreenhouseDefinition>,
		IModuleInfo
	{
		[KSPField(groupName = "Greenhouse", groupDisplayName = "#KERBALISM_Group_Greenhouse")]
		[UI_Toggle(scene = UI_Scene.All, requireFullControl = false, affectSymCounterparts = UI_Scene.None)]
		public bool setupRunning;

		[KSPField(groupName = "Greenhouse", groupDisplayName = "#KERBALISM_Group_Greenhouse")]
		[UI_Toggle(scene = UI_Scene.All, requireFullControl = false, affectSymCounterparts = UI_Scene.None)]
		public bool growthRunning;

		private BaseField growthRunningField;
		private BaseField setupRunningField;

		// animation handlers
		private Animator shutterAnimator;
		private Animator plantsAnimator;
		private Renderer lampsRenderer;
		private Color lampColor;

		public override void KsmStart()
		{
			// get BaseField references
			growthRunningField = Fields["growthRunning"];
			setupRunningField = Fields["setupRunning"];

			// add value modified callbacks to the toggles
			growthRunningField.OnValueModified += OnToggleGrowth;
			setupRunningField.OnValueModified += OnToggleSetup;

			((UI_Toggle)growthRunningField.uiControlFlight).enabledText = Lib.Color("enabled", Lib.Kolor.Green);
			((UI_Toggle)growthRunningField.uiControlFlight).disabledText = Lib.Color("disabled", Lib.Kolor.Yellow);
			((UI_Toggle)growthRunningField.uiControlEditor).enabledText = Lib.Color("enabled", Lib.Kolor.Green);
			((UI_Toggle)growthRunningField.uiControlEditor).disabledText = Lib.Color("disabled", Lib.Kolor.Yellow);

			((UI_Toggle)setupRunningField.uiControlFlight).enabledText = Lib.Color("enabled", Lib.Kolor.Green);
			((UI_Toggle)setupRunningField.uiControlFlight).disabledText = Lib.Color("disabled", Lib.Kolor.Yellow);
			((UI_Toggle)setupRunningField.uiControlEditor).enabledText = Lib.Color("enabled", Lib.Kolor.Green);
			((UI_Toggle)setupRunningField.uiControlEditor).disabledText = Lib.Color("disabled", Lib.Kolor.Yellow);

			Setup();
		}

		public void Setup()
		{
			// get animations
			shutterAnimator = new Animator(part, Definition.anim_shutters, Definition.anim_shutters_reverse);
			plantsAnimator = new Animator(part, Definition.anim_plants, Definition.anim_plants_reverse);

			// get lamps renderer
			if (Definition.lamps.Length > 0)
			{
				lampsRenderer = part.FindModelComponent<Renderer>(Definition.lamps);
				if (lampsRenderer != null)
				{
					lampColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);
					lampsRenderer.material.SetColor("_EmissiveColor", lampColor);
				}
			}
			else
			{
				lampsRenderer = null;
			}

			// set PAW visibility
			growthRunningField.guiActive = growthRunningField.guiActiveEditor = true;
			setupRunningField.guiActive = setupRunningField.guiActiveEditor = moduleHandler.SetupProcess != null;

			// set PAW names
			growthRunningField.guiName = moduleHandler.GrowthProcess.title ?? "Grow food";
			setupRunningField.guiName = moduleHandler.SetupProcess?.title ?? "Generate Substrate";

			// synchronize PAW state with data state
			growthRunning = moduleHandler.GrowthRunning;
			setupRunning = moduleHandler.SetupRunning;

			// set animations state
			shutterAnimator.Still(growthRunning ? 0f : 1f);
		}

		public void Update()
		{
			// TODO turn on lights if current light level is too low
			// set lamps emissive object
			if (lampsRenderer != null)
			{
				lampColor.a = (growthRunning || setupRunning) ? 1.0f : 0.0f;
				lampsRenderer.material.SetColor("_EmissiveColor", lampColor);
			}

			plantsAnimator.Still((float)moduleHandler.growthRate);
		}

		private void OnToggleSetup(object field) => moduleHandler.SetupRunning = !moduleHandler.SetupRunning;

		private void OnToggleGrowth(object field) => moduleHandler.GrowthRunning = !moduleHandler.GrowthRunning;

		// IModuleInfo : module title
		public string GetModuleTitle() => Local.Greenhouse;

		// IModuleInfo : part tooltip module description
		public override string GetInfo()
		{
			return moduleHandler.GetSubtypeDescription(moduleHandler.definition, null);
		}

		// IModuleInfo : part tooltip general part info
		public string GetPrimaryField() => string.Empty;
		public Callback<Rect> GetDrawModulePanelCallback() => null;

		// automation
		public override AutomationAdapter[] CreateAutomationAdapter(KsmPartModule moduleOrPrefab, ModuleHandler moduleData)
		{
			return new AutomationAdapter[] {
				new GreenhouseGrowthAutomationAdapter(moduleOrPrefab, moduleData),
				new GreenhouseSetupAutomationAdapter(moduleOrPrefab, moduleData)
			};
		}

		private abstract class GreenhouseAutomationAdapter : AutomationAdapter
		{
			protected ModuleKsmGreenhouse greenhouseModule => module as ModuleKsmGreenhouse;
			protected GreenhouseHandler data => moduleData as GreenhouseHandler;

			public GreenhouseAutomationAdapter(KsmPartModule module, ModuleHandler moduleData) : base(module, moduleData) { }
		}

		private class GreenhouseGrowthAutomationAdapter : GreenhouseAutomationAdapter
		{
			public GreenhouseGrowthAutomationAdapter(KsmPartModule module, ModuleHandler moduleData) : base(module, moduleData) { }


			public override string Name => "Greenhouse grow food"; // must be hardcoded
			public override string DisplayName => data.GrowthProcess.title;

			public override string Status => Lib.Color(data.growthRunning, Local.Generic_RUNNING, Lib.Kolor.Green, Local.Generic_STOPPED, Lib.Kolor.Orange);

			public override void Ctrl(bool value)
			{
				if(data.growthRunning != value)
					ToggleGrowth(data);
			}

			public override void Toggle()
			{
				ToggleGrowth(data);
			}
		}

		private class GreenhouseSetupAutomationAdapter : GreenhouseAutomationAdapter
		{
			public GreenhouseSetupAutomationAdapter(KsmPartModule module, ModuleHandler moduleData) : base(module, moduleData)
			{
				IsVisible = data.SetupProcess != null;
			}

			public override string Name => "Greenhouse generate substrate"; // must be hardcoded
			public override string DisplayName => data.SetupProcess?.title ?? "Generate substrate";

			public override string Status => Lib.Color(data.setupRunning, Local.Generic_RUNNING, Lib.Kolor.Green, Local.Generic_STOPPED, Lib.Kolor.Orange);

			public override void Ctrl(bool value)
			{
				if (data.setupRunning != value)
					ToggleSetup(data);
			}

			public override void Toggle()
			{
				ToggleSetup(data);
			}
		}
	}
*/
}
