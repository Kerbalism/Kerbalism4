using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace KERBALISM
{
	public class ModuleDeployableSolarPanelHandler : SolarPanelHandlerBase
	{
		private static string[] moduleNames;
		
		static ModuleDeployableSolarPanelHandler()
		{
			// TODO : nope, this can't be done here, as other mod assemblies might not be loaded yet when the static constructor is called
			// move it to ModuleManagerPostLoad
			List<string> moduleTypes = new List<string>();
			moduleTypes.Add(nameof(ModuleDeployableSolarPanel));
			foreach (Type type in AssemblyLoader.GetSubclassesOfParentClass(typeof(ModuleDeployableSolarPanel)))
			{
				moduleTypes.Add(type.Name);
			}

			moduleNames = moduleTypes.ToArray();
		}

		public override string[] ModuleTypeNames => moduleNames;

		private ModuleDeployableSolarPanel loadedPanel;
		private RaycastHit raycastHit;
		private PersistentTransform sunCatcherPosition;   // middle point of the panel surface (usually). Use only position, panel surface direction depend on the pivot transform, even for static panels.
		private PersistentTransform sunCatcherPivot;      // If it's a tracking panel, "up" is the pivot axis and "position" is the pivot position. In any case "forward" is the panel surface normal.

		private enum PanelType
		{
			FlatTracking,
			FlatNonTracking,
			CylindricalPivot,
			CylindricalPartX,
			CylindricalPartY,
			CylindricalPartZ,
			Spherical
		}

		private PanelType panelType;

		public override void Load(ConfigNode node)
		{
			base.Load(node);
			panelType = Lib.ConfigEnum(node, nameof(panelType), PanelType.FlatNonTracking);
			sunCatcherPosition = new PersistentTransform(node.GetNode(nameof(sunCatcherPosition)));
			sunCatcherPivot = new PersistentTransform(node.GetNode(nameof(sunCatcherPivot)));
		}

		public override void Save(ConfigNode node)
		{
			base.Save(node);
			node.AddValue(nameof(panelType), panelType);
			sunCatcherPosition?.Save(node.AddNode(nameof(sunCatcherPosition)), VesselData);
			sunCatcherPivot?.Save(node.AddNode(nameof(sunCatcherPivot)), VesselData);
		}

		public override void OnStart()
		{
			if (IsLoaded)
			{
				loadedPanel = (ModuleDeployableSolarPanel)loadedModule;

				// hide stock ui
				loadedPanel.Fields["sunAOA"].guiActive = false;
				loadedPanel.Fields["flowRate"].guiActive = false;
				loadedPanel.Fields["status"].guiActive = false;
				loadedPanel.Fields["status"].guiActiveEditor = false;
				loadedPanel.showStatus = false;

				GetModuleTransformsAndRate();
			}
			else
			{
				PersistentTransform.Init(ref sunCatcherPivot, this);
				PersistentTransform.Init(ref sunCatcherPosition, this);
			}

			base.OnStart();
		}

		private void GetModuleTransformsAndRate()
		{
			nominalRate = loadedPanel.resHandler.outputResources[0].rate;

			PersistentTransform.Init(ref sunCatcherPosition, this, loadedPanel.part.FindModelTransform(loadedPanel.secondaryTransformName));

			if (sunCatcherPosition == null)
			{
				Lib.Log($"Could not find suncatcher transform `{loadedPanel.secondaryTransformName}` in part `{loadedPanel.part.name}`", Lib.LogLevel.Error);
				handlerIsEnabled = false;
				return;
			}

			switch (loadedPanel.panelType)
			{
				case ModuleDeployableSolarPanel.PanelType.FLAT:
					panelType = loadedPanel.isTracking ? PanelType.FlatTracking : PanelType.FlatNonTracking;
					break;
				case ModuleDeployableSolarPanel.PanelType.CYLINDRICAL:
					switch (loadedPanel.alignType)
					{
						case ModuleDeployablePart.PanelAlignType.PIVOT:
							panelType = PanelType.CylindricalPivot;
							break;
						case ModuleDeployablePart.PanelAlignType.X:
							panelType = PanelType.CylindricalPartX;
							break;
						case ModuleDeployablePart.PanelAlignType.Y:
							panelType = PanelType.CylindricalPartY;
							break;
						case ModuleDeployablePart.PanelAlignType.Z:
							panelType = PanelType.CylindricalPartZ;
							break;
					}
					break;
				case ModuleDeployableSolarPanel.PanelType.SPHERICAL:
					panelType = PanelType.Spherical;
					break;
			}

			if (panelType == PanelType.CylindricalPartX || panelType == PanelType.CylindricalPartY || panelType == PanelType.CylindricalPartZ)
			{
				PersistentTransform.Init(ref sunCatcherPivot, this, loadedPanel.part.transform);
			}
			else
			{
				PersistentTransform.Init(ref sunCatcherPivot, this, loadedPanel.part.FindModelComponent<Transform>(loadedPanel.pivotName));
			}
		}

		protected override double GetOccludedFactor(Vector3d sunDir, out string occludingObjectTitle, out bool occluderIsPart)
		{
			occludingObjectTitle = null;
			occluderIsPart = false;

			if (!Physics.Raycast(sunCatcherPosition.Position + (sunDir * loadedPanel.raycastOffset), sunDir, out raycastHit, 10000f, raycastMask))
				return 1.0;

			Part hittedPart = FlightGlobals.GetPartUpwardsCached(raycastHit.transform.gameObject);

			if (hittedPart != null)
			{
				// avoid panels from occluding themselves
				if (hittedPart == loadedPanel.part)
					return 1.0;

				occludingObjectTitle = hittedPart.partInfo.title;
				occluderIsPart = true;
			}
			else
			{
				occludingObjectTitle = raycastHit.transform.gameObject.name;
			}

			return 0.0;
		}

		protected override double GetCosineFactor(Vector3d sunDir)
		{
			switch (panelType)
			{
				case PanelType.FlatTracking:
					if (!IsLoaded || isAnalytic)
						return Math.Cos((Math.PI * 0.5) - Math.Acos(Vector3d.Dot(sunDir, sunCatcherPivot.Up)));
					else
						return Math.Max(Vector3d.Dot(sunDir, sunCatcherPivot.Forward), 0.0);
				case PanelType.FlatNonTracking:
					return Math.Max(Vector3d.Dot(sunDir, sunCatcherPivot.Forward), 0.0);
				case PanelType.CylindricalPivot:
					return Math.Max((1.0 - Math.Abs(Vector3d.Dot(sunDir, sunCatcherPivot.Forward))) * (1.0 / Math.PI), 0.0);
				case PanelType.CylindricalPartX:
					return Math.Max((1.0 - Math.Abs(Vector3d.Dot(sunDir, sunCatcherPivot.Right))) * (1.0 / Math.PI), 0.0);
				case PanelType.CylindricalPartY:
					return Math.Max((1.0 - Math.Abs(Vector3d.Dot(sunDir, sunCatcherPivot.Forward))) * (1.0 / Math.PI), 0.0);
				case PanelType.CylindricalPartZ:
					return Math.Max((1.0 - Math.Abs(Vector3d.Dot(sunDir, sunCatcherPivot.Up))) * (1.0 / Math.PI), 0.0);
				case PanelType.Spherical:
					return 0.25;
				default:
					return 0.0;
			}
		}

		protected override PanelState GetState()
		{
			// Detect modified module (B9PS switching of the stock module or ROSolar built-in switching)
			if (loadedPanel.resHandler.outputResources[0].rate != nominalRate)
			{
				GetModuleTransformsAndRate();
			}

			if (!loadedPanel.useAnimation)
			{
				if (loadedPanel.deployState == ModuleDeployablePart.DeployState.BROKEN)
					return PanelState.Broken;

				return PanelState.Static;
			}

			switch (loadedPanel.deployState)
			{
				case ModuleDeployablePart.DeployState.EXTENDED:
					if (!IsRetractable()) return PanelState.ExtendedFixed;
					return PanelState.Extended;
				case ModuleDeployablePart.DeployState.RETRACTED: return PanelState.Retracted;
				case ModuleDeployablePart.DeployState.RETRACTING: return PanelState.Retracting;
				case ModuleDeployablePart.DeployState.EXTENDING: return PanelState.Extending;
				case ModuleDeployablePart.DeployState.BROKEN: return PanelState.Broken;
			}
			return PanelState.Unknown;
		}

		public override void OnSetTrackedBody(CelestialBody body)
		{
			loadedPanel.trackingBody = body;
			loadedPanel.GetTrackingBodyTransforms();
		}

		public override void OnFixedUpdate(double elapsedSec)
		{
			base.OnFixedUpdate(elapsedSec);

			// set the stock rate field (functionally not needed, but some mods might be checking it)
			if (IsLoaded)
				loadedPanel.flowRate = (float)currentOutput;
		}
	}

	// - Prevent the stock raycasts from running
	[HarmonyPatch(typeof(ModuleDeployableSolarPanel))]
	[HarmonyPatch(nameof(ModuleDeployableSolarPanel.CalculateTrackingLOS))]
	class ModuleDeployableSolarPanel_CalculateTrackingLOS
	{
		static bool Prefix(ref bool __result, ref string blocker)
		{
			__result = false;
			blocker = string.Empty;
			return false;
		}
	}

	// - Prevent the stock AoA and submerged tests from running
	// - Force all resource generation to 0
	[HarmonyPatch(typeof(ModuleDeployableSolarPanel))]
	[HarmonyPatch(nameof(ModuleDeployableSolarPanel.PostCalculateTracking))]
	class ModuleDeployableSolarPanel_PostCalculateTracking
	{
		static bool Prefix(ModuleDeployableSolarPanel __instance)
		{
			return false;
		}
	}
}
