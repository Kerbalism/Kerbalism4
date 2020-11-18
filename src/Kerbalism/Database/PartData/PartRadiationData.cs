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

		

		private bool raycastDone = true;

		// total radiation dose received since launch
		public double accumulatedRadiation;

		private double elapsedSecSinceLastUpdate;

		// all active radiation shields whose protecting field include this part.
		private List<CoilArrayShielding> radiationCoilArrays;

		// occluding stats for the part structural mass
		private PartStructuralOcclusion structuralOcclusion;

		// occluding stats for the part resources
		private List<ResourceOcclusion> resourcesOcclusion;

		private SunRaycastTask sunRaycastTask;

		private List<EmitterRaycastTask> emitterRaycastTasks;

		#endregion

		#region PROPERTIES

		// rad/s received by that part
		public PartData PartData { get; private set; }
		public double RadiationRate { get; private set; }
		public bool IsOccluder { get; private set; } = false;
		public bool IsReceiver { get; private set; } = false;
		public bool IsEmitter { get; private set; } = false;
		public List<IRadiationEmitter> RadiationEmitters { get; private set; }

		#endregion

		#region LIFECYLE

		public PartRadiationData(PartData partData)
		{
			PartData = partData;
		}

		public void PostInstantiateSetup()
		{
			IsOccluder = PartData.volumeAndSurface != null;

			foreach (ModuleData md in PartData.modules)
			{
				if (md is IRadiationReceiver receiver && receiver.EnableInterface)
				{
					IsReceiver = true;
				}
				else if (md is IRadiationEmitter emitter && emitter.EnableInterface)
				{
					if (RadiationEmitters == null)
						RadiationEmitters = new List<IRadiationEmitter>();

					RadiationEmitters.Add(emitter);
					IsEmitter = true;
				}
			}

			if (IsReceiver)
			{
				if (radiationCoilArrays == null)
					radiationCoilArrays = new List<CoilArrayShielding>();

				if (sunRaycastTask == null)
					sunRaycastTask = new SunRaycastTask(this);

				if (emitterRaycastTasks == null)
					emitterRaycastTasks = new List<EmitterRaycastTask>();
			}

			if (IsOccluder)
			{
				structuralOcclusion = new PartStructuralOcclusion();
				resourcesOcclusion = new List<ResourceOcclusion>();
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
		private double radiationRateDbg;
		private double stormRadiationFactorDbg;
		private double stormRadiationDbg;
		private double emittersRadiationDbg;

		// debug occluder info
		private string lastRaycastDbg;
		private double rayPenetrationDbg;
		private double blockedRadDbg;
		private double bremsstrahlungDbg;
		private double crossSectionFactorDbg;

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

			if (PartData.vesselData.LoadedOrEditor)
			{
				if (IsReceiver)
				{
					UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.PartRadiationData.Update.Arrays");
					radiationCoilArrays.Clear();
					foreach (RadiationCoilData array in PartData.vesselData.ObjectsCache.RadiationArrays)
					{
						double protection = array.loadedModule.GetPartProtectionFactor(PartData.LoadedPart);
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
				stormRadiationDbg = 0.0;
				emittersRadiationDbg = 0.0;

				// add "ambiant" radiation (background, belts, bodies...)
				RadiationRate = PartData.vesselData.EnvRadiation * OcclusionFactor(false, true, false);

				// synchronize emitters references and add their radiation
				int vesselEmittersCount = PartData.vesselData.ObjectsCache.RadiationEmitters.Count;
				int tasksCount = emitterRaycastTasks.Count;

				if (tasksCount > vesselEmittersCount)
				{
					emitterRaycastTasks.RemoveRange(vesselEmittersCount, tasksCount - vesselEmittersCount);
				}

				for (int i = 0; i < vesselEmittersCount; i++)
				{
					if (i + 1 > tasksCount)
					{
						emitterRaycastTasks.Add(new EmitterRaycastTask(this, PartData.vesselData.ObjectsCache.RadiationEmitters[i]));
					}
					else
					{
						emitterRaycastTasks[i].CheckEmitterHasChanged(PartData.vesselData.ObjectsCache.RadiationEmitters[i]);
					}

					RadiationRate += emitterRaycastTasks[i].Radiation();
					emittersRadiationDbg += emitterRaycastTasks[i].Radiation();
				}

				// add storm radiation, if there is a storm
				stormRadiationFactorDbg = sunRaycastTask.sunRadiationFactor;
				if (PartData.vesselData.EnvStorm)
				{
					RadiationRate += PartData.vesselData.EnvStormRadiation * sunRaycastTask.sunRadiationFactor;
					stormRadiationDbg = PartData.vesselData.EnvStormRadiation * sunRaycastTask.sunRadiationFactor;
				}

				// substract magnetic shields effects
				foreach (CoilArrayShielding arrayData in radiationCoilArrays)
				{
					RadiationRate -= arrayData.RadiationRemoved;
				}

				// clamp to nominal
				RadiationRate = Math.Max(RadiationRate, Radiation.Nominal);
				radiationRateDbg = RadiationRate;

				// accumulate total radiation received by this part
				accumulatedRadiation += RadiationRate * elapsedSecSinceLastUpdate;
			}

			elapsedSecSinceLastUpdate = 0.0;

			UnityEngine.Profiling.Profiler.EndSample();

			if (!debug && PartData.vesselData.LoadedOrEditor)
			{
				debug = true;

				if (PartData.vesselData.LoadedOrEditor)
				{
					if (IsReceiver)
					{
						PartData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), GetType().GetField(nameof(radiationRateDbg), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance), this));
						PartData.LoadedPart.Fields[nameof(radiationRateDbg)].guiName = "Radiation";
						PartData.LoadedPart.Fields[nameof(radiationRateDbg)].guiFormat = "F10";
						PartData.LoadedPart.Fields[nameof(radiationRateDbg)].guiUnits = " rad/s";

						PartData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), GetType().GetField(nameof(emittersRadiationDbg), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance), this));
						PartData.LoadedPart.Fields[nameof(emittersRadiationDbg)].guiName = "Emitters";
						PartData.LoadedPart.Fields[nameof(emittersRadiationDbg)].guiFormat = "F10";
						PartData.LoadedPart.Fields[nameof(emittersRadiationDbg)].guiUnits = " rad/s";

						PartData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), GetType().GetField(nameof(stormRadiationDbg), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance), this));
						PartData.LoadedPart.Fields[nameof(stormRadiationDbg)].guiName = "Storm";
						PartData.LoadedPart.Fields[nameof(stormRadiationDbg)].guiFormat = "F10";
						PartData.LoadedPart.Fields[nameof(stormRadiationDbg)].guiUnits = " rad/s";

						PartData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), GetType().GetField(nameof(stormRadiationFactorDbg), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance), this));
						PartData.LoadedPart.Fields[nameof(stormRadiationFactorDbg)].guiName = "Storm rad factor";
						PartData.LoadedPart.Fields[nameof(stormRadiationFactorDbg)].guiFormat = "P6";
					}

					if (IsOccluder)
					{
						PartData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), GetType().GetField(nameof(lastRaycastDbg), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance), this));

						PartData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), GetType().GetField(nameof(rayPenetrationDbg), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance), this));
						PartData.LoadedPart.Fields[nameof(rayPenetrationDbg)].guiName = "rayPenetration";
						PartData.LoadedPart.Fields[nameof(rayPenetrationDbg)].guiFormat = "F3";
						PartData.LoadedPart.Fields[nameof(rayPenetrationDbg)].guiUnits = "m";

						PartData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), GetType().GetField(nameof(blockedRadDbg), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance), this));
						PartData.LoadedPart.Fields[nameof(blockedRadDbg)].guiName = "blockedRad";
						PartData.LoadedPart.Fields[nameof(blockedRadDbg)].guiFormat = "P6";

						PartData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), GetType().GetField(nameof(bremsstrahlungDbg), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance), this));
						PartData.LoadedPart.Fields[nameof(bremsstrahlungDbg)].guiName = "bremsstrahlung";
						PartData.LoadedPart.Fields[nameof(bremsstrahlungDbg)].guiFormat = "P6";

						PartData.LoadedPart.Fields.Add(new BaseField(new UI_Label(), GetType().GetField(nameof(crossSectionFactorDbg), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance), this));
						PartData.LoadedPart.Fields[nameof(crossSectionFactorDbg)].guiName = "crossSectionFactor";
						PartData.LoadedPart.Fields[nameof(crossSectionFactorDbg)].guiFormat = "P6";
					}


				}
			}
		}

		// TODO : use the actual part mass, not the prefab mass
		// Since the part can be unloaded, this require either storing the protopart reference in PartData, or storing the mass independently (a bit silly)
		public void GetOccluderStats()
		{
			structuralOcclusion.Update(PartData.PartPrefab.mass, PartData.volumeAndSurface.surface, PartData.volumeAndSurface.volume);
		}

		private void UpdateOcclusionStats()
		{
			int listCapacity = resourcesOcclusion.Count;
			int occluderIndex = -1;

			for (int i = 0; i < PartData.LoadedPart.Resources.Count; i++)
			{
				PartResource res = PartData.LoadedPart.Resources[i];

				if (res.info.density == 0.0)
					continue;

				occluderIndex++;
				if (occluderIndex >= listCapacity)
				{
					resourcesOcclusion.Add(new ResourceOcclusion(res.info));
					listCapacity++;
				}

				resourcesOcclusion[occluderIndex].UpdateOcclusion(res, PartData.volumeAndSurface.surface, PartData.volumeAndSurface.volume);
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
