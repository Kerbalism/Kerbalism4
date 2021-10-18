﻿using System.Collections.Generic;
using UnityEngine;

namespace KERBALISM
{
	public class KerbalEVAHandler : TypedModuleHandler<KerbalEVA>
	{
		public override ActivationContext Activation => ActivationContext.Loaded;

		private KerbalData kerbal;
		private bool isDead;

		private Recipe headLampRecipe;
		private Light headLampLight;
		private List<Renderer> headLampFlareComponents;

		public override void OnStart()
		{
			// determine if headlamps need ec
			// - not required if there is no EC capacity in eva kerbal (no ec supply in profile)
			// - not required if no EC cost for headlamps is specified (set by the user)
			if (VesselData.ResHandler.ElectricCharge.Capacity > 0.0 && Settings.HeadLampsECCost > 0.0)
			{
				headLampLight = loadedModule.headLamp.GetComponent<Light>();

				headLampFlareComponents = new List<Renderer>();
				foreach (Renderer renderer in loadedModule.GetComponentsInChildren<Renderer>())
				{
					if (renderer.name == "flare1" || renderer.name == "flare2")
					{
						headLampFlareComponents.Add(renderer);
					}
				}

				headLampRecipe = new Recipe("Head lamp", RecipeCategory.Light, OnRecipeExecuted);
				headLampRecipe.AddInput(VesselResHandler.ElectricChargeId, Settings.HeadLampsECCost);
			}

			kerbal = DB.GetOrCreateKerbalData(partData.LoadedPart.protoModuleCrew[0]);
			isDead = false; // will be synchronized in the first FU in case kerbal.evaDead is true
		}

		public override void OnUpdate(double elapsedSec)
		{
			if (headLampRecipe != null && loadedModule.lampOn)
			{
				headLampRecipe.RequestExecution(VesselData.ResHandler);
			}

			if (isDead && loadedModule.vessel.isActiveVessel)
			{
				InputLockManager.SetControlLock(ControlTypes.EVA_INPUT, "eva_dead_lock");
			}

			// set dead state if necessary, but wait until the fsm is properly initialized
			if (!isDead && kerbal.isEvaDead && !string.IsNullOrEmpty(loadedModule.fsm.currentStateName))
			{
				SetDeadState(loadedModule);
				isDead = true;
			}

		}

		public void OnRecipeExecuted(double elapsedSec)
		{
			headLampLight.intensity = (float)headLampRecipe.ExecutedFactor;
			bool enableFlare = headLampRecipe.ExecutedFactor > 0.5;

			foreach (Renderer renderer in headLampFlareComponents)
				renderer.enabled = enableFlare;
		}

		private void SetDeadState(KerbalEVA kerbal)
		{
			// set kerbal to the 'freezed' unescapable state
			// how it works:
			// - kerbal animations and ragdoll state are driven by a finite-state-machine (FSM)
			// - this function is called every frame for all active eva kerbals flagged as dead
			// - if the FSM current state is already 'freezed', we do nothing and this function is a no-op
			// - we create an 'inescapable' state called 'freezed'
			// - we switch the FSM to that state using an ad-hoc event from current state
			// - once the 'freezed' state is set, the FSM cannot switch to any other states
			// - the animator of the object is stopped to stop any left-over animations from previous state

			// do nothing if already freezed
			if (kerbal.fsm.currentStateName != "freezed")
			{
				// create freezed state
				KFSMState freezed = new KFSMState("freezed");

				// create freeze event
				KFSMEvent eva_freeze = new KFSMEvent("EVAfreeze")
				{
					GoToStateOnEvent = freezed,
					updateMode = KFSMUpdateMode.MANUAL_TRIGGER
				};
				kerbal.fsm.AddEvent(eva_freeze, kerbal.fsm.CurrentState);

				// trigger freeze event
				kerbal.fsm.RunEvent(eva_freeze);

				// stop animations
				kerbal.GetComponent<Animation>().Stop();
			}

			// disable modules
			foreach (PartModule m in kerbal.part.Modules)
			{
				// ignore KerbalEVA itself
				if (m.moduleName == "KerbalEVA") continue;

				// keep the flag decal
				if (m.moduleName == "FlagDecal") continue;

				// disable all other modules
				m.isEnabled = false;
				m.enabled = false;
			}

			// remove plant flag action
			kerbal.flagItems = 0;
		}
	}
}
