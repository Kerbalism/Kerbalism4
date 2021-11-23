using System;
using System.Collections.Generic;
using UnityEngine;
using static KERBALISM.HabitatHandler;

namespace KERBALISM
{
	/// <summary>
	/// loaded/unloaded/editor state independant persisted data and logic used by the ModuleKsmHabitat module.
	/// </summary>
	public class HabitatHandler : KsmModuleHandler<ModuleKsmHabitat, HabitatHandler, HabitatDefinition>,
		IRadiationReceiver, IMultipleRecipeExecutedCallback
	{
		#region ENUMS AND TYPES

		public enum PressureState
		{
			Pressurized,
			Breatheable,
			AlwaysDepressurized,
			Depressurized,
			Pressurizing,
			DepressurizingAboveThreshold,
			DepressurizingBelowThreshold
		}

		public enum AnimState
		{
			Retracted,
			Deploying,
			Retracting,
			Deployed,
			Accelerating,
			Decelerating,
			Rotating,
			RotatingNotEnoughEC,
			Stuck

		}

		public enum Comfort
		{
			firmGround = 1 << 0,
			notAlone = 1 << 1,
			callHome = 1 << 2,
			exercice = 1 << 3,
			panorama = 1 << 4,
			plants = 1 << 5
		}

		#endregion

		#region FIELDS

		/// <summary> can the habitat be occupied and does it count for global pressure/volume/comforts/radiation </summary>
		public bool isEnabled = false;

		/// <summary> pressure state </summary>
		public PressureState pressureState = PressureState.AlwaysDepressurized;

		public AnimState animState = AnimState.Retracted;

		/// <summary> crew count </summary>
		public int crewCount = 0;

		/// <summary> current atmosphere count (1 unit = 1 m3 of air at STP) </summary>
		public double atmoAmount = 0.0;

		/// <summary> current % of poisonous atmosphere (CO2)</summary>
		public double wasteLevel = 0.0; 

		/// <summary> current shielding count (1 unit = 1 m2 of fully shielded surface, see Radiation.ShieldingEfficiency) </summary>
		public double shieldingAmount = 0.0;

		/// <summary> used to know when to consume ec for deploy/retract and accelerate/decelerate centrifuges</summary>
		public float animTimer = 0f;

		/// <summary> artifical gravity from the centrifuge, or from the vessel rotation</summary>
		public double gravity;

		private PartResourceWrapper atmoRes;
		private PartResourceWrapper wasteRes;
		private PartResourceWrapper shieldRes;
		private VesselResourceKSP atmoResInfo;
		private VesselResourceKSP wasteResInfo;
		private bool isEditor;

		private List<Recipe> executedRecipes = new List<Recipe>(3);
		public Recipe deployRecipe;
		public Recipe centrifugeRecipe;
		public RecipeInput centrifugeECInput;
		public Recipe depressurizationRecipe;
		public RecipeInput depressurizationECInput;
		public RecipeLocalInput depressurizationAtmoInput;
		public RecipeLocalInput depressurizationWasteInput;
		public RecipeOutput depressurizationAtmoOutput;


		#endregion

		#region PROPERTIES

		public PartResourceWrapper AtmoRes => atmoRes;
		public PartResourceWrapper WasteRes => wasteRes;
		public PartResourceWrapper ShieldRes => shieldRes;

		public bool IsDeployed
		{
			get
			{
				switch (animState)
				{
					case AnimState.Deployed:
					case AnimState.Accelerating:
					case AnimState.Decelerating:
					case AnimState.Rotating:
					case AnimState.RotatingNotEnoughEC:
					case AnimState.Stuck:
						return true;
					default:
						return false;
				}
			}
		}

		public bool IsDeployingRequested
		{
			get
			{
				switch (animState)
				{
					case AnimState.Deploying:
					case AnimState.Deployed:
					case AnimState.Accelerating:
					case AnimState.Decelerating:
					case AnimState.Rotating:
					case AnimState.RotatingNotEnoughEC:
					case AnimState.Stuck:
						return true;
					default:
						return false;
				}
			}
		}

		public bool IsRotationNominal => animState == AnimState.Rotating;
		public bool IsAccelerating => animState == AnimState.Accelerating;
		public bool IsDecelerating => animState == AnimState.Decelerating;
		public bool IsStuck => animState == AnimState.Stuck;

		public bool IsRotationEnabled
		{
			get
			{
				switch (animState)
				{
					case AnimState.Accelerating:
					case AnimState.Rotating:
					case AnimState.RotatingNotEnoughEC:
						return true;
					default:
						return false;
				}
			}
		}

		public bool IsRotationStopped
		{
			get
			{
				switch (animState)
				{
					case AnimState.Retracted:
					case AnimState.Retracting:
					case AnimState.Deploying:
					case AnimState.Deployed:
						return true;
					default:
						return false;
				}
			}
		}

		/// <summary>
		/// Is the habitat pressurized above the pressure threshold
		/// Note that when false, it doesn't mean kerbals need to be in their suits if they are in breathable atmosphere.
		/// </summary>
		public bool IsPressurizedAboveThreshold
		{
			get
			{
				switch (pressureState)
				{
					case PressureState.Pressurized:
					case PressureState.DepressurizingAboveThreshold:
						return true;
					default:
						return false;
				}
			}
		}

		public bool IsPressurizationRequested
		{
			get
			{
				switch (pressureState)
				{
					case PressureState.Pressurized:
					case PressureState.Pressurizing:
						return true;
					default:
						return false;
				}
			}
		}


		/// <summary>
		/// Are suits required. Note that this doesn't mean the habitat is depressurized.
		/// </summary>
		public bool RequireHelmet
		{
			get
			{
				switch (pressureState)
				{
					case PressureState.AlwaysDepressurized:
					case PressureState.Depressurized:
					case PressureState.Pressurizing:
					case PressureState.DepressurizingBelowThreshold:
						return true;
					default:
						return false;
				}
			}
		}

		/// <summary>
		/// Is the habitat at zero pressure ?
		/// </summary>
		public bool IsFullyDepressurized
		{
			get
			{
				switch (pressureState)
				{
					case PressureState.AlwaysDepressurized:
					case PressureState.Depressurized:
						return true;
					default:
						return false;
				}
			}
		}

		public bool IsConsideredForHabitatRadiation
		{
			get
			{
				switch (pressureState)
				{
					case PressureState.AlwaysDepressurized:
					case PressureState.Depressurized:
					case PressureState.Pressurizing:
					case PressureState.DepressurizingBelowThreshold:
						return isEnabled && crewCount > 0;
					default:
						return isEnabled;
				}
			}
		}

		// IRadiationReceiver implementation
		public PartRadiationData RadiationData => partData.radiationData;
		public bool EnableInterface => true;

		#endregion

		#region LIFECYCLE

		public override void OnFirstSetup()
		{
			if (loadedModule != null)
				crewCount = Lib.CrewCount(loadedModule.part);
			else
				crewCount = Lib.CrewCount(partData.ProtoPart);

			animState = definition.isDeployable ? AnimState.Retracted : AnimState.Deployed;
			isEnabled = !definition.isDeployable;

			// add atmo, waste, reclaim and shielding resources to the part
			double volumeLiters = HabitatLib.M3ToL(definition.volume);
			partData.resources.AddResource(Settings.HabitatAtmoResource, volumeLiters, volumeLiters);
			partData.resources.AddResource(Settings.HabitatWasteResource, 0.0, volumeLiters);

			if (definition.hasShielding)
				partData.resources.AddResource(definition.shieldingResource, 0.0, definition.surface * definition.maxShieldingFactor);

			if (definition.canPressurize && definition.reclaimStorageFactor > 0.0)
			{
				double capacity = volumeLiters * definition.reclaimStorageFactor;
				partData.resources.AddResource(definition.reclaimResource, capacity, capacity);
			}

			if (Lib.IsEditor)
			{
				if (!definition.canPressurize)
					pressureState = PressureState.AlwaysDepressurized;
				else if (definition.isDeployable && !IsDeployed)
					pressureState = PressureState.Depressurized;
				else
					pressureState = PressureState.Pressurized;
			}
			// part was created in flight (rescue, KIS...)
			else
			{
				// if part is manned (rescue vessel), force enabled and deployed
				if (crewCount > 0)
				{
					animState = AnimState.Deployed;
					isEnabled = true;
				}

				// don't pressurize. if it's a rescue, the player will likely go on EVA immediatly anyway, and in a case of a
				// part that was created in flight, it doesn't make sense to have it pre-pressurized.
				pressureState = definition.canPressurize ? PressureState.Depressurized : PressureState.AlwaysDepressurized;
			}
		}

		public override void OnStart()
		{
			isEditor = Lib.IsEditor;

			foreach (PartResourceWrapper resource in partData.resources)
			{
				if (resource.ResName == Settings.HabitatAtmoResource)
					atmoRes = resource;
				else if (resource.ResName == Settings.HabitatWasteResource)
					wasteRes = resource;
				else if (definition.hasShielding && resource.ResName == definition.shieldingResource)
					shieldRes = resource;
			}

			atmoResInfo = VesselData.ResHandler.GetKSPResource(Settings.HabitatAtmoResourceId);
			wasteResInfo = VesselData.ResHandler.GetKSPResource(Settings.HabitatWasteResourceId);

			switch (pressureState)
			{
				case PressureState.Pressurized: PressurizingEndEvt(); break;
				case PressureState.Pressurizing: PressurizingStartEvt(); break;
				case PressureState.DepressurizingAboveThreshold: DepressurizingStartEvt(); break;
				case PressureState.DepressurizingBelowThreshold: DepressurizingPassThresholdEvt(); break;
				case PressureState.Depressurized: DepressurizingEndEvt(); break;
				case PressureState.Breatheable: BreatheableStartEvt(); break;
				case PressureState.AlwaysDepressurized: AlwaysDepressurizedStartEvt(); break;
			}

			if (definition.deployECRate > 0.0)
			{
				deployRecipe = new Recipe(partData.Title, RecipeCategory.Deployement, OnDeployRecipeExecuted);
				deployRecipe.AddInput(VesselResHandler.ElectricChargeId, definition.deployECRate);
			}

			if (definition.isCentrifuge)
			{
				centrifugeRecipe = new Recipe(partData.Title, RecipeCategory.Centrifuge, OnCentrifugeRecipeExecuted);
				centrifugeECInput = centrifugeRecipe.AddInput(VesselResHandler.ElectricChargeId, definition.rotateECRate);
			}

			if (definition.canPressurize && definition.depressurizationRate > 0.0)
			{
				depressurizationRecipe = new Recipe(partData.Title, RecipeCategory.Pressure, OnDepressurizationRecipeExecuted);

				depressurizationAtmoInput = depressurizationRecipe.AddLocalInput(atmoRes, definition.depressurizationRate);
				depressurizationWasteInput = depressurizationRecipe.AddLocalInput(wasteRes, definition.depressurizationRate);

				if (definition.depressurizeECRate > 0.0)
				{
					depressurizationECInput = depressurizationRecipe.AddInput(VesselResHandler.ElectricChargeId, definition.depressurizeECRate);
				}

				if (definition.reclaimFactor > 0.0)
				{
					depressurizationAtmoOutput = depressurizationRecipe.AddOutput(definition.reclaimResource, definition.depressurizationRate, true, false);
				}
			}
		}

		public override void OnLoad(ConfigNode node)
		{
			isEnabled = Lib.ConfigValue(node, "habitatEnabled", isEnabled);
			pressureState = Lib.ConfigEnum(node, "pressureState", pressureState);
			animState = Lib.ConfigEnum(node, "animState", animState);
			crewCount = Lib.ConfigValue(node, "crewCount", crewCount);
			atmoAmount = Lib.ConfigValue(node, "atmoAmount", atmoAmount);
			wasteLevel = Lib.ConfigValue(node, "wasteLevel", wasteLevel);
			shieldingAmount = Lib.ConfigValue(node, "shieldingAmount", shieldingAmount);
			animTimer = Lib.ConfigValue(node, "animTimer", animTimer);
			gravity = Lib.ConfigValue(node, "gravity", gravity);
		}

		public override void OnSave(ConfigNode node)
		{
			node.AddValue("habitatEnabled", isEnabled);
			node.AddValue("pressureState", pressureState.ToString());
			node.AddValue("animState", animState.ToString());
			node.AddValue("crewCount", crewCount);
			node.AddValue("atmoAmount", atmoAmount);
			node.AddValue("wasteLevel", wasteLevel);
			node.AddValue("shieldingAmount", shieldingAmount);
			node.AddValue("animTimer", animTimer);
			node.AddValue("gravity", gravity);
		}

		#endregion

		#region UPDATE

		public override void OnUpdate(double elapsedSec)
		{
			isEditor = Lib.IsEditor;
			AnimationsUpdate(elapsedSec);
			PressureUpdate(elapsedSec);
			shieldingAmount = definition.hasShielding ? shieldRes.Amount : 0.0;

			if (definition.isCentrifuge)
			{
				if (IsLoaded)
				{
					gravity = definition.centrifugeGravity * (loadedModule.rotateAnimator.CurrentSpeed / loadedModule.rotateAnimator.NominalSpeed);
				}
				else
				{
					gravity = IsRotationNominal ? definition.centrifugeGravity : 0.0;
				}
			}
			else if (IsLoaded)
			{
				Rigidbody rb = partData.LoadedPart.rb;
				gravity = rb == null ? 0.0 : (rb.angularVelocity.magnitude * rb.velocity.magnitude) / PhysicsGlobals.GravitationalAcceleration;
			}
		}

		private void AnimationsUpdate(double elapsed_s)
		{
			// animations state machine
			if (partData.IsLoaded)
			{
				switch (animState)
				{
					case AnimState.Deploying:
						if (definition.deployWithPressure)
							break;

						if (loadedModule.deployAnimator.Playing)
						{
							if (!isEditor && deployRecipe != null)
								deployRecipe.RequestExecution(VesselData.ResHandler);
							
							animTimer = loadedModule.deployAnimator.AnimDuration * (1f - loadedModule.deployAnimator.NormalizedTime);
						}
						else
						{
							animState = AnimState.Deployed;
							animTimer = 0f;

							if (loadedModule.part.internalModel != null)
								loadedModule.part.internalModel.SetVisible(true);

							ModuleKsmHabitat.TryToggleHabitat(loadedModule, this, true);

						}
						break;
					case AnimState.Retracting:

						bool isRetracted = false;
						if (definition.deployWithPressure)
						{
							if (isEditor)
							{
								if (!loadedModule.deployAnimator.Playing)
									isRetracted = true;
							}
							else
							{
								if (animTimer <= 0f)
									isRetracted = true;
							}
						}
						else
						{
							if (loadedModule.deployAnimator.Playing)
							{
								if (!isEditor && deployRecipe != null)
									deployRecipe.RequestExecution(VesselData.ResHandler);
								
								animTimer = loadedModule.deployAnimator.AnimDuration * loadedModule.deployAnimator.NormalizedTime;
							}
							else
							{
								isRetracted = true;
							}
						}

						if (isRetracted)
						{
							animTimer = 0f;
							animState = AnimState.Retracted;
						}
						break;
					case AnimState.Accelerating:
						if (loadedModule.rotateAnimator.IsSpinningNominal)
						{
							if (!isEditor)
							{
								centrifugeECInput.NominalRate = definition.rotateECRate;
								centrifugeRecipe.RequestExecution(VesselData.ResHandler);
							}

							animState = AnimState.Rotating;
						}
						else if (loadedModule.rotateAnimator.IsStopped)
						{
							animState = AnimState.Stuck;
						}
						else
						{
							if (!isEditor)
							{
								centrifugeECInput.NominalRate = definition.accelerateECRate;
								centrifugeRecipe.RequestExecution(VesselData.ResHandler);
							}
						}
						break;
					case AnimState.Decelerating:
						if (loadedModule.rotateAnimator.IsStopped)
							animState = AnimState.Deployed;

						break;
					case AnimState.Rotating:
					case AnimState.RotatingNotEnoughEC:
						if (!isEditor)
							centrifugeRecipe.RequestExecution(VesselData.ResHandler);

						break;
				}
			}
			else
			{
				switch (animState)
				{
					case AnimState.Deploying:

						if (!definition.deployWithPressure)
						{
							if (deployRecipe != null)
								deployRecipe.RequestExecution(VesselData.ResHandler);
							else
								animTimer -= (float) elapsed_s;
						}

						if (animTimer <= 0f)
							animState = AnimState.Deployed;

						break;

					case AnimState.Retracting:

						if (definition.deployWithPressure)
						{
							if (animTimer <= 0f)
							{
								animTimer = 0f;
								animState = AnimState.Retracted;
							}
						}
						else
						{
							if (deployRecipe != null)
								deployRecipe.RequestExecution(VesselData.ResHandler);
							else
								animTimer -= (float)elapsed_s;

							if (animTimer <= 0f)
								animState = AnimState.Retracted;
						}
						break;
					case AnimState.Accelerating:
						centrifugeECInput.NominalRate = definition.accelerateECRate;
						centrifugeRecipe.RequestExecution(VesselData.ResHandler);
						break;
					case AnimState.Decelerating:

						animTimer -= (float)elapsed_s;

						if (animTimer <= 0.0)
							animState = AnimState.Deployed;

						break;
					case AnimState.Rotating:
						centrifugeRecipe.RequestExecution(VesselData.ResHandler);
						break;
				}
			}
		}

		private void PressureUpdate(double elapsed_s)
		{
			switch (pressureState)
			{
				case PressureState.Pressurized:
					// if pressure drop below the minimum habitable pressure, switch to partial pressure state
					if (atmoRes.Amount / atmoRes.Capacity < Settings.PressureThreshold)
						PressureDroppedEvt();
					break;
				case PressureState.Breatheable:

					//if (!isEditor)
					//	atmoResInfo.equalizeMode = ResourceInfo.EqualizeMode.Disabled;

					if (!isEditor && !VesselData.EnvInBreathableAtmosphere)
					{
						if (definition.canPressurize)
							PressureDroppedEvt();
						else
							AlwaysDepressurizedStartEvt();
						break;
					}

					// equalize pressure with external pressure
					atmoRes.Amount = Math.Min(VesselData.EnvStaticPressure * atmoRes.Capacity, atmoRes.Capacity);

					break;
				case PressureState.AlwaysDepressurized:

					if (!isEditor)
					{
						if (VesselData.EnvInBreathableAtmosphere)
						{
							BreatheableStartEvt();
							break;
						}
						else if (VesselData.EnvInOxygenAtmosphere)
						{
							atmoRes.Amount = Math.Min(VesselData.EnvStaticPressure * atmoRes.Capacity, atmoRes.Capacity);
						}
						else
						{
							atmoRes.Amount = 0.0;
						}
					}

					break;

				case PressureState.Depressurized:

					if (!isEditor)
					{
						if (!IsDeployed)
						{
							break;
						}

						if (VesselData.EnvInBreathableAtmosphere)
						{
							BreatheableStartEvt();
							break;
						}
						else if (VesselData.EnvInOxygenAtmosphere)
						{
							atmoRes.Amount = Math.Min(VesselData.EnvStaticPressure * atmoRes.Capacity, atmoRes.Capacity);
						}
					}
					break;
				case PressureState.Pressurizing:

					if (!isEditor)
						atmoResInfo.equalizeMode = VesselResourceKSP.EqualizeMode.Disabled;

					if (definition.deployWithPressure && animState == AnimState.Deploying)
					{
						float deployPercent = (float)Math.Min(atmoRes.Amount / (atmoRes.Capacity * Settings.PressureThreshold), 1.0);

						if (partData.IsLoaded)
							loadedModule.deployAnimator.Still(deployPercent);

						animTimer = modulePrefab.deployAnimator.AnimDuration * (1f - deployPercent);
					}

					// if pressure go back to the minimum habitable pressure, switch to pressurized state
					if (atmoRes.Amount / atmoRes.Capacity > Settings.PressureThreshold)
						PressurizingEndEvt();

					break;
				case PressureState.DepressurizingAboveThreshold:
				case PressureState.DepressurizingBelowThreshold:

					// if fully depressurized, go to the depressurized state
					if (atmoRes.Amount == 0.0)
					{
						DepressurizingEndEvt();
						break;
					}
					// if external pressure is less than the hab pressure, stop depressurization and go to the breathable state
					else if (VesselData.EnvInOxygenAtmosphere && atmoRes.Level < VesselData.EnvStaticPressure && IsDeployed)
					{
						DepressurizingEndEvt();
						break;
					}
					// pressure is going below the survivable threshold : time for kerbals to put their helmets
					else if (pressureState == PressureState.DepressurizingAboveThreshold && atmoRes.Level < Settings.PressureThreshold)
					{
						DepressurizingPassThresholdEvt();
					}

					bool isReclaiming = definition.reclaimFactor > 0.0 && atmoRes.Level >= 1.0 - definition.reclaimFactor;
					bool isInflatableRetracting = definition.isDeployable && definition.deployWithPressure && animState == AnimState.Retracting;

					// consume EC if we are reclaiming, or if we are deflating an inflatable
					if (depressurizationECInput != null)
					{
						if (isReclaiming || isInflatableRetracting)
							depressurizationECInput.NominalRate = definition.depressurizeECRate;
						else
							depressurizationECInput.NominalRate = 0.0;
					}

					// produce nitrogen back if we are reclaiming
					if (isReclaiming && depressurizationAtmoOutput != null)
					{
						if (isReclaiming)
							depressurizationAtmoOutput.NominalRate = depressurizationAtmoInput.NominalRate;
						else
							depressurizationAtmoOutput.NominalRate = 0.0;
					}

					// we only vent CO2 when the kerbals aren't yet in their helmets
					if (pressureState == PressureState.DepressurizingAboveThreshold)
						depressurizationWasteInput.NominalRate = depressurizationAtmoInput.NominalRate * (wasteRes.Amount / atmoRes.Amount);
					else
						depressurizationWasteInput.NominalRate = 0.0; // Nope, rate of zero doesn't work

					depressurizationRecipe.RequestExecution(VesselData.ResHandler);
					break;
			}

			// synchronize resource amounts to the persisted data
			atmoAmount = HabitatLib.LToM3(atmoRes.Amount);
			wasteLevel = wasteRes.Capacity > 0.0 ? wasteRes.Amount / wasteRes.Capacity : 0.0;

			// set equalizaton mode if it hasn't been explictely disabled in the breathable / depressurizing states
			if (!isEditor)
			{
				if (atmoResInfo.equalizeMode == VesselResourceKSP.EqualizeMode.NotSet)
					atmoResInfo.equalizeMode = VesselResourceKSP.EqualizeMode.Enabled;

				if (wasteResInfo.equalizeMode == VesselResourceKSP.EqualizeMode.NotSet)
					wasteResInfo.equalizeMode = VesselResourceKSP.EqualizeMode.Enabled;
			}
		}

		private void PressureDroppedEvt()
		{
			ShowHelmets(true);

			// go to pressurizing state
			pressureState = PressureState.Pressurizing;
		}

		public void BreatheableStartEvt()
		{
			atmoRes.FlowState = false;

			wasteRes.Capacity = HabitatLib.M3ToL(definition.volume);
			wasteRes.FlowState = false;

			ShowHelmets(false);

			pressureState = PressureState.Breatheable;
		}

		public void AlwaysDepressurizedStartEvt()
		{
			atmoRes.FlowState = false;

			wasteRes.Capacity = crewCount * Settings.PressureSuitVolume;
			wasteRes.FlowState = true;

			ShowHelmets(true);

			pressureState = PressureState.AlwaysDepressurized;
		}

		public void PressurizingStartEvt()
		{
			atmoRes.FlowState = true;

			if (!isEditor)
			{
				atmoResInfo.equalizeMode = VesselResourceKSP.EqualizeMode.Disabled;
				pressureState = PressureState.Pressurizing;
			}
			else
			{
				PressurizingEndEvt();
			}
		}

		public void PressurizingEndEvt()
		{
			wasteRes.Capacity = HabitatLib.M3ToL(definition.volume);
			wasteRes.FlowState = true;
			atmoRes.FlowState = true;

			if (!isEditor)
			{
				ShowHelmets(false);

				if (definition.deployWithPressure && animState == AnimState.Deploying)
				{
					if (partData.IsLoaded)
					{
						loadedModule.deployAnimator.Still(1f);
						animState = AnimState.Deployed;
						ModuleKsmHabitat.TryToggleHabitat(modulePrefab, this, true);
					}
					else
					{
						animState = AnimState.Deployed;
						ModuleKsmHabitat.TryToggleHabitat(loadedModule, this, true);
					}
				}
			}
			else
			{
				atmoRes.Amount = HabitatLib.M3ToL(definition.volume);

				if (definition.isDeployable && definition.deployWithPressure && !IsDeployed)
					loadedModule.deployAnimator.Play(false, false, loadedModule.OnDeployCallback, 5f);
			}

			pressureState = PressureState.Pressurized;
		}

		public void DepressurizingStartEvt()
		{
			atmoRes.FlowState = false;
			wasteRes.FlowState = false;

			if (isEditor)
				DepressurizingEndEvt();
			else if (atmoRes.Amount / atmoRes.Capacity >= Settings.PressureThreshold)
				pressureState = PressureState.DepressurizingAboveThreshold;
			else
				DepressurizingPassThresholdEvt();
		}

		public void DepressurizingPassThresholdEvt()
		{
			atmoRes.FlowState = false;

			double suitsVolume = crewCount * Settings.PressureSuitVolume;
			// make the CO2 level in the suit the same as the current CO2 level in the part by adjusting the amount
			// We only do it if the part is crewed, because since it discard nearly all the CO2 in the part, it can
			// be exploited to remove CO2, by stopping the depressurization immediatly.
			if (crewCount > 0)
				wasteRes.Amount *= suitsVolume / wasteRes.Capacity;

			wasteRes.Capacity = suitsVolume;
			wasteRes.FlowState = true; // kerbals are now in their helmets and CO2 won't be vented anymore, let the suits CO2 level equalize with the vessel CO2 level

			ShowHelmets(true);

			pressureState = PressureState.DepressurizingBelowThreshold;
		}

		public void DepressurizingEndEvt()
		{
			atmoRes.FlowState = false;

			if (isEditor)
			{
				atmoRes.Amount = 0.0;
				wasteRes.Amount = 0.0;

				wasteRes.Capacity = crewCount * Settings.PressureSuitVolume;
				wasteRes.FlowState = true;
				pressureState = PressureState.Depressurized;
			}
			else
			{
				if (VesselData.EnvInBreathableAtmosphere && IsDeployed)
				{
					wasteRes.Capacity = HabitatLib.M3ToL(definition.volume);
					wasteRes.FlowState = false;
					pressureState = PressureState.Breatheable; // don't go to breathable start to avoid resetting the portraits, we already have locked flowstate anyway
				}
				else
				{
					wasteRes.Capacity = crewCount * Settings.PressureSuitVolume;
					wasteRes.FlowState = true;
					pressureState = PressureState.Depressurized;
				}
			}
		}

		public void ForceBreathableDepressurizationEvt()
		{
			atmoRes.FlowState = false;
			wasteRes.FlowState = false;
			atmoRes.Amount = 0.0;
			wasteRes.Amount = 0.0;
			wasteRes.Capacity = 0.0;
			pressureState = PressureState.Depressurized;
		}

		public void UpdateHelmets()
		{
			ShowHelmets(RequireHelmet);
		}


		// make the kerbals put or remove their helmets
		// this works in conjunction with the SpawnCrew prefix patch that check if the part is pressurized or not on spawning the IVA.
		private void ShowHelmets(bool active)
		{
			if (!isEditor && crewCount > 0 && VesselData is VesselData vdVessel && vdVessel.Vessel.isActiveVessel && loadedModule.part.internalModel != null)
			{
				foreach (InternalSeat seat in loadedModule.part.internalModel.seats)
				{
					if (seat.kerbalRef == null)
						continue;

					seat.kerbalRef.ShowHelmet(active);
				}
			}
		}

		#endregion

		bool IRecipeExecutedCallback.IsCallbackRegistered { get; set; }
		public List<Recipe> ExecutedRecipes { get; private set; } = new List<Recipe>(3);

		private void OnDepressurizationRecipeExecuted(double elapsedSec)
		{
			if (definition.isDeployable && definition.deployWithPressure && animState == AnimState.Retracting)
			{
				float deployLevel = Math.Min(1f, (float)(atmoRes.Amount / (atmoRes.Capacity * Settings.PressureThreshold)));
				if (partData.IsLoaded)
					loadedModule.deployAnimator.Still(deployLevel);

				animTimer = modulePrefab.deployAnimator.AnimDuration * deployLevel;
			}
		}

		private void OnDeployRecipeExecuted(double elapsedSec)
		{
			if (animState == AnimState.Deploying || animState == AnimState.Retracting)
			{
				if (partData.IsLoaded)
				{
					loadedModule.deployAnimator.ChangeSpeed((float)deployRecipe.ExecutedFactor);
				}
				else
				{
					animTimer -= (float)(elapsedSec * deployRecipe.ExecutedFactor);
				}
			}
		}

		private void OnCentrifugeRecipeExecuted(double elapsedSec)
		{
			if (partData.IsLoaded)
			{
				switch (animState)
				{
					case AnimState.Accelerating:
					case AnimState.Decelerating:
					case AnimState.Rotating:
					case AnimState.RotatingNotEnoughEC:
						bool loosingSpeed = animState == AnimState.RotatingNotEnoughEC;
						bool isRotationEnabled = IsRotationEnabled;
						float executedFactor = (float)centrifugeRecipe.ExecutedFactor;

						loadedModule.rotateAnimator.Update(isRotationEnabled, loosingSpeed, (float)elapsedSec, executedFactor);
						loadedModule.counterweightAnimator.Update(isRotationEnabled, loosingSpeed, (float)elapsedSec, executedFactor);

						if (animState == AnimState.Rotating && executedFactor < 1f)
						{
							animState = AnimState.RotatingNotEnoughEC;
						}
						else if (loosingSpeed)
						{
							if (executedFactor == 1f)
								animState = AnimState.Accelerating;
							else if (loadedModule.rotateAnimator.IsStopped)
								animState = AnimState.Stuck;
						}

						break;
				}
			}
			else
			{
				switch (animState)
				{
					case AnimState.Accelerating:
						animTimer -= (float)(elapsedSec * centrifugeRecipe.ExecutedFactor);

						if (animTimer <= 0.0)
						{
							centrifugeECInput.NominalRate = definition.rotateECRate;
							animState = AnimState.Rotating;
						}
						break;
					case AnimState.Rotating:
						if (centrifugeRecipe.ExecutedFactor < 1.0)
						{
							float speedLost = (float)(elapsedSec * centrifugeRecipe.ExecutedFactor);
							float timeToAccelerate = definition.rotateSpinRate / definition.rotateAccelerationRate;
							animTimer = timeToAccelerate * Math.Min(speedLost / definition.rotateSpinRate, 1f);
							animState = AnimState.Accelerating;
						}
						break;
				}
			}
		}
	}
}
