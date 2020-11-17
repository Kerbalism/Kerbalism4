using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KERBALISM
{
	/*
OVERVIEW : 
- Storm / local radiation raycasting :
	- RaycastAll between emitter and receiver, save "from" hits
	- RaycastAll between receiver and emitter, save "to" hits
	- foreach hit :
		- build a list of hitted parts and affect from and to hits
		- the good "from" hit is the one closest from the emitter
		- the good "to" hit is the one closest from the receiver
		- distance from / to is the thickness
		- determine wall thickness the ray has gone through by using the hits normals

- Occluder stats :
	- At minimum, a part has 2 occluder "materials" : walls and core
	- The "wall" occluder has a thickness enclosing the part.
		- Half the part mass is considered to be walls of aluminium density, giving a "wall volume"
		- The wall thickness is the "wall volume" divided by the part surface
		- HVL values are derived from aluminium properties
	- A "core" occluder that is derived from the part volume and half the part mass
		- A "density factor" is derived from the difference between the part volume and the volume of half the part mass at aluminum density
		- HVL values are derived from aluminium properties, scaled down by the "density factor"
	- By default, resources are additional "core" occluders using the same formula, but with the density and HVL parameters from the resource instead of aluminium
	- A RESOURCE_HVL node in the profile can be used to define the resource occlusion stats (wall/core, HVL values)
		- Resources that don't have a definition are considered as "core", with HVL values derived from the resource density.

- Radiation is computed as follow :
	- "Ambiant" radiation (bodies, belts...) :
		- Is blocked by the part "wall" occluder materials, according to the material "low" energy HVL value
		- Is blocked by coil array fields enclosing the part
	- "Storm" radiation (sun) :
		- Is blocked by the part "wall" occluder materials
		- Is blocked by all occluder materials from all parts in between
		- According to the material "high" energy HVL value
		- Parts in between produce secondary radiation (bremsstrahlung)
		- Bremsstrahlung is also blocked, but using the "low" energy HVL value
	- "Local" radiation (emitters)
		- Is blocked by the part "wall" occluder materials, and by all occluder materials from all parts in between
		- Is blocked by all occluder materials from all parts in between
		- According to the material "high" or "low" energy HVL value depending on the emitter

- TODO :
	- IRadiationEmitter is currently only implemented in ModuleKsmRadiationEmitter. An implementation in ModuleKsmProcessController would make sense.
	- Also, ModuleKsmRadiationEmitter could benefit from a config-defined reflection based system allowing to watch another module state to
	  determine if the emitter should be enabled, and eventually to scale the radiation level.
	- Currently there is a set of heuristics used to determine if a part can or can't be an occluder, to exclude small parts.
	  An option to override the automatic choice with per-part configs would be nice
	- Occlusion computations accuracy heavily rely on a static library of part volume/surface stats currently computed at prefab compilation.
	  This is problematic for deployable / procedural parts. Ideally we should have volume/surface stats stored in PartData, with hooks /
	  events watching for "shape changes", either triggering a volume/surface reevaluation (might need to investigate how to do that in a separate thread,
	  this too heavy to be done in real time) or acquiring volume/surface from external sources (procedural parts ?)
	- Planner / flight UI info for radiation need a bit of love. Some info (tooltip) about the radiation level per source would be nice.
	  Also an ETA on radiation poisonning at current level.
	- The coil array system is a bit dumb and unrealistic. Ideally the field should be cylindrical and be an occluder for storm/emitters, 
	  only providing "direct" protection for "ambiant" radiation. In any case, the whole system is very WIP and in dire need of debugging and balancing.
	- Ideally, planetary (and sun) radiation should be directional, like storms and emitters. The only "ambiant" radiation should happen in belts (and maybe
	  when close from a body surface)
	- An "auto-shelter" feature would be nice : when external radiation exceed a player defined level, a set of player defined habitats are disabled, 
	  and re-enabled when the storm is over. Require implementing unloaded crew transfer (relatively easy).
	*/

	public partial class PartRadiationData
	{
		public const string NODENAME_RADIATION = "RADIATION";
		public const string NODENAME_EMITTERS = "EMITTERS";

		#region TYPES

		private struct CoilArrayShielding
		{
			private RadiationCoilData.ArrayEffectData array;
			private double protectionFactor;

			public CoilArrayShielding(RadiationCoilData.ArrayEffectData array, double protectionFactor)
			{
				this.array = array;
				this.protectionFactor = protectionFactor;
			}

			public double RadiationRemoved => array.RadiationRemoved * protectionFactor;
		}
		#endregion

		#region FIELDS

		// total radiation dose received since launch
		public double accumulatedRadiation;

		private bool raycastDone = true;

		private double elapsedSecSinceLastUpdate;

		// all active radiation shields whose protecting field include this part.
		private List<CoilArrayShielding> radiationCoilArrays;

		// occluding stats for the part structural mass
		private PartStructuralOcclusion structuralOcclusion;

		// occluding stats for the part resources
		private List<ResourceOcclusion> resourcesOcclusion = new List<ResourceOcclusion>();

		private PartData partData;

		private bool? isReceiver;

		private SunRaycastTask sunRaycastTask;

		private List<EmitterRaycastTask> emitterRaycastTasks;

		#endregion

		#region PROPERTIES

		// rad/s received by that part
		public double RadiationRate { get; private set; }

		public bool IsReceiver
		{
			get
			{
				if (isReceiver == null)
				{
					foreach (ModuleData md in partData.modules)
					{
						if (md is IRadiationReceiver)
						{
							if (radiationCoilArrays == null)
								radiationCoilArrays = new List<CoilArrayShielding>();

							if (sunRaycastTask == null)
								sunRaycastTask = new SunRaycastTask(this);

							if (emitterRaycastTasks == null)
								emitterRaycastTasks = new List<EmitterRaycastTask>();

							isReceiver = true;
							break;
						}
					}

					if (isReceiver == null)
					{
						isReceiver = false;
					}
				}

				return (bool)isReceiver;
			}
		}

		public bool IsOccluder { get; private set; }

		#endregion

		#region LIFECYLE

		public PartRadiationData(PartData partData)
		{
			this.partData = partData;
			IsOccluder = partData.volumeAndSurface != null;
			if (IsOccluder)
			{
				structuralOcclusion = new PartStructuralOcclusion();
			}

		}

		public static void Load(PartData partData, ConfigNode partDataNode)
		{
			ConfigNode radNode = partDataNode.GetNode(NODENAME_RADIATION);
			if (radNode == null)
				return;

			partData.radiationData.RadiationRate = Lib.ConfigValue(radNode, "radRate", 0.0);
			partData.radiationData.accumulatedRadiation = Lib.ConfigValue(radNode, "radAcc", 0.0);

			if (radNode.HasValue("sunFactor"))
			{
				if (partData.radiationData.sunRaycastTask == null)
				{
					partData.radiationData.sunRaycastTask = new SunRaycastTask(partData.radiationData);
				}
				partData.radiationData.sunRaycastTask.sunRadiationFactor = Lib.ConfigValue(radNode, "sunFactor", 1.0);
			}
			
			ConfigNode emittersNode = radNode.GetNode(NODENAME_EMITTERS);
			if (emittersNode != null)
			{
				if (partData.radiationData.emitterRaycastTasks == null)
				{
					partData.radiationData.emitterRaycastTasks = new List<EmitterRaycastTask>();
				}
				
				foreach (ConfigNode.Value value in emittersNode.values)
				{
					EmitterRaycastTask emitter = new EmitterRaycastTask(partData.radiationData, value);
					partData.radiationData.emitterRaycastTasks.Add(emitter);
				}
			}
		}

		public static bool Save(PartData partData, ConfigNode partDataNode)
		{
			if (!partData.radiationData.IsReceiver)
				return false;

			ConfigNode radiationNode = partDataNode.AddNode(NODENAME_RADIATION);
			radiationNode.AddValue("radRate", partData.radiationData.RadiationRate);
			radiationNode.AddValue("radAcc", partData.radiationData.accumulatedRadiation);

			if (partData.radiationData.sunRaycastTask != null)
			{
				radiationNode.AddValue("sunFactor", partData.radiationData.sunRaycastTask.sunRadiationFactor);
			}

			if (partData.radiationData.emitterRaycastTasks != null && partData.radiationData.emitterRaycastTasks.Count > 0.0)
			{
				ConfigNode emittersNode = new ConfigNode(NODENAME_EMITTERS);
				foreach (var emitterRaycastTask in partData.radiationData.emitterRaycastTasks)
				{
					emitterRaycastTask.SaveToNode(emittersNode);
				}
				radiationNode.AddNode(emittersNode);
			}

			return true;
		}

		#endregion

		#region EVALUATION

		public void AddElapsedTime(double elapsedSec) => elapsedSecSinceLastUpdate += elapsedSec;

		public void EnqueueRaycastTasks(Queue<RaycastTask> raycastTasks)
		{
			if (raycastDone)
			{
				raycastDone = false;

				raycastTasks.Enqueue(sunRaycastTask);

				foreach (EmitterRaycastTask emitterRaycastTask in emitterRaycastTasks)
				{
					raycastTasks.Enqueue(emitterRaycastTask);
				}
			}
		}

		// debug receiver info
		private bool debug = false;
		private double radiationRate;
		private double stormRadiationFactor;
		private double stormRadiation;
		private double emittersRadiation;

		// debug occluder info
		private string lastRaycast;
		private double rayPenetration;
		private double blockedRad;
		private double bremsstrahlung;
		private double crossSectionFactor;

		public void Update()
		{
			// TODO : should we consider emissions and occlusion from other nearby (~ 250m max) loaded vessels ?
			// It feels relevant for EVA and when all considered vessels are landed...
			// But if evaluating in-flight EVA there is a risk of saving radiation values that won't stay true while unloaded...

			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.PartRadiationData.Update");

			if (IsOccluder)
			{
				// TODO : remove that in favor of a static dictionary of per part stats
				GetOccluderStats();
			}

			if (partData.vesselData.LoadedOrEditor)
			{
				if (IsReceiver)
				{
					UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.PartRadiationData.Update.Arrays");
					radiationCoilArrays.Clear();
					foreach (RadiationCoilData array in partData.vesselData.PartCache.RadiationArrays)
					{
						double protection = array.loadedModule.GetPartProtectionFactor(partData.LoadedPart);
						if (protection > 0.0)
							radiationCoilArrays.Add(new CoilArrayShielding(array.effectData, protection));

					}
					UnityEngine.Profiling.Profiler.EndSample();

				}

				if (IsOccluder)
				{
					UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.PartRadiationData.Update.Occluders");
					UpdateOcclusionStats();
					UnityEngine.Profiling.Profiler.EndSample();
				}
			}

			if (IsReceiver)
			{
				stormRadiation = 0.0;
				emittersRadiation = 0.0;

				// add "ambiant" radiation (background, belts, bodies...)
				RadiationRate = partData.vesselData.EnvRadiation * OcclusionFactor(false, true, false);

				// synchronize emitters references and add their radiation
				int vesselEmittersCount = partData.vesselData.PartCache.RadiationEmitters.Count;
				int tasksCount = emitterRaycastTasks.Count;

				if (tasksCount > vesselEmittersCount)
				{
					emitterRaycastTasks.RemoveRange(vesselEmittersCount - 1, tasksCount - vesselEmittersCount);
				}

				for (int i = 0; i < vesselEmittersCount; i++)
				{
					if (i + 1 > tasksCount)
					{
						emitterRaycastTasks.Add(new EmitterRaycastTask(this, partData.vesselData.PartCache.RadiationEmitters[i]));
					}
					else
					{
						emitterRaycastTasks[i].CheckEmitterHasChanged(partData.vesselData.PartCache.RadiationEmitters[i]);
					}

					RadiationRate += emitterRaycastTasks[i].Radiation;
					emittersRadiation += emitterRaycastTasks[i].Radiation;
				}

				// add storm radiation, if there is a storm
				stormRadiationFactor = sunRaycastTask.sunRadiationFactor;
				if (partData.vesselData.EnvStorm)
				{
					RadiationRate += partData.vesselData.EnvStormRadiation * sunRaycastTask.sunRadiationFactor;
					stormRadiation = partData.vesselData.EnvStormRadiation * sunRaycastTask.sunRadiationFactor;
				}

				// substract magnetic shields effects
				foreach (CoilArrayShielding arrayData in radiationCoilArrays)
				{
					RadiationRate -= arrayData.RadiationRemoved;
				}

				// clamp to nominal
				RadiationRate = Math.Max(RadiationRate, Radiation.Nominal);
				radiationRate = RadiationRate;

				// accumulate total radiation received by this part
				accumulatedRadiation += RadiationRate * elapsedSecSinceLastUpdate;
			}

			elapsedSecSinceLastUpdate = 0.0;

			UnityEngine.Profiling.Profiler.EndSample();

			if (!debug && partData.vesselData.LoadedOrEditor)
			{
				debug = true;

				if (partData.vesselData.LoadedOrEditor)
				{
					if (IsReceiver)
					{
						partData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), GetType().GetField(nameof(radiationRate), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance), this));
						partData.LoadedPart.Fields[nameof(radiationRate)].guiName = "Radiation";
						partData.LoadedPart.Fields[nameof(radiationRate)].guiFormat = "F10";
						partData.LoadedPart.Fields[nameof(radiationRate)].guiUnits = " rad/s";

						partData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), GetType().GetField(nameof(emittersRadiation), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance), this));
						partData.LoadedPart.Fields[nameof(emittersRadiation)].guiName = "Emitters";
						partData.LoadedPart.Fields[nameof(emittersRadiation)].guiFormat = "F10";
						partData.LoadedPart.Fields[nameof(emittersRadiation)].guiUnits = " rad/s";

						partData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), GetType().GetField(nameof(stormRadiation), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance), this));
						partData.LoadedPart.Fields[nameof(stormRadiation)].guiName = "Storm";
						partData.LoadedPart.Fields[nameof(stormRadiation)].guiFormat = "F10";
						partData.LoadedPart.Fields[nameof(stormRadiation)].guiUnits = " rad/s";

						partData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), GetType().GetField(nameof(stormRadiationFactor), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance), this));
						partData.LoadedPart.Fields[nameof(stormRadiationFactor)].guiName = "Storm rad factor";
						partData.LoadedPart.Fields[nameof(stormRadiationFactor)].guiFormat = "P6";
					}

					if (IsOccluder)
					{
						partData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), GetType().GetField(nameof(lastRaycast), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance), this));

						partData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), GetType().GetField(nameof(rayPenetration), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance), this));
						partData.LoadedPart.Fields[nameof(rayPenetration)].guiName = "rayPenetration";
						partData.LoadedPart.Fields[nameof(rayPenetration)].guiFormat = "F3";
						partData.LoadedPart.Fields[nameof(rayPenetration)].guiUnits = "m";

						partData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), GetType().GetField(nameof(blockedRad), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance), this));
						partData.LoadedPart.Fields[nameof(blockedRad)].guiName = "blockedRad";
						partData.LoadedPart.Fields[nameof(blockedRad)].guiFormat = "P6";

						partData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), GetType().GetField(nameof(bremsstrahlung), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance), this));
						partData.LoadedPart.Fields[nameof(bremsstrahlung)].guiName = "bremsstrahlung";
						partData.LoadedPart.Fields[nameof(bremsstrahlung)].guiFormat = "P6";

						partData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), GetType().GetField(nameof(crossSectionFactor), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance), this));
						partData.LoadedPart.Fields[nameof(crossSectionFactor)].guiName = "crossSectionFactor";
						partData.LoadedPart.Fields[nameof(crossSectionFactor)].guiFormat = "P6";
					}


				}
			}
		}

		// TODO : use the actual part mass, not the prefab mass
		// Since the part can be unloaded, this require either storing the protopart reference in PartData, or storing the mass independently (a bit silly)
		public void GetOccluderStats()
		{
			structuralOcclusion.Update(partData.PartPrefab.mass, partData.volumeAndSurface.surface, partData.volumeAndSurface.volume);
		}

		private void UpdateOcclusionStats()
		{
			int listCapacity = resourcesOcclusion.Count;
			int occluderIndex = -1;

			for (int i = 0; i < partData.LoadedPart.Resources.Count; i++)
			{
				PartResource res = partData.LoadedPart.Resources[i];

				if (res.info.density == 0.0)
					continue;

				occluderIndex++;
				if (occluderIndex >= listCapacity)
				{
					resourcesOcclusion.Add(new ResourceOcclusion(res.info));
					listCapacity++;
				}

				resourcesOcclusion[occluderIndex].UpdateOcclusion(res, partData.volumeAndSurface.surface, partData.volumeAndSurface.volume);
			}

			while (listCapacity > occluderIndex + 1)
			{
				resourcesOcclusion.RemoveAt(listCapacity - 1);
				listCapacity--;
			}
		}

		private double OcclusionFactor(bool highPowerRad, bool wallOnly = false, bool crossing = true)
		{
			if (!IsOccluder)
			{
				return 0.0;
			}

			double rad = 1.0;

			rad *= PartWallOcclusion.RadiationFactor(highPowerRad, crossing);

			if (!wallOnly)
			{
				rad *= structuralOcclusion.RadiationFactor(hitPenetration, highPowerRad);
			}
			
			foreach (ResourceOcclusion occlusion in resourcesOcclusion)
			{
				if (occlusion.IsWallOccluder)
				{
					rad *= occlusion.WallRadiationFactor(highPowerRad, crossing);
				}
				else if (!wallOnly)
				{
					rad *= occlusion.VolumeRadiationFactor(hitPenetration, highPowerRad);
				}
			}
			return 1.0 - rad;
		}

		#endregion
	}
}
