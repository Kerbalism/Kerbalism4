using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;

namespace KERBALISM
{
	public class ModuleDeployableSolarPanelHandler : SolarPanelHandlerBase
	{
		private static readonly string[] moduleNames;

		static ModuleDeployableSolarPanelHandler()
		{
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

		private Transform sunCatcherPosition;   // middle point of the panel surface (usually). Use only position, panel surface direction depend on the pivot transform, even for static panels.
		private Transform sunCatcherPivot;      // If it's a tracking panel, "up" is the pivot axis and "position" is the pivot position. In any case "forward" is the panel surface normal.

		public override void OnStart()
		{
			base.OnStart();

			if (IsLoaded)
			{
				loadedPanel = (ModuleDeployableSolarPanel)loadedModule;

				// hide stock ui
				loadedPanel.Fields["sunAOA"].guiActive = false;
				loadedPanel.Fields["flowRate"].guiActive = false;
				loadedPanel.Fields["status"].guiActive = false;

				GetModuleTransformsAndRate();
			}
		}

		private void GetModuleTransformsAndRate()
		{
			if (sunCatcherPivot == null)
				sunCatcherPivot = loadedPanel.part.FindModelComponent<Transform>(loadedPanel.pivotName);
			if (sunCatcherPosition == null)
				sunCatcherPosition = loadedPanel.part.FindModelTransform(loadedPanel.secondaryTransformName);

			if (sunCatcherPosition == null)
			{
				Lib.Log($"Could not find suncatcher transform `{loadedPanel.secondaryTransformName}` in part `{loadedPanel.part.name}`", Lib.LogLevel.Error);
				handlerIsEnabled = false;
				return;
			}

			// avoid rate lost due to OnStart being called multiple times in the editor
			if (loadedPanel.resHandler.outputResources[0].rate == 0.0)
				return;

			// reset target module rate
			// - This can break mods that evaluate solar panel output for a reason or another (eg: AmpYear, BonVoyage).
			//   We fix that by exploiting the fact that resHandler was introduced in KSP recently, and most of
			//   these mods weren't updated to reflect the changes or are not aware of them, and are still reading
			//   chargeRate. However the stock solar panel ignore chargeRate value during FixedUpdate.
			//   So we only reset resHandler rate.
			nominalRate = loadedPanel.resHandler.outputResources[0].rate;
			loadedPanel.resHandler.outputResources[0].rate = 0.0;
		}

		protected override double GetOccludedFactor(Vector3d sunDir, out string occludingPart, bool analytic = false)
		{
			double occludingFactor = 1.0;
			occludingPart = null;
			RaycastHit raycastHit;
			if (analytic)
			{
				if (sunCatcherPosition == null)
					sunCatcherPosition = loadedPanel.part.FindModelTransform(loadedPanel.secondaryTransformName);

				Physics.Raycast(sunCatcherPosition.position + (sunDir * loadedPanel.raycastOffset), sunDir, out raycastHit, 10000f);
			}
			else
			{
				raycastHit = loadedPanel.hit;
			}

			if (raycastHit.collider != null)
			{
				Part blockingPart = Part.GetComponentUpwards<Part>(raycastHit.collider.gameObject);
				if (blockingPart != null)
				{
					// avoid panels from occluding themselves
					if (blockingPart == loadedPanel.part)
						return occludingFactor;

					occludingPart = blockingPart.partInfo.title;
				}
				occludingFactor = 0.0;
			}
			return occludingFactor;
		}

		protected override double GetCosineFactor(Vector3d sunDir, bool analytic = false)
		{
			switch (loadedPanel.panelType)
			{
				case ModuleDeployableSolarPanel.PanelType.FLAT:
					if (!analytic)
						return Math.Max(Vector3d.Dot(sunDir, loadedPanel.trackingDotTransform.forward), 0.0);

					if (loadedPanel.isTracking)
						return Math.Cos(1.57079632679 - Math.Acos(Vector3d.Dot(sunDir, sunCatcherPivot.up)));
					else
						return Math.Max(Vector3d.Dot(sunDir, sunCatcherPivot.forward), 0.0);

				case ModuleDeployableSolarPanel.PanelType.CYLINDRICAL:
					return Math.Max((1.0 - Math.Abs(Vector3d.Dot(sunDir, loadedPanel.trackingDotTransform.forward))) * (1.0 / Math.PI), 0.0);
				case ModuleDeployableSolarPanel.PanelType.SPHERICAL:
					return 0.25;
				default:
					return 0.0;
			}
		}

		protected override PanelState GetState()
		{
			// Detect modified TotalEnergyRate (B9PS switching of the stock module or ROSolar built-in switching)
			if (loadedPanel.resHandler.outputResources[0].rate != 0.0)
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

		protected override void OnLoadedUpdate()
		{
			loadedPanel.flowRate = (float)currentOutput;
		}
	}
}
