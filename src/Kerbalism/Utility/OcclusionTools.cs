using System;
using System.IO;
using System.IO.Compression;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace KERBALISM.Utility
{
	public class OcclusionTools : IDisposable
	{
		public NativeArray<quaternion> points512, points1024, points2048;
		private const float Lattice_epsilon = 0.36f;

		public OcclusionTools()
		{
			points512 = new NativeArray<quaternion>(512, Allocator.Persistent);
			points1024 = new NativeArray<quaternion>(1024, Allocator.Persistent);
			points2048 = new NativeArray<quaternion>(2048, Allocator.Persistent);
			var x512 = new SpherePointsJob(512, Lattice_epsilon)
			{
				results = points512,
			}.Schedule(512, 16);
			var x1024 = new SpherePointsJob(1024, Lattice_epsilon)
			{
				results = points1024,
			}.Schedule(1024, 16);
			var x2048 = new SpherePointsJob(2048, Lattice_epsilon)
			{
				results = points2048,
			}.Schedule(2048, 16);
			JobHandle.ScheduleBatchedJobs();
			JobHandle.CompleteAll(ref x512, ref x1024, ref x2048);
		}

		private static OcclusionTools _instance = null;
		public static OcclusionTools Instance
		{
			get
			{
				if (_instance == null) _instance = new OcclusionTools();
				return _instance;
			}
		}

		public void Dispose()
		{
			if (points512.IsCreated) points512.Dispose();
			if (points1024.IsCreated) points1024.Dispose();
			if (points2048.IsCreated) points2048.Dispose();
		}

		public quaternion GetNearestQuaternion(in NativeArray<quaternion> points, float3 vec)
		{
			var dots = new NativeArray<float>(points.Length, Allocator.TempJob);
			var job = new DotProductJob
			{
				quaternions = points,
				vec = vec,
				dot = dots
			}.Schedule(points.Length, 16);
			job.Complete();
			var arr = dots.ToArray();
			float max = Mathf.Max(arr);
			dots.Dispose();
			return points[arr.IndexOf(max)];
		}

		public struct DotProductJob : IJobParallelFor
		{
			[ReadOnly] public NativeArray<quaternion> quaternions;
			[ReadOnly] public float3 vec;
			[WriteOnly] public NativeArray<float> dot;

			public void Execute(int index)
			{
				var fwd = new float3(0, 0, 1);
				float3 a = math.mul(quaternions[index], fwd);
				dot[index] = math.dot(a, vec);
			}
		}

		//http://extremelearning.com.au/how-to-evenly-distribute-points-on-a-sphere-more-effectively-than-the-canonical-fibonacci-lattice/
		//[BurstCompile]
		public struct SpherePointsJob : IJobParallelFor
		{
			[ReadOnly] public int points;
			[ReadOnly] public float epsilon;
			[WriteOnly] public NativeArray<quaternion> results;
			private const float GoldenRatio = 1.61803398875f;   //(1 + math.sqrt(5)) / 2;
			private const float ThetaConstantTerm = 2 * math.PI / GoldenRatio;
			private readonly float denomRecip;

			public SpherePointsJob(int points, float epsilon) : this()
			{
				this.points = points;
				this.epsilon = epsilon;
				denomRecip = 1f / (points - 1 + (2 * epsilon));
			}

			public void Execute(int index)
			{
				if (Unity.Burst.CompilerServices.Hint.Unlikely(index == 0))
				{
					results[index] = quaternion.identity;
				}
				else if (Unity.Burst.CompilerServices.Hint.Unlikely(index == points - 1))
				{
					results[index] = quaternion.Euler(180, 0, 0);
				}
				else
				{
					float theta = index * ThetaConstantTerm;
					float num = 2 * (index + epsilon);
					float cosPhi = 1 - num * denomRecip;
					float sinPhi = math.sqrt(1 - cosPhi * cosPhi);
					math.sincos(theta, out float sinTheta, out float cosTheta);
					var res = new float3(cosTheta * sinPhi,
											sinTheta * sinPhi,
											cosPhi);
					results[index] = Quaternion.FromToRotation(new float3(0, 0, 1), res);
				}
			}
		}

		public static byte[] Compress(byte[] data)
		{
			using (var compressedStream = new MemoryStream())
			using (var zipStream = new GZipStream(compressedStream, CompressionMode.Compress))
			{
				zipStream.Write(data, 0, data.Length);
				zipStream.Close();
				return compressedStream.ToArray();
			}
		}

		public static byte[] Decompress(byte[] data)
		{
			using (var compressedStream = new MemoryStream(data))
			using (var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
			using (var resultStream = new MemoryStream())
			{
				zipStream.CopyTo(resultStream);
				return resultStream.ToArray();
			}
		}
	}
}
