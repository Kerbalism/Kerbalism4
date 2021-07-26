using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using KSP.UI.Screens;
using UnityEngine;
using KSP.Localization;
using KSP.UI;

namespace KERBALISM.Events
{
	public sealed class GameEventsHandler
	{
		private GameEventsHabitat gameEventsHabitat = new GameEventsHabitat();
		private GameEventsCrew gameEventsCrew = new GameEventsCrew();
		private GameEventsUI gameEventsUI = new GameEventsUI();
		private VesselLifecycle vesselLifecycle = new VesselLifecycle();
		private PartLifecycle partLifecycle = new PartLifecycle();
		private PartModuleLifecycle partModuleLifecycle = new PartModuleLifecycle();
		private ShipConstructLifecycle editorLifecycle = new ShipConstructLifecycle();

		public GameEventsHandler()
		{
			// HABITAT
			GameEvents.onCrewTransferred.Add(gameEventsHabitat.CrewTransferred);
			GameEvents.onCrewTransferSelected.Add(gameEventsHabitat.CrewTransferSelected);
			BaseCrewAssignmentDialog.onCrewDialogChange.Add(gameEventsHabitat.EditorCrewChanged);

			// CREW
			GameEvents.onKerbalLevelUp.Add(gameEventsCrew.OnKerbalLevelUp);
			GameEvents.onCrewOnEva.Add(gameEventsCrew.ToEVA);
			GameEvents.onCrewBoardVessel.Add(gameEventsCrew.FromEVA);
			GameEvents.onAttemptEva.Add(gameEventsCrew.AttemptEVA);

			// VESSEL
			GameEvents.onVesselRecovered.Add(vesselLifecycle.VesselRecovered);
			GameEvents.onVesselTerminated.Add(vesselLifecycle.VesselTerminated);
			GameEvents.onVesselWillDestroy.Add(vesselLifecycle.VesselDestroyed);
			GameEvents.onNewVesselCreated.Add(vesselLifecycle.VesselCreated);
			GameEvents.onPartCouple.Add(vesselLifecycle.VesselDock);
			GameEvents.onVesselChange.Add(vesselLifecycle.OnVesselModified);
			GameEvents.onVesselStandardModification.Add(vesselLifecycle.OnVesselStandardModification);
			GameEvents.onPartCouple.Add(vesselLifecycle.OnPartCouple);
			GameEvents.onVesselRecoveryProcessingComplete.Add(vesselLifecycle.OnVesselRecoveryProcessingComplete);

			// PART
			GameEvents.onPartDestroyed.Add(partLifecycle.OnPartDestroyed);

			// EDITOR
			GameEvents.onEditorShipModified.Add(editorLifecycle.OnEditorShipModified);
			GameEvents.onEditorPartEvent.Add(editorLifecycle.OnEditorPartEvent);

			// UI
			GameEvents.onGUIEditorToolbarReady.Add(gameEventsUI.AddEditorCategory);

			GameEvents.onGUIAdministrationFacilitySpawn.Add(() => gameEventsUI.uiVisible = false);
			GameEvents.onGUIAdministrationFacilityDespawn.Add(() => gameEventsUI.uiVisible = true);

			GameEvents.onGUIAstronautComplexSpawn.Add(() => gameEventsUI.uiVisible = false);
			GameEvents.onGUIAstronautComplexDespawn.Add(() => gameEventsUI.uiVisible = true);

			GameEvents.onGUIMissionControlSpawn.Add(() => gameEventsUI.uiVisible = false);
			GameEvents.onGUIMissionControlDespawn.Add(() => gameEventsUI.uiVisible = true);

			GameEvents.onGUIRnDComplexSpawn.Add(() => gameEventsUI.uiVisible = false);
			GameEvents.onGUIRnDComplexDespawn.Add(() => gameEventsUI.uiVisible = true);

			GameEvents.onHideUI.Add(() => gameEventsUI.uiVisible = false);
			GameEvents.onShowUI.Add(() => gameEventsUI.uiVisible = true);

			GameEvents.onGUILaunchScreenSpawn.Add((_) => gameEventsUI.uiVisible = false);
			GameEvents.onGUILaunchScreenDespawn.Add(() => gameEventsUI.uiVisible = true);

			GameEvents.onGameSceneSwitchRequested.Add((_) => gameEventsUI.uiVisible = false);
			GameEvents.onGUIApplicationLauncherReady.Add(() => gameEventsUI.uiVisible = true);
		}
	}
}
