using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KERBALISM.Modules
{
	public class BoundsTestModule : PartModule
	{
		private class InterBB
		{
			public Transform transform;
			public Vector3 size;
			public Vector3 center;

			public InterBB(Transform transform, Vector3 size, Vector3 center)
			{
				this.transform = transform;
				this.size = size;
				this.center = center;
			}
		}

		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "BBVolume")]//Radiation
		public double volume;  // rate of radiation emitted

		private Bounds bounds;

		private List<InterBB> intermediateBounds = new List<InterBB>();
		private List<Bounds> intermediateTBounds = new List<Bounds>();

		// foreach meshfilter
		// - get mesh bounds
		// - get the 8 points of the bounds
		// - transform those points acording to the meshfilter transform
		// - get a bounds aligned to the part transform orientation that contains all 8 points
		// merge all bounds

		public void Update()
		{
			bounds = ColliderBounds(part.partTransform);
			volume = PartVolumeAndSurface.BoundsVolume(bounds);
		}

		//public void OnRenderObject()
		//{
		//	foreach (InterBB bb in intermediateBounds)
		//	{
		//		DrawTools.DrawLocalCube(bb.transform, bb.size, Color.yellow, bb.center);
		//	}

		//	foreach (Bounds bb in intermediateTBounds)
		//	{
		//		DrawTools.DrawBounds(bb, Color.cyan);
		//	}

		//	DrawTools.DrawBounds(part.GetPartRendererBound(), Color.red);
		//	DrawTools.DrawBounds(bounds, Color.green);
		//}

		private Bounds ColliderBounds(Transform partTransform)
		{
			intermediateBounds.Clear();
			intermediateTBounds.Clear();

			Bounds bounds = default;

			foreach (MeshCollider meshCollider in partTransform.GetComponentsInChildren<MeshCollider>(false))
			{
				if (meshCollider.gameObject.layer != 0)
					continue;

				bounds = MergeComponentBoundToWorldBound(bounds, partTransform, meshCollider, meshCollider.sharedMesh.bounds);
			}

			foreach (BoxCollider boxCollider in partTransform.GetComponentsInChildren<BoxCollider>(false))
			{
				if (boxCollider.gameObject.layer != 0)
					continue;
				Bounds boxBounds = new Bounds(boxCollider.center, boxCollider.size);
				bounds = MergeComponentBoundToWorldBound(bounds, partTransform, boxCollider, boxBounds);
			}

			foreach (SphereCollider sphereCollider in partTransform.GetComponentsInChildren<SphereCollider>(false))
			{
				if (sphereCollider.gameObject.layer != 0)
					continue;
				Bounds sphereBounds = new Bounds(sphereCollider.center, new Vector3(sphereCollider.radius * 2f, sphereCollider.radius * 2f, sphereCollider.radius * 2f));
				bounds = MergeComponentBoundToWorldBound(bounds, partTransform, sphereCollider, sphereBounds);
			}

			foreach (CapsuleCollider capsuleCollider in partTransform.GetComponentsInChildren<CapsuleCollider>(false))
			{
				if (capsuleCollider.gameObject.layer != 0)
					continue;
				Vector3 capsuleSize;
				switch (capsuleCollider.direction)
				{
					case 0: capsuleSize = new Vector3(capsuleCollider.height, capsuleCollider.radius * 2f, capsuleCollider.radius * 2f); break;
					case 1: capsuleSize = new Vector3(capsuleCollider.radius * 2f, capsuleCollider.height, capsuleCollider.radius * 2f); break;
					case 2: capsuleSize = new Vector3(capsuleCollider.radius * 2f, capsuleCollider.radius * 2f, capsuleCollider.height); break;
					default: capsuleSize = default; break;
				}
				Bounds capsuleBounds = new Bounds(capsuleCollider.center, capsuleSize);
				bounds = MergeComponentBoundToWorldBound(bounds, partTransform, capsuleCollider, capsuleBounds);
			}

			return bounds;
		}


		private Bounds GetTransformRootAndChildrensBounds(Transform partTransform)
		{
			intermediateBounds.Clear();
			intermediateTBounds.Clear();

			Bounds bounds = default;

			MeshFilter[] meshFilters = partTransform.GetComponentsInChildren<MeshFilter>(false);

			foreach (MeshFilter meshFilter in meshFilters)
			{
				// Ignore colliders
				if (meshFilter.gameObject.GetComponent<MeshCollider>() != null)
					continue;

				// Ignore non rendered meshes
				MeshRenderer renderer = meshFilter.gameObject.GetComponent<MeshRenderer>();
				if (renderer == null || !renderer.enabled)
					continue;

				bounds = MergeComponentBoundToWorldBound(bounds, partTransform, meshFilter, meshFilter.sharedMesh.bounds);
			}

			SkinnedMeshRenderer[] skinnedMeshRenderers = partTransform.GetComponentsInChildren<SkinnedMeshRenderer>(false);

			foreach (SkinnedMeshRenderer skinnedMeshRenderer in skinnedMeshRenderers)
			{
				if (!skinnedMeshRenderer.enabled)
				{
					continue;
				}

				bounds = MergeComponentBoundToWorldBound(bounds, partTransform, skinnedMeshRenderer, skinnedMeshRenderer.localBounds);
			}

			return bounds;
		}

		private Bounds MergeComponentBoundToWorldBound(Bounds worldBounds, Transform partTransform, Component component, Bounds meshBounds)
		{
			Transform localT = component.transform; 
			Vector3[] boundsPoints = new Vector3[8];

			intermediateBounds.Add(new InterBB(localT, meshBounds.size, meshBounds.center));

			boundsPoints[0] = localT.TransformPoint(meshBounds.center + new Vector3(-meshBounds.size.x, meshBounds.size.y, -meshBounds.size.z) * 0.5f);
			boundsPoints[1] = localT.TransformPoint(meshBounds.center + new Vector3(meshBounds.size.x, meshBounds.size.y, -meshBounds.size.z) * 0.5f);
			boundsPoints[2] = localT.TransformPoint(meshBounds.center + new Vector3(meshBounds.size.x, meshBounds.size.y, meshBounds.size.z) * 0.5f);
			boundsPoints[3] = localT.TransformPoint(meshBounds.center + new Vector3(-meshBounds.size.x, meshBounds.size.y, meshBounds.size.z) * 0.5f);

			boundsPoints[4] = localT.TransformPoint(meshBounds.center + new Vector3(-meshBounds.size.x, -meshBounds.size.y, -meshBounds.size.z) * 0.5f);
			boundsPoints[5] = localT.TransformPoint(meshBounds.center + new Vector3(meshBounds.size.x, -meshBounds.size.y, -meshBounds.size.z) * 0.5f);
			boundsPoints[6] = localT.TransformPoint(meshBounds.center + new Vector3(meshBounds.size.x, -meshBounds.size.y, meshBounds.size.z) * 0.5f);
			boundsPoints[7] = localT.TransformPoint(meshBounds.center + new Vector3(-meshBounds.size.x, -meshBounds.size.y, meshBounds.size.z) * 0.5f);

			Bounds partBounds = GeometryUtility.CalculateBounds(boundsPoints, partTransform.worldToLocalMatrix);
			intermediateTBounds.Add(partBounds);

			worldBounds.Encapsulate(partBounds);

			return worldBounds;
		}
	}
}
