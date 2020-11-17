using System;
using System.Collections.Generic;
using UnityEngine;

namespace KERBALISM
{
	public partial class PartRadiationData
	{
		// Note : all fields here are temporary variables used for raycasting.
		// They are here for convenience, to avoid heap allocs and creating separate objects

		// optimization to avoid keeping computing radiation levels that don't matter
		private const double minFactor = 1e-9;

		// the hit point closest from rayOrigin
		private RaycastHit fromHit;
		private bool fromHitExists = false;

		// the hit point farthest from rayOrigin
		private RaycastHit toHit;
		private bool toHitExists = false;

		// distance between the two hit points
		private double hitPenetration;

		// we maintain our own cache of renderers because despite FindModelRenderersCached() caching
		// a list, it instantiate a copy of it every time. Note that anything iterating on this list MUST
		// check if Renderer == null before using it, and if that check is true, set partRenderers = null
		// Also note that this will ignore any renderer creating druing the part lifetime, like fairing
		// edition in the editor (and possibly procedural parts mods).
		private List<Renderer> partRenderers;

		public void UpdateRenderers()
		{
			if (partData.LoadedPart == null)
			{
				if (partRenderers != null)
				{
					partRenderers = null;
				}
			}
			else
			{
				if (partRenderers == null)
				{
					// On instantiating a new vessel from the VAB, child parts will be parented (as in unity-parented)
					// to their parent. This mean doing a FindComponentInChildren() on the root part will return the whole vessel.
					// Note this isn't the case on a loaded from save vessel : parts are top level (world) objects.
					// So, we rely on the stock FindModelRenderersCached() which get the model GO by its name (usually the first child
					// of the part GO). Note that as usual there are a few special casesregarding the GO name, so better to let KSP
					// handle this.
					partRenderers = partData.LoadedPart.FindModelRenderersCached();
					for (int i = partRenderers.Count - 1; i >= 0; i--)
					{
						if (!(partRenderers[i] is MeshRenderer || partRenderers[i] is SkinnedMeshRenderer))
						{
							partRenderers.RemoveAt(i);
						}
					}
				}
			}
		}

		/// <summary>
		/// Compute penetration and ray impact angle
		/// </summary>
		private void AnalyzeRaycastHit()
		{
			hitPenetration = (fromHit.point - toHit.point).magnitude;
		}

		/// <summary>
		/// Reset the "occluder has hits" flags.
		/// **Must** be called for every part that has been added to the occluder list after a OcclusionRaycast() call
		/// </summary>
		private void ResetRaycastHit()
		{
			toHitExists = false;
			fromHitExists = false;
		}

		public abstract class RaycastTask
		{
			protected PartRadiationData origin;

			public RaycastTask(PartRadiationData origin)
			{
				this.origin = origin;

				if (layerMask < 0)
				{
					// AeroFXIgnore -> solar panels, possibly other parts...
					layerMask = LayerMask.GetMask(new string[] { "Default", "AeroFxIgnore" });
				}
			}

			public virtual void Raycast(RaycastTask nextTask)
			{
				if (nextTask == null || nextTask.origin != origin)
				{
					origin.raycastDone = true;
				}
			}

			protected Bounds GetPartBounds(PartRadiationData prd)
			{
				if (prd.partRenderers == null)
				{
					return default;
				}

				Vector3 min = default;
				Vector3 max = default;
				bool first = true;
				foreach (Renderer renderer in prd.partRenderers)
				{
					if (renderer == null)
					{
						prd.partRenderers = null;
					}

					if (!renderer.enabled)
					{
						continue;
					}

					if (first)
					{
						first = false;
						min = renderer.bounds.min;
						max = renderer.bounds.max;
					}
					else
					{
						min = Vector3.Min(min, renderer.bounds.min);
						max = Vector3.Max(max, renderer.bounds.max);
					}
				}

				Vector3 size = max - min;
				return new Bounds(min + (size * 0.5f), size);
			}

			// TODO : call this on scene changes
			public static void ClearLoadedPartsCache()
			{
				prdByTransforms.Clear();
			}

			private static RaycastHit[] hitsBuffer = new RaycastHit[500];

			private static Dictionary<int, PartRadiationData> prdByTransforms = new Dictionary<int, PartRadiationData>();

			private static int layerMask = -1;

			protected static List<PartRadiationData> hittedParts = new List<PartRadiationData>();

			protected enum Direction { X = 0, Y = 1, Z = 2}

			protected struct Section
			{
				readonly float aMin;
				readonly float aMax;
				readonly float bMin;
				readonly float bMax;

				public Section(Vector3 point, Bounds bb, Direction dir)
				{
					Vector3 bbMin = bb.min;
					Vector3 bbMax = bb.max;
					switch (dir)
					{
						case Direction.X:
							aMin = point.y - bbMin.y;
							aMax = bbMax.y - point.y;
							bMin = point.z - bbMin.z;
							bMax = bbMax.z - point.z;
							break;
						case Direction.Y:
							aMin = point.x - bbMin.x;
							aMax = bbMax.x - point.x;
							bMin = point.z - bbMin.z;
							bMax = bbMax.z - point.z;
							break;
						case Direction.Z:
							aMin = point.x - bbMin.x;
							aMax = bbMax.x - point.x;
							bMin = point.y - bbMin.y;
							bMax = bbMax.y - point.y;
							break;
						default:
							aMin = aMax = bMin = bMax = 0f;
							break;
					}
				}

				public double OccluderMinFactor(Section occluder)
				{
					float factor = 0f;

					factor += Math.Min(
						aMin > 0f ? Math.Min(1f, occluder.aMin / aMin) : 1f,
						aMax > 0f ? Math.Min(1f, occluder.aMax / aMax) : 1f);
					factor += Math.Min(
						bMin > 0f ? Math.Min(1f, occluder.bMin / bMin) : 1f,
						bMax > 0f ? Math.Min(1f, occluder.bMax / bMax) : 1f);

					// x^1.5 : arbitrary balancing based on experimentation
					return Math.Pow(factor / 2.0, 1.5);
				}

				public double OccluderFactor(Section occluder)
				{
					float factor = 0f;

					factor += aMin > 0f ? Math.Min(1f, occluder.aMin / aMin) : 1f;
					factor += aMax > 0f ? Math.Min(1f, occluder.aMax / aMax) : 1f;
					factor += bMin > 0f ? Math.Min(1f, occluder.bMin / bMin) : 1f;
					factor += bMax > 0f ? Math.Min(1f, occluder.bMax / bMax) : 1f;

					// x^1.5 : arbitrary balancing based on experimentation
					return Math.Pow(factor / 4.0, 1.5);
				}

				public override string ToString()
				{
					return $"{aMin:F2}, {aMax:F2}, {bMin:F2}, {bMax:F2}";
				}
			}

			protected Direction PrimaryDirection(Vector3 rayDirection)
			{
				float maxWeight = 0f;
				int max = 0;
				for (int i = 0; i < 3; i++)
				{
					float dirWeight = Math.Abs(rayDirection[i]);
					if (dirWeight > maxWeight)
					{
						max = i;
						maxWeight = dirWeight;
					}
				}

				return (Direction)max;
			}

			/// <summary>
			/// Get the PartRadiationData that this transform belong to. If the transform isn't cached, return false.
			/// If the part doesn't exist anymore (unloaded, destroyed...), clean the cache.
			/// </summary>
			private static bool TryGetRadiationDataForTransformCached(Transform transform, out PartRadiationData partRadiationData)
			{
				if (prdByTransforms.TryGetValue(transform.GetInstanceID(), out partRadiationData))
				{
					if (partRadiationData.partData.LoadedPart.gameObject == null)
					{
						prdByTransforms.Remove(transform.GetInstanceID());
						return false;
					}
					return true;
				}
				return false;
			}

			/// <summary>
			/// Get the PartRadiationData that this transform belong to, and store it in the static cache.
			/// </summary>
			private static bool TryGetRadiationDataForTransform(Transform transform, out PartRadiationData partRadiationData)
			{
				Part hittedPart = transform.GetComponentInParent<Part>();
				if (hittedPart == null)
				{
					partRadiationData = null;
					return false;
				}

				partRadiationData = PartData.GetLoadedPartData(hittedPart).radiationData;
				prdByTransforms.Add(transform.GetInstanceID(), partRadiationData);
				return true;
			}

			/// <summary>
			/// Perform a bidirectional raycast to get every occluder along rayDir.
			/// This allow to compute the penetration depth inside each occluder, as well as the angle of impact.
			/// </summary>
			/// <param name="rayOrigin">Position of the part that we want to get occlusion for</param>
			/// <param name="rayDir">Normalized direction vector</param>
			protected static void OcclusionRaycast(Vector3 rayOrigin, Vector3 rayDir)
			{
				hittedParts.Clear();

				// raycast from the origin part in direction of the ray
				int hitCount = Physics.RaycastNonAlloc(rayOrigin, rayDir, hitsBuffer, 250f, layerMask);
				for (int i = 0; i < hitCount; i++)
				{
					// if the transform is known, fetch the corresponding occluding part
					if (TryGetRadiationDataForTransformCached(hitsBuffer[i].transform, out PartRadiationData partRadiationData))
					{
						if (!partRadiationData.IsOccluder)
						{
							continue;
						}

						// if the occluding part isn't marked as occluding for this raycast, save the hit and add the part to the occluder list
						if (!partRadiationData.fromHitExists)
						{
							partRadiationData.fromHitExists = true;
							partRadiationData.fromHit = hitsBuffer[i];
							hittedParts.Add(partRadiationData);
						}
						// if we are getting another hit for the same occluding part (multiple colliders), retain the hit that is the closest from the origin part
						else if (hitsBuffer[i].distance < partRadiationData.fromHit.distance)
						{
							partRadiationData.fromHit = hitsBuffer[i];
						}
					}
					// else find the occluding part that match this transform (and add it to the cache for next time)
					else
					{
						if (!TryGetRadiationDataForTransform(hitsBuffer[i].transform, out partRadiationData))
						{
							continue;
						}

						if (!partRadiationData.IsOccluder)
						{
							continue;
						}

						partRadiationData.fromHit = hitsBuffer[i];
						hittedParts.Add(partRadiationData);
					}
				}

				// now raycast in the opposite direction, toward the origin part
				hitCount = Physics.RaycastNonAlloc(rayOrigin + rayDir * 250f, -rayDir, hitsBuffer, 250f, layerMask);
				for (int i = 0; i < hitCount; i++)
				{
					// if the transform is known, fetch the corresponding occluding part
					if (TryGetRadiationDataForTransformCached(hitsBuffer[i].transform, out PartRadiationData partRadiationData))
					{
						// if the occuding part hasn't been hit on the first raycast, ignore it
						if (!partRadiationData.IsOccluder || !partRadiationData.fromHitExists)
						{
							continue;
						}

						// same logic as in the first raycast
						if (!partRadiationData.toHitExists)
						{
							partRadiationData.toHitExists = true;
							partRadiationData.toHit = hitsBuffer[i];
						}
						else if (hitsBuffer[i].distance < partRadiationData.fromHit.distance)
						{
							partRadiationData.toHit = hitsBuffer[i];
						}
					}
					else
					{
						if (!TryGetRadiationDataForTransform(hitsBuffer[i].transform, out partRadiationData))
						{
							continue;
						}

						if (!partRadiationData.IsOccluder || !partRadiationData.fromHitExists)
						{
							continue;
						}

						partRadiationData.toHit = hitsBuffer[i];

					}
				}

				// sort by distance, in reverse
				hittedParts.Sort((a, b) => b.toHit.distance.CompareTo(a.toHit.distance));
			}
		}

		private enum WorldDir { X, Y, Z }

		private class SunRaycastTask : RaycastTask
		{
			public double sunRadiationFactor = 1.0;

			public SunRaycastTask(PartRadiationData origin) : base(origin) { }

			public override void Raycast(RaycastTask nextTask)
			{
				base.Raycast(nextTask);

				Vector3 sunDirection = origin.partData.vesselData.MainStarDirection;

				OcclusionRaycast(origin.partData.LoadedPart.WCoM, sunDirection);

				Bounds originBounds = GetPartBounds(origin);
				Direction direction = PrimaryDirection(sunDirection);
				Section originSection = new Section(origin.partData.LoadedPart.WCoM, originBounds, direction);

				DebugDrawer.Draw(new DebugDrawer.Bounds(originBounds, Color.green, 50));
				DebugDrawer.Draw(new DebugDrawer.Point(origin.partData.LoadedPart.WCoM, Color.green, 50));
				DebugDrawer.Draw(new DebugDrawer.Line(origin.partData.LoadedPart.WCoM, sunDirection, Color.red, 50f, 50));

				// Explaination :
				// When high energy charged particules from CME events hit a solid surface, three things happen :
				// - the charged particules loose some energy
				// - its travelling direction is slightly altered
				// - secondary particules (photons) are emitted
				// Those secondary particules are called bremsstrahlung radiation.
				// For medium to high energy (> 10 MeV) particules, the vast majority of bremsstrahlung is
				// emitted in same direction as the CME particules.
				// For our purpose, we use a very simplified model where we assume that all the blocked CME radiation
				// is converted into bremmstralung radiation in same direction as the original CME and with a 20° dispersion.

				// Note that we are computing a [0;1] factor here, not actual radiation level.
				double cmeRadiation = 1.0;
				double bremsstrahlung = 0.0;

				foreach (PartRadiationData prd in hittedParts)
				{
					// optimization to avoid keeping computing radiation levels that don't matter
					if (cmeRadiation + bremsstrahlung < minFactor || prd == origin)
					{
						prd.ResetRaycastHit();
						continue;
					}

					prd.AnalyzeRaycastHit();

					//Vector3 midPoint = prd.toHit.point + ((prd.fromHit.point - prd.toHit.point) * 0.5f);
					//DebugDrawer.Draw(new DebugDrawer.Point(midPoint, Color.green, 30));

					Bounds occluderBounds = GetPartBounds(prd);
					Section occluderSection = new Section(prd.fromHit.point, occluderBounds, direction);
					double sectionRatio = originSection.OccluderFactor(occluderSection);

					// get the high energy radiation that is blocked by the part, using "high energy" HVL.
					double partBremsstrahlung = cmeRadiation * prd.OcclusionFactor(true) * sectionRatio;

					// get the remaining high energy radiation
					cmeRadiation -= partBremsstrahlung;

					// get the remaining bremsstrahlung that hasn't been blocked by the part, using "low energy" HVL.
					bremsstrahlung -= bremsstrahlung * prd.OcclusionFactor(false) * sectionRatio;

					// add the bremsstrahlung created by the CME radiation hitting the part
					// Assumption : the bremsstrahlung is emitted in the same direction as the original CME radiation, in a 20° cone
					double sqrDistance = (prd.toHit.point - origin.partData.LoadedPart.WCoM).sqrMagnitude;
					bremsstrahlung += partBremsstrahlung / Math.Max(1.0, 0.222 * Math.PI * sqrDistance);

					DebugDrawer.Draw(new DebugDrawer.Bounds(occluderBounds, Color.yellow, 50));
					DebugDrawer.Draw(new DebugDrawer.Point(prd.toHit.point, Color.blue, 50));
					DebugDrawer.Draw(new DebugDrawer.Point(prd.fromHit.point, Color.red, 50));

					prd.lastRaycast = $"sun-{Time.frameCount}";
					prd.rayPenetration = prd.hitPenetration;
					prd.crossSectionFactor = sectionRatio;
					prd.blockedRad = partBremsstrahlung;
					prd.bremsstrahlung = partBremsstrahlung / Math.Max(1.0, 0.222 * Math.PI * sqrDistance);

					prd.ResetRaycastHit();
				}

				// factor in the origin part wall shielding
				cmeRadiation -= cmeRadiation * origin.OcclusionFactor(true, true, false);
				bremsstrahlung -= bremsstrahlung * origin.OcclusionFactor(false, true, false);

				sunRadiationFactor = Math.Max(cmeRadiation + bremsstrahlung, 0.0);
			}
		}

		private class EmitterRaycastTask : RaycastTask
		{
			private IRadiationEmitter emitter;

			private double reductionFactor = 0.0;
			private int emitterId;

			public EmitterRaycastTask(PartRadiationData origin, IRadiationEmitter emitter) : base(origin)
			{
				this.emitter = emitter;
				emitterId = emitter.ModuleId;
			}

			public EmitterRaycastTask(PartRadiationData origin, ConfigNode.Value value) : base(origin)
			{
				emitterId = Lib.Parse.ToInt(value.name);
				reductionFactor = Lib.Parse.ToDouble(value.value);
			}

			public void SaveToNode(ConfigNode emittersNode)
			{
				emittersNode.AddValue(emitterId.ToString(), reductionFactor.ToString());
			}

			/// <summary>
			/// To avoid creating/destructing objects when synchronizing the EmitterRaycastTask list in PartRadiationData,
			/// we just swap the emitter reference of the existing EmitterRaycastTask
			/// </summary>
			public void CheckEmitterHasChanged(IRadiationEmitter otherEmitter)
			{
				if (otherEmitter.ModuleId != emitterId)
				{
					emitter = otherEmitter;
					emitterId = otherEmitter.ModuleId;
					reductionFactor = 0.0;
				}
				else if (emitter == null)
				{
					emitter = otherEmitter;
				}
			}

			public double Radiation
			{
				get
				{
					if (emitter != null && emitter.IsActive)
					{
						return emitter.RadiationRate * reductionFactor;
					}

					return 0.0;
				}
			}

			public override void Raycast(RaycastTask nextTask)
			{
				base.Raycast(nextTask);

				Vector3 rayDir = emitter.RadiationData.partData.LoadedPart.WCoM - origin.partData.LoadedPart.WCoM;
				float distance = rayDir.magnitude;

				// compute initial radiation strength according to the emitter distance
				reductionFactor = KERBALISM.Radiation.DistanceRadiation(1.0, distance);
				rayDir /= distance;

				DebugDrawer.Draw(new DebugDrawer.Line(origin.partData.LoadedPart.WCoM, rayDir, Color.yellow, 50f, 50));

				OcclusionRaycast(origin.partData.LoadedPart.WCoM, rayDir);

				Bounds originBounds = GetPartBounds(origin);
				Direction direction = PrimaryDirection(rayDir);
				Section originSection = new Section(origin.partData.LoadedPart.WCoM, originBounds, direction);

				foreach (PartRadiationData prd in hittedParts)
				{
					// optimization to avoid keeping computing radiation levels that don't matter
					// also make sure we ignore the origin and emitter parts
					if (reductionFactor < minFactor || prd == origin || prd == emitter.RadiationData)
					{
						prd.ResetRaycastHit();
						continue;
					}

					prd.AnalyzeRaycastHit();

					Bounds occluderBounds = GetPartBounds(prd);
					Section occluderSection = new Section(prd.fromHit.point, occluderBounds, direction);
					double sectionRatio = originSection.OccluderFactor(occluderSection);

					prd.blockedRad = reductionFactor * prd.OcclusionFactor(false) * sectionRatio;
					reductionFactor -= prd.blockedRad;
					//reductionFactor -= reductionFactor * prd.OcclusionFactor(false);
					prd.ResetRaycastHit();

					prd.lastRaycast = $"emt-{Time.frameCount}";
					prd.rayPenetration = prd.hitPenetration;
					prd.crossSectionFactor = sectionRatio;
					prd.bremsstrahlung = 0.0;
				}

				// factor in the origin part wall shielding
				reductionFactor -= reductionFactor * origin.OcclusionFactor(false, true, false);
			}
		}
	}
}
