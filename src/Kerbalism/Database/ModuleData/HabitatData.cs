using System;
using System.Collections.Generic;
using UnityEngine;
using static KERBALISM.HabitatData;

namespace KERBALISM
{
	/// <summary>
	/// loaded/unloaded/editor state independant persisted data and logic used by the ModuleKsmHabitat module.
	/// </summary>
	public class HabitatData : ModuleData<ModuleKsmHabitat, HabitatData>, IRadiationReceiver
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

		/// <summary> habitat volume in m3 </summary>
		public double baseVolume = 0.0;

		/// <summary> habitat surface in m2 </summary>
		public double baseSurface = 0.0;

		/// <summary> bitmask of comforts provided by the habitat</summary>
		public int baseComfortsMask = 0;

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

		private PartResourceWrapper atmoRes;
		private PartResourceWrapper wasteRes;
		private PartResourceWrapper shieldRes;
		private VesselKSPResource ecResInfo;
		private VesselKSPResource atmoResInfo;
		private VesselKSPResource wasteResInfo;
		private VesselKSPResource breathableResInfo;
		private bool isEditor;

		#endregion

		#region PROPERTIES

		public PartResourceWrapper AtmoRes => atmoRes;
		public PartResourceWrapper WasteRes => wasteRes;
		public PartResourceWrapper ShieldRes => shieldRes;
		public VesselKSPResource EcResInfo => ecResInfo;

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

		public override void OnFirstInstantiate()
		{
			if (loadedModule != null)
				crewCount = Lib.CrewCount(loadedModule.part);
			else
				crewCount = Lib.CrewCount(partData.ProtoPart);

			baseVolume = modulePrefab.volume;
			baseSurface = modulePrefab.surface;
			baseComfortsMask = modulePrefab.baseComfortsMask;
			animState = modulePrefab.isDeployable ? AnimState.Retracted : AnimState.Deployed;
			isEnabled = !modulePrefab.isDeployable;

			// add atmo, waste, reclaim and shielding resources to the part
			double volumeLiters = HabitatLib.M3ToL(baseVolume);
			partData.resources.AddResource(Settings.HabitatAtmoResource, volumeLiters, volumeLiters);
			partData.resources.AddResource(Settings.HabitatWasteResource, 0.0, volumeLiters);

			if (modulePrefab.hasShielding)
				partData.resources.AddResource(modulePrefab.shieldingResource, 0.0, baseSurface * modulePrefab.maxShieldingFactor);

			if (modulePrefab.canPressurize && modulePrefab.reclaimStorageFactor > 0.0)
			{
				double capacity = volumeLiters * modulePrefab.reclaimStorageFactor;
				double amount = Math.Max(0.0, capacity - (volumeLiters * modulePrefab.reclaimFactor));
				partData.resources.AddResource(modulePrefab.reclaimResource, amount, capacity);
			}

			if (Lib.IsEditor)
			{
				if (!modulePrefab.canPressurize)
					pressureState = PressureState.AlwaysDepressurized;
				else if (modulePrefab.isDeployable && !IsDeployed)
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
				pressureState = modulePrefab.canPressurize ? PressureState.Depressurized : PressureState.AlwaysDepressurized;
			}
		}

		public override void OnStart()
		{
			isEditor = Lib.IsEditor;

			atmoRes = partData.resources.Find(p => p.ResName == Settings.HabitatAtmoResource);
			wasteRes = partData.resources.Find(p => p.ResName == Settings.HabitatWasteResource);

			if (modulePrefab.hasShielding)
			{
				shieldRes = partData.resources.Find(p => p.ResName == modulePrefab.shieldingResource);
			}

			ecResInfo = VesselData.ResHandler.ElectricCharge;
			atmoResInfo = (VesselKSPResource)VesselData.ResHandler.GetResource(Settings.HabitatAtmoResource);
			wasteResInfo = (VesselKSPResource)VesselData.ResHandler.GetResource(Settings.HabitatWasteResource);

			if (Settings.HabitatBreathableResourceRate > 0.0)
				breathableResInfo = (VesselKSPResource)VesselData.ResHandler.GetResource(Settings.HabitatBreathableResource);

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

		}

		public override void OnLoad(ConfigNode node)
		{
			baseVolume = Lib.ConfigValue(node, "baseVolume", baseVolume);
			baseSurface = Lib.ConfigValue(node, "baseSurface", baseSurface);
			baseComfortsMask = Lib.ConfigValue(node, "baseComfortsMask", baseComfortsMask);
			isEnabled = Lib.ConfigValue(node, "habitatEnabled", isEnabled);
			pressureState = Lib.ConfigEnum(node, "pressureState", pressureState);
			animState = Lib.ConfigEnum(node, "animState", animState);
			crewCount = Lib.ConfigValue(node, "crewCount", crewCount);
			atmoAmount = Lib.ConfigValue(node, "atmoAmount", atmoAmount);
			wasteLevel = Lib.ConfigValue(node, "wasteLevel", wasteLevel);
			shieldingAmount = Lib.ConfigValue(node, "shieldingAmount", shieldingAmount);
			animTimer = Lib.ConfigValue(node, "animTimer", animTimer);
		}

		public override void OnSave(ConfigNode node)
		{
			node.AddValue("baseVolume", baseVolume);
			node.AddValue("baseSurface", baseSurface);
			node.AddValue("baseComfortsMask", baseComfortsMask);
			node.AddValue("habitatEnabled", isEnabled);
			node.AddValue("pressureState", pressureState.ToString());
			node.AddValue("animState", animState.ToString());
			node.AddValue("crewCount", crewCount);
			node.AddValue("atmoAmount", atmoAmount);
			node.AddValue("wasteLevel", wasteLevel);
			node.AddValue("shieldingAmount", shieldingAmount);
			node.AddValue("animTimer", animTimer);
		}

		#endregion

		#region UPDATE

		public override void OnFixedUpdate(double elapsedSec)
		{
			isEditor = Lib.IsEditor;
			AnimationsUpdate(elapsedSec);
			PressureUpdate(elapsedSec);
			shieldingAmount = modulePrefab.hasShielding ? shieldRes.Amount : 0.0;
		}

		private void AnimationsUpdate(double elapsed_s)
		{
			// animations state machine
			if (partData.IsLoaded)
			{
				switch (animState)
				{
					case AnimState.Deploying:
						if (loadedModule.deployWithPressure)
							break;

						if (loadedModule.deployAnimator.Playing)
						{
							if (!isEditor && loadedModule.deployECRate > 0.0)
							{
								ecResInfo.Consume(loadedModule.deployECRate * elapsed_s, ResourceBroker.Deployment);
								loadedModule.deployAnimator.ChangeSpeed((float)ecResInfo.AvailabilityFactor);
							}
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
						if (loadedModule.deployWithPressure)
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
								if (!isEditor && loadedModule.deployECRate > 0.0)
								{
									ecResInfo.Consume(loadedModule.deployECRate * elapsed_s, ResourceBroker.Deployment);
									loadedModule.deployAnimator.ChangeSpeed((float)ecResInfo.AvailabilityFactor);
								}
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
							if (!isEditor && loadedModule.rotateECRate > 0.0)
								ecResInfo.Consume(loadedModule.rotateECRate * elapsed_s, ResourceBroker.GravityRing);

							animState = AnimState.Rotating;
						}
						else if (loadedModule.rotateAnimator.IsStopped)
						{
							animState = AnimState.Stuck;
						}
						else
						{
							if (!isEditor && loadedModule.accelerateECRate > 0.0)
								ecResInfo.Consume(loadedModule.accelerateECRate * elapsed_s, ResourceBroker.GravityRing);
						}
						break;
					case AnimState.Decelerating:

						if (loadedModule.rotateAnimator.IsStopped)
						{
							animState = AnimState.Deployed;
						}

						break;
					case AnimState.Rotating:
						if (!isEditor && loadedModule.rotateECRate > 0.0)
						{
							ecResInfo.Consume(loadedModule.rotateECRate * elapsed_s, ResourceBroker.GravityRing);

							if (ecResInfo.AvailabilityFactor < 1.0)
								animState = AnimState.RotatingNotEnoughEC;
						}
						break;
					case AnimState.RotatingNotEnoughEC:
						if (!isEditor && loadedModule.rotateECRate > 0.0)
						{
							ecResInfo.Consume(loadedModule.rotateECRate * elapsed_s, ResourceBroker.GravityRing);

							if (ecResInfo.AvailabilityFactor == 1.0)
							{
								animState = AnimState.Accelerating;
							}
							else if (loadedModule.rotateAnimator.IsStopped)
							{
								animState = AnimState.Stuck;
							}
						}
						break;
				}
			}
			else
			{
				switch (animState)
				{
					case AnimState.Deploying:

						if (!modulePrefab.deployWithPressure)
						{
							double deploySpeedFactor = 1.0;
							if (loadedModule.deployECRate > 0.0)
							{
								double timeSpent = Math.Min(elapsed_s, animTimer);
								ecResInfo.Consume(modulePrefab.deployECRate * timeSpent, ResourceBroker.Deployment);
								deploySpeedFactor = ecResInfo.AvailabilityFactor;
							}

							animTimer -= (float)(elapsed_s * deploySpeedFactor);
						}

						if (animTimer <= 0f)
							animState = AnimState.Deployed;

						break;

					case AnimState.Retracting:

						if (modulePrefab.deployWithPressure)
						{
							if (animTimer <= 0f)
							{
								animTimer = 0f;
								animState = AnimState.Retracted;
							}
						}
						else
						{
							double retractSpeedFactor = 1.0;
							if (modulePrefab.deployECRate > 0.0)
							{
								double timeSpent = Math.Min(elapsed_s, animTimer);
								ecResInfo.Consume(modulePrefab.deployECRate * timeSpent, ResourceBroker.Deployment);
								retractSpeedFactor = ecResInfo.AvailabilityFactor;
							}

							animTimer -= (float)(elapsed_s * retractSpeedFactor);

							if (animTimer <= 0f)
								animState = AnimState.Retracted;
						}
						break;
					case AnimState.Accelerating:

						double accelSpeedFactor = 1.0;
						if (modulePrefab.accelerateECRate > 0.0)
						{
							double timeSpent = Math.Min(elapsed_s, animTimer);
							ecResInfo.Consume((modulePrefab.accelerateECRate + modulePrefab.rotateECRate) * timeSpent, ResourceBroker.GravityRing);
							accelSpeedFactor = ecResInfo.AvailabilityFactor;
						}

						//accelSpeedFactor -= Transformator.spinLosses / modulePrefab.rotateAccelerationRate;

						animTimer -= (float)(elapsed_s * accelSpeedFactor);

						if (animTimer > modulePrefab.rotateAnimator.TimeNeededToStartOrStop)
							animState = AnimState.Deployed;
						else if (animTimer <= 0.0)
							animState = AnimState.Rotating;

						break;
					case AnimState.Decelerating:

						animTimer -= (float)elapsed_s;

						if (animTimer <= 0.0)
							animState = AnimState.Deployed;

						break;
					case AnimState.Rotating:

						if (modulePrefab.rotateECRate > 0.0)
							ecResInfo.Consume(modulePrefab.rotateECRate * elapsed_s, ResourceBroker.GravityRing);

						if (ecResInfo.AvailabilityFactor < 1.0)
						{
							//double speedLost = Transformator.spinLosses * elapsed_s * ecResInfo.AvailabilityFactor;
							double speedLost = elapsed_s * ecResInfo.AvailabilityFactor;
							animTimer = modulePrefab.rotateAnimator.TimeNeededToStartOrStop * Math.Min((float)speedLost / modulePrefab.rotateSpinRate, 1f);
							animState = AnimState.Accelerating;
						}
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
						if (modulePrefab.canPressurize)
							PressureDroppedEvt();
						else
							AlwaysDepressurizedStartEvt();
						break;
					}

					// magic scrubbing and oxygen supply
					if (breathableResInfo != null && crewCount > 0)
					{
						double rate = crewCount * Settings.HabitatBreathableResourceRate * elapsed_s;
						breathableResInfo.Produce(rate, ResourceBroker.Environment);
						// note : we abuse the isCritical system here to make sure this consumption
						// is prioritized over the consume calls from scrubbers.
						wasteResInfo.Consume(rate, ResourceBroker.Environment, true);
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
						atmoResInfo.equalizeMode = VesselKSPResource.EqualizeMode.Disabled;

					if (modulePrefab.deployWithPressure && animState == AnimState.Deploying)
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
					if (atmoRes.Amount <= 0.0)
					{
						DepressurizingEndEvt();
						break;
					}
					// if external pressure is less than the hab pressure, stop depressurization and go to the breathable state
					else if (VesselData.EnvInOxygenAtmosphere && atmoRes.Amount / atmoRes.Capacity < VesselData.EnvStaticPressure && IsDeployed)
					{
						DepressurizingEndEvt();
						break;
					}
					// pressure is going below the survivable threshold : time for kerbals to put their helmets
					else if (pressureState == PressureState.DepressurizingAboveThreshold && atmoRes.Amount / atmoRes.Capacity < Settings.PressureThreshold)
					{
						DepressurizingPassThresholdEvt();
					}

					// remove atmosphere and convert it into the reclaimed resource :
					// - consume EC if there we are reclaiming, or if we are deflating an inflatable
					// - if EC is consumed, scale the output with EC availability

					bool isReclaiming = modulePrefab.reclaimFactor > 0.0 && atmoRes.Level >= 1.0 - modulePrefab.reclaimFactor;
					bool isInflatableRetracting = modulePrefab.isDeployable && modulePrefab.deployWithPressure && animState == AnimState.Retracting;

					double ecFactor;
					if (modulePrefab.depressurizeECRate > 0.0 && (isReclaiming || isInflatableRetracting))
					{
						ecResInfo.Consume(modulePrefab.depressurizeECRate * elapsed_s, ResourceBroker.Depressurization);
						ecFactor = ecResInfo.AvailabilityFactor;
					}
					else
					{
						ecFactor = 1.0;
					}

					double newAtmoAmount = atmoRes.Amount - (modulePrefab.depressurizationSpeed * elapsed_s);
					newAtmoAmount = Math.Max(newAtmoAmount, 0.0);

					// we only vent CO2 when the kerbals aren't yet in their helmets
					if (pressureState == PressureState.DepressurizingAboveThreshold)
					{
						wasteRes.Amount *= atmoRes.Amount > 0.0 ? newAtmoAmount / atmoRes.Amount : 1.0;
						wasteRes.Amount = Lib.Clamp(wasteRes.Amount, 0.0, wasteRes.Capacity);
					}

					if (isReclaiming)
					{
						VesselData.ResHandler.Produce(modulePrefab.reclaimResource, (atmoRes.Amount - newAtmoAmount) * ecFactor, ResourceBroker.Depressurization);
					}

					atmoRes.Amount = newAtmoAmount;

					// handle inflatable retraction animation / animation timer
					if (isInflatableRetracting)
					{
						float deployLevel = Math.Min(1f, (float)(atmoRes.Amount / (atmoRes.Capacity * Settings.PressureThreshold)));
						if (partData.IsLoaded)
							loadedModule.deployAnimator.Still(deployLevel);

						animTimer = modulePrefab.deployAnimator.AnimDuration * deployLevel;
					}

					break;
			}

			// synchronize resource amounts to the persisted data
			atmoAmount = HabitatLib.LToM3(atmoRes.Amount);
			wasteLevel = wasteRes.Capacity > 0.0 ? wasteRes.Amount / wasteRes.Capacity : 0.0;

			// set equalizaton mode if it hasn't been explictely disabled in the breathable / depressurizing states
			if (!isEditor)
			{
				if (atmoResInfo.equalizeMode == VesselKSPResource.EqualizeMode.NotSet)
					atmoResInfo.equalizeMode = VesselKSPResource.EqualizeMode.Enabled;

				if (wasteResInfo.equalizeMode == VesselKSPResource.EqualizeMode.NotSet)
					wasteResInfo.equalizeMode = VesselKSPResource.EqualizeMode.Enabled;
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

			wasteRes.Capacity = HabitatLib.M3ToL(modulePrefab.volume);
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
				atmoResInfo.equalizeMode = VesselKSPResource.EqualizeMode.Disabled;
				pressureState = PressureState.Pressurizing;
			}
			else
			{
				PressurizingEndEvt();
			}
		}

		public void PressurizingEndEvt()
		{
			wasteRes.Capacity = HabitatLib.M3ToL(modulePrefab.volume);
			wasteRes.FlowState = true;
			atmoRes.FlowState = true;

			if (!isEditor)
			{
				ShowHelmets(false);

				if (modulePrefab.deployWithPressure && animState == AnimState.Deploying)
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
				atmoRes.Amount = HabitatLib.M3ToL(loadedModule.volume);

				if (loadedModule.isDeployable && loadedModule.deployWithPressure && !IsDeployed)
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
					wasteRes.Capacity = HabitatLib.M3ToL(modulePrefab.volume);
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
	}
}
