using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Linq;
using UnityEngine;
using KSP.UI.Screens;
using KSP.UI;
using KSP.UI.Screens.Flight;
using System.Collections;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using HarmonyLib;
using KSP.Localization;
using Debug = UnityEngine.Debug;

namespace KERBALISM
{
	public static class Lib
	{
		#region UTILS

		public enum LogLevel
		{
			Message,
			Warning,
			Error
		}

		private const string modName = "Kerbalism4";
		public const string gameDataDirectory = "Kerbalism4-Core";

		public static string KerbalismRootPath => Path.Combine(Path.GetFullPath(KSPUtil.ApplicationRootPath), "GameData", gameDataDirectory);

		///<summary>write a message to the log</summary>
		public static void Log(string message, LogLevel level = LogLevel.Message,
		[CallerMemberName] string memberName = "",
		[CallerFilePath] string sourceFilePath = "")
		{
			switch (level)
			{
				default:
					UnityEngine.Debug.Log($"[{modName}:{Path.GetFileNameWithoutExtension(sourceFilePath)}.{memberName}] {message.Replace("\n", "\r\n\t").TrimEnd()}");
					return;
				case LogLevel.Warning:
					UnityEngine.Debug.LogWarning($"[{modName}:{Path.GetFileNameWithoutExtension(sourceFilePath)}.{memberName}] {message.Replace("\n", "\r\n\t").TrimEnd()}");
					return;
				case LogLevel.Error:
					UnityEngine.Debug.LogError($"[{modName}:{Path.GetFileNameWithoutExtension(sourceFilePath)}.{memberName}] {message.Replace("\n", "\r\n\t").TrimEnd()}");
					return;
			}
		}

		///<summary>write a message and the call stack to the log</summary>
		public static void LogStack(string message, LogLevel level = LogLevel.Message,
			[CallerMemberName] string memberName = "",
			[CallerFilePath] string sourceFilePath = "")
		{
			StackTrace trace;

			switch (level)
			{
				default:
					trace = new StackTrace();
					UnityEngine.Debug.Log($"[{modName}:{Path.GetFileNameWithoutExtension(sourceFilePath)}.{memberName}] {message.Replace("\n", "\r\n\t").TrimEnd()}\r\n\t{trace.ToString().Replace("\n", "\r\n\t").TrimEnd()}");
					return;
				case LogLevel.Warning:
					trace = new StackTrace();
					UnityEngine.Debug.LogWarning($"[{modName}:{Path.GetFileNameWithoutExtension(sourceFilePath)}.{memberName}] {message.Replace("\n", "\r\n\t").TrimEnd()}\r\n\t{trace.ToString().Replace("\n", "\r\n\t").TrimEnd()}");
					return;
				case LogLevel.Error:
					// KSP will already log the stacktrace if the log level is error
					UnityEngine.Debug.LogError($"[{modName}:{Path.GetFileNameWithoutExtension(sourceFilePath)}.{memberName}] {message.Replace("\n", "\r\n\t").TrimEnd()}\r\n");
					return;
			}
		}

		///<summary>write a message to the log, only on DEBUG and DEVBUILD builds</summary>
		[Conditional("DEBUG"), Conditional("DEVBUILD")]
		public static void LogDebug(string message, LogLevel level = LogLevel.Message,
		[CallerMemberName] string memberName = "",
		[CallerFilePath] string sourceFilePath = "")
		{
			switch (level)
			{
				default:
					UnityEngine.Debug.Log($"[{modName}:{Path.GetFileNameWithoutExtension(sourceFilePath)}.{memberName}] {message.Replace("\n", "\r\n\t").TrimEnd()}");
					return;
				case LogLevel.Warning:
					UnityEngine.Debug.LogWarning($"[{modName}:{Path.GetFileNameWithoutExtension(sourceFilePath)}.{memberName}] {message.Replace("\n", "\r\n\t").TrimEnd()}");
					return;
				case LogLevel.Error:
					UnityEngine.Debug.LogError($"[{modName}:{Path.GetFileNameWithoutExtension(sourceFilePath)}.{memberName}] {message.Replace("\n", "\r\n\t").TrimEnd()}");
					return;
			}
		}

		///<summary>write a message and the full call stack to the log, only on DEBUG and DEVBUILD builds</summary>
		[Conditional("DEBUG"), Conditional("DEVBUILD")]
		public static void LogDebugStack(string message, LogLevel level = LogLevel.Message,
			[CallerMemberName] string memberName = "",
			[CallerFilePath] string sourceFilePath = "")
		{
			StackTrace trace;

			switch (level)
			{
				default:
					trace = new StackTrace();
					UnityEngine.Debug.Log($"[{modName}:{Path.GetFileNameWithoutExtension(sourceFilePath)}.{memberName}] {message.Replace("\n", "\r\n\t").TrimEnd()}\r\n\t{trace.ToString().Replace("\n", "\r\n\t").TrimEnd()}");
					return;
				case LogLevel.Warning:
					trace = new StackTrace();
					UnityEngine.Debug.LogWarning($"[{modName}:{Path.GetFileNameWithoutExtension(sourceFilePath)}.{memberName}] {message.Replace("\n", "\r\n\t").TrimEnd()}\r\n\t{trace.ToString().Replace("\n", "\r\n\t").TrimEnd()}");
					return;
				case LogLevel.Error:
					// KSP will already log the stacktrace if the log level is error
					UnityEngine.Debug.LogError($"[{modName}:{Path.GetFileNameWithoutExtension(sourceFilePath)}.{memberName}] {message.Replace("\n", "\r\n\t").TrimEnd()}\r\n");
					return;
			}
		}

		/// <summary> This constant is being set by the build system when a dev release is requested</summary>
#if DEVBUILD
		public static bool IsDevBuild => true ;
#else
		public static bool IsDevBuild => false;
#endif

		static Version kerbalismVersion;
		/// <summary> current Kerbalism major/minor version</summary>
		public static Version KerbalismVersion
		{
			get
			{
				if (kerbalismVersion == null) kerbalismVersion = new Version(Assembly.GetAssembly(typeof(Kerbalism)).GetName().Version.Major, Assembly.GetAssembly(typeof(Kerbalism)).GetName().Version.Minor);
				return kerbalismVersion;
			}
		}

		/// <summary> current KSP version as a "MajorMinor" string</summary>
		public static string KSPVersionCompact
		{
			get
			{
				return Versioning.version_major.ToString() + Versioning.version_minor.ToString();
			}
		}

		///<summary>swap two variables</summary>
		public static void Swap<T>(ref T a, ref T b)
		{
			T tmp = b;
			b = a;
			a = tmp;
		}

		#endregion

		#region MATH
		///<summary>clamp a value</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Clamp(int value, int min, int max)
		{
			if (min > max)
				throw new ArgumentException($"{min} is higher than {max}");

			if (value < min)
				return min;
			if (value > max)
				return max;

			return value;
		}

		///<summary>clamp a value</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float Clamp(float value, float min, float max)
		{
			if (min > max)
				throw new ArgumentException($"{min} is higher than {max}");

			if (value < min)
				return min;
			if (value > max)
				return max;

			return value;
		}

		///<summary>clamp a value</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static double Clamp(double value, double min, double max)
		{
			if (min > max)
				throw new ArgumentException($"{min} is higher than {max}");

			if (value < min)
				return min;
			if (value > max)
				return max;

			return value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static double ClampToPositive(double value)
		{
			return value < 0.0 ? 0.0 : value;
		}

		///<summary>blend between two values</summary>
		public static float Mix(float a, float b, float k)
		{
			return a * (1.0f - k) + b * k;
		}

		///<summary>blend between two values</summary>
		public static double Mix(double a, double b, double k)
		{
			return a * (1.0 - k) + b * k;
		}

		/// <summary>
		/// For a value in range [inputMin, inputMax], return the linearly scaled value in the range [outputMin, outputMax]
		/// </summary>
		public static double MapValueToRange(double value, double inputMin, double inputMax, double outputMin, double outputMax)
		{
			return (outputMax - outputMin) * (value - inputMin) / (inputMax - inputMin) + outputMin;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe bool IsNegativeOrNaN(this double value)
		{
			long doubleAsLong = *(long*)(&value);
			return doubleAsLong < 0L && doubleAsLong != -9223372036854775808L;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe bool IsZeroOrNegativeOrNaN(this double value)
		{
			long doubleAsLong = *(long*)(&value);
			return doubleAsLong <= 0L;
		}

		/// <summary> returns the smallest possible higher value. Only works with non-NaN, non-Infinity, non-zero positive values</summary>
		// More complete implementation : https://github.com/dotnet/runtime/blob/af4efb1936b407ca5f4576e81484cf5687b79a26/src/libraries/System.Private.CoreLib/src/System/Math.cs#L210-L258
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe double NextHigherPositiveDouble(double value)
		{
			long doubleAsLong = *(long*)(&value);
			doubleAsLong++;
			return *(double*)(&doubleAsLong);
		}

		#endregion

		#region 3D MATH

		/// <summary>
		/// return true if the ray intersects the sphere
		/// <br/>rayDir must be normalized
		/// </summary>
		public static bool RaySphereIntersection(Vector3d rayOrigin, Vector3d rayDir, Vector3d sphereCenter, double sphereRadius)
		{
			// vector from ray origin to sphere center
			Vector3d difference = sphereCenter - rayOrigin;

			// projection of ray origin -> sphere center over the raytracing direction
			double k = Vector3d.Dot(difference, rayDir);

			// the ray hit the sphere if its minimal analytical distance along the ray is more than the radius
			return k > 0.0 && (rayDir * k - difference).sqrMagnitude < sphereRadius * sphereRadius;
		}

		/// <summary>
		/// return the first intersection point if the ray intersects the sphere,
		/// <br/>return null if it doesn't intersect or if the ray origin is inside the sphere
		/// <br/>rayDir must be normalized
		/// </summary>
		public static Vector3d? RaySphereIntersectionPoint(Vector3d rayOrigin, Vector3d rayDir, Vector3d sphereCenter, double sphereRadius)
		{
			// vector from ray origin to sphere center
			Vector3d difference = sphereCenter - rayOrigin;

			double differenceLengthSquared = difference.sqrMagnitude;
			double sphereRadiusSquared = sphereRadius * sphereRadius;

			// If the distance between the ray start and the sphere's centre is less than
			// the radius of the sphere, we are inside the sphere.
			if (differenceLengthSquared < sphereRadiusSquared)
				return null;

			double distanceAlongRay = Vector3d.Dot(rayDir, difference);
			// If the ray is pointing away from the sphere then we don't ever intersect
			if (distanceAlongRay < 0)
				return null;

			// Next check if we are within the bounds of the sphere
			// with x = radius of sphere
			// with y = distance between ray position and sphere centre
			// with z = the distance we've travelled along the ray
			// if x^2 + z^2 - y^2 < 0, we do not intersect
			double dist = sphereRadiusSquared + distanceAlongRay * distanceAlongRay - differenceLengthSquared;

			if (dist < 0.0)
				return null;

			// get the intersection point
			double rayLength = distanceAlongRay - Math.Sqrt(dist);
			return rayOrigin + rayDir * rayLength;
		}

		/// <summary>
		/// return the first intersection point if the ray intersects the sphere,
		/// <br/>return null if it doesn't intersect or if the ray origin is inside the sphere
		/// <br/>rayDir must be normalized
		/// </summary>
		public static bool RaySphereIntersectionFloat(Vector3 rayOrigin, Vector3 rayDir, Vector3 sphereCenter, float sphereRadius, out Vector3 hitPoint)
		{
			// vector from ray origin to sphere center
			Vector3 difference = sphereCenter - rayOrigin;

			float differenceLengthSquared = difference.sqrMagnitude;
			float sphereRadiusSquared = sphereRadius * sphereRadius;

			// If the distance between the ray start and the sphere's centre is less than
			// the radius of the sphere, we are inside the sphere.
			if (differenceLengthSquared < sphereRadiusSquared)
			{
				hitPoint = Vector3.zero;
				return false;
			}

			float distanceAlongRay = Vector3.Dot(rayDir, difference);
			// If the ray is pointing away from the sphere then we don't ever intersect
			if (distanceAlongRay < 0)
			{
				hitPoint = Vector3.zero;
				return false;
			}

			// Next check if we are within the bounds of the sphere
			// with x = radius of sphere
			// with y = distance between ray position and sphere centre
			// with z = the distance we've travelled along the ray
			// if x^2 + z^2 - y^2 < 0, we do not intersect
			float dist = sphereRadiusSquared + distanceAlongRay * distanceAlongRay - differenceLengthSquared;

			if (dist < 0.0)
			{
				hitPoint = Vector3.zero;
				return false;
			}

			// get the intersection point
			float rayLength = distanceAlongRay - Mathf.Sqrt(dist);
			hitPoint = rayOrigin + rayDir * rayLength;
			return true;
		}

		/// <summary>
		/// return the first intersection point if the ray intersects the sphere,
		/// <br/>return null if it doesn't intersect or if the ray origin is inside the sphere
		/// <br/>rayDir must be normalized
		/// </summary>
		public static bool RaySphereIntersectionFloat(Vector3 rayOrigin, Vector3 rayDir, Vector3 sphereCenter, float sphereRadius, out Vector3 hitPoint, out float differenceLengthSquared)
		{
			// vector from ray origin to sphere center
			Vector3 difference = sphereCenter - rayOrigin;

			differenceLengthSquared = difference.sqrMagnitude;
			float sphereRadiusSquared = sphereRadius * sphereRadius;

			// If the distance between the ray start and the sphere's centre is less than
			// the radius of the sphere, we are inside the sphere.
			if (differenceLengthSquared < sphereRadiusSquared)
			{
				hitPoint = Vector3.zero;
				return false;
			}

			float distanceAlongRay = Vector3.Dot(rayDir, difference);
			// If the ray is pointing away from the sphere then we don't ever intersect
			if (distanceAlongRay < 0)
			{
				hitPoint = Vector3.zero;
				return false;
			}

			// Next check if we are within the bounds of the sphere
			// with x = radius of sphere
			// with y = distance between ray position and sphere centre
			// with z = the distance we've travelled along the ray
			// if x^2 + z^2 - y^2 < 0, we do not intersect
			float dist = sphereRadiusSquared + distanceAlongRay * distanceAlongRay - differenceLengthSquared;

			if (dist < 0.0)
			{
				hitPoint = Vector3.zero;
				return false;
			}

			// get the intersection point
			float rayLength = distanceAlongRay - Mathf.Sqrt(dist);
			hitPoint = rayOrigin + rayDir * rayLength;
			return true;
		}

		/// <summary> Check if a point is inside a cylinder defined by two points and a radius </summary>
		public static bool PointInCylinder(Vector3 cylTop, Vector3 cylBottom, float cylRadius, Vector3 point)
		{
			Vector3 topToBottom = cylBottom - cylTop;
			return PointInCylinderFast(cylTop, topToBottom, topToBottom.sqrMagnitude, cylRadius * cylRadius, point);
		}

		/// <summary> Check if a point is inside a cylinder, batch checking optimized version </summary>
		public static bool PointInCylinderFast(Vector3 cylTop, Vector3 topToBottom, float lengthSqr, float radiusSqr, Vector3 point)
		{
			Vector3 topToPoint = point - cylTop;

			// Dot the vectors to see if point lies behind the cylinder cap
			float dot = Vector3.Dot(topToPoint, topToBottom);

			// If dot is less than zero the point is behind the cylTop cap.
			// If greater than the cylinder axis line segment length squared
			// then the point is outside the other end cap at bottom.
			if (dot < 0f || dot > lengthSqr)
				return false;

			// Point lies within the parallel caps, so find
			// distance squared from point to cylinder axis, using the fact that sin^2 + cos^2 = 1
			float distanceSqr = Vector3.Dot(topToPoint, topToPoint) - dot * dot / lengthSqr;
			return radiusSqr >= distanceSqr;
		}

		/// <summary> Check if a point is inside a sphere, batch checking optimized version </summary>
		public static bool PointInSphere(Vector3 point, Vector3 sphereCenter, float sphereRadius)
		{
			return (point - sphereCenter).sqrMagnitude <= sphereRadius * sphereRadius;
		}

		/// <summary> Check if a point is inside a sphere, batch checking optimized version </summary>
		public static bool PointInSphereFast(Vector3 point, Vector3 sphereCenter, float sphereRadiusSqr)
		{
			return (point - sphereCenter).sqrMagnitude <= sphereRadiusSqr;
		}

		/// <summary> shortest distance from a ray to a point. rayDir must be normalized</summary>
		public static float RayPointDistance(Vector3d rayOrigin, Vector3d rayDir, Vector3d point)
		{
			return Vector3.Cross(rayDir, point - rayOrigin).magnitude;
		}

		/// <summary> shortest distance from a ray to a point, squared. rayDir must be normalized</summary>
		public static float RayPointDistanceSquared(Vector3d rayOrigin, Vector3d rayDir, Vector3d point)
		{
			return Vector3.Cross(rayDir, point - rayOrigin).sqrMagnitude;
		}


		#endregion

		#region RANDOM
		// store the random number generator
		static System.Random rng = new System.Random();

		///<summary>return random [MinValue,MaxValue] integer</summary>
		public static int RandomInt()
		{
			return rng.Next(int.MinValue, int.MaxValue);
		}

		///<summary>return random positive integer</summary>
		public static int RandomInt(int max_value)
		{
			return rng.Next(max_value);
		}

		///<summary>return random float [0..1]</summary>
		public static float RandomFloat()
		{
			return (float)rng.NextDouble();
		}

		///<summary>return random float [min, max]</summary>
		public static float RandomFloat(float min, float max)
		{
			return (max - min) * RandomFloat() / 1f + min;
		}

		///<summary>return random double [0..1]</summary>
		public static double RandomDouble()
		{
			return rng.NextDouble();
		}

		///<summary>return random double [min, max]</summary>
		public static double RandomDouble(double min, double max)
		{
			return (max - min) * RandomDouble() / 1.0 + min;
		}

		///<summary> return a random but deterministic double in range [min, max] for the given string seed </summary>
		public static double RandomDeterministic(string seed, double min, double max)
		{
			double k = (double)Hash32(seed) / uint.MaxValue;
			return min + k * (max - min);
		}

		static int fast_float_seed = 1;
		/// <summary>
		/// return random float in [-1,+1] range
		/// - it is less random than the c# RNG, but is way faster
		/// - the seed is meant to overflow! (turn off arithmetic overflow/underflow exceptions)
		/// </summary>
		public static float FastRandomFloat()
		{
			fast_float_seed *= 16807;
			return fast_float_seed * 4.6566129e-010f;
		}
		#endregion

		#region HASH
		///<summary>combine two guid, irregardless of their order (eg: Combine(a,b) == Combine(b,a))</summary>
		public static Guid CombineGuid(Guid a, Guid b)
		{
			byte[] a_buf = a.ToByteArray();
			byte[] b_buf = b.ToByteArray();
			byte[] c_buf = new byte[16];
			for (int i = 0; i < 16; ++i) c_buf[i] = (byte)(a_buf[i] ^ b_buf[i]);
			return new Guid(c_buf);
		}

		///<summary>combine two guid, in a non-commutative way</summary>
		public static Guid OrderedCombineGuid(Guid a, Guid b)
		{
			byte[] a_buf = a.ToByteArray();
			byte[] b_buf = b.ToByteArray();
			byte[] c_buf = new byte[16];
			for (int i = 0; i < 16; ++i) c_buf[i] = (byte)(a_buf[i] & ~b_buf[i]);
			return new Guid(c_buf);
		}

		///<summary>get 32bit FNV-1a hash of a string</summary>
		public static uint Hash32(string s)
		{
			// offset basis
			uint h = 2166136261u;

			// for each byte of the buffer
			for (int i = 0; i < s.Length; ++i)
			{
				// xor the bottom with the current octet
				h ^= s[i];

				// equivalent to h *= 16777619 (FNV magic prime mod 2^32)
				h += (h << 1) + (h << 4) + (h << 7) + (h << 8) + (h << 24);
			}

			//return the hash
			return h;
		}

		//
		public static int GetStableHashCode(this string str)
		{
			unchecked
			{
				int hash1 = 5381;
				int hash2 = hash1;

				for (int i = 0; i < str.Length && str[i] != '\0'; i += 2)
				{
					hash1 = ((hash1 << 5) + hash1) ^ str[i];
					if (i == str.Length - 1 || str[i + 1] == '\0')
						break;
					hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
				}

				return hash1 + (hash2 * 1566083941);
			}
		}
		#endregion

		#region TIME

		///<summary>
		///Real amount of hours in a solar day. <br/>
		///This can be a non-integer amount if the home body rotation period isn't a multiple of an hour
		///</summary>
		public static double HoursInDayExact { get; private set; } = 6.0;
		public static double SecondsInDayExact { get; private set; } = 6.0 * 3600.0;

		///<summary>
		///Real amount of solar days in a solar year.<br/>
		///This can be a fractional amount if the home body orbit period isn't a multiple of a solar day
		///</summary>
		public static double DaysInYearExact { get; private set; } = 426.0;
		public static double SecondsInYearExact { get; private set; } = 426.0 * 6.0 * 3600.0;

		///<summary>
		///Floored integer amount of hours in a solar day.<br/>
		///This may be lower than the real value if the home body rotation period isn't a multiple of an hour
		///</summary>
		public static double HoursInDayFloored { get; private set; } = 6.0;
		public static double SecondsInDayFloored { get; private set; } = 6.0 * 3600.0;

		///<summary>
		///Floored integer amount of solar days in a solar year.<br/>
		///This may be lower than the real value if the home body orbit period isn't a multiple of a solar day
		///</summary>
		public static double DaysInYearFloored { get; private set; } = 426.0;
		public static double SecondsInYearFloored { get; private set; }= 426.0 * 6.0 * 3600.0;

		///<summary>ulong (floored) amount of hours in a solar day (utility to avoid a cast every time we need it in Lib) </summary>
		public static ulong HoursInDayLong { get; private set; } = 6;
		public static ulong SecondsInDayLong { get; private set; } = 6 * 3600;

		///<summary>ulong (floored) amount of solar days in a solar year (utility to avoid a cast every time we need it in Lib)</summary>
		public static ulong DaysInYearLong { get; private set; } = 426;
		public static ulong SecondsInYearLong { get; private set; } = 429 * 6 * 3600;

		///<summary>
		/// Setup hours in day and days in year values by parsing kopernicus configs if present, or using the kerbin time setting otherwise.
		/// Note that this should be called before anything using time is called : before we parse configs and before part prefabs compilation.
		///</summary>
		public static string SetupCalendar()
		{
			string info;

			if (!Settings.UseHomeBodyCalendar || !Kopernicus.GetHomeWorldCalendar(out double hoursInDay, out ulong hoursInDayLong, out double daysInYear, out ulong daysInYearLong, out info))
			{
				hoursInDay = GameSettings.KERBIN_TIME ? 6.0 : 24.0;
				daysInYear = GameSettings.KERBIN_TIME ? 426.0 : 365.0;
				HoursInDayLong = (ulong)hoursInDay;
				DaysInYearLong = (ulong)daysInYear;
				info = GameSettings.KERBIN_TIME ? "Using Kerbin time from settings" : "Using Earth time from settings";
			}
			else
			{
				HoursInDayLong = hoursInDayLong;
				DaysInYearLong = daysInYearLong;
				info = "Using Kopernicus body time for " + info;
			}

			HoursInDayExact = hoursInDay;
			DaysInYearExact = daysInYear;
			SecondsInDayExact = hoursInDay * 3600.0;
			SecondsInYearExact = daysInYear * hoursInDay * 3600.0;
			HoursInDayFloored = HoursInDayLong;
			DaysInYearFloored = DaysInYearLong;
			SecondsInDayLong = HoursInDayLong * 3600;
			SecondsInYearLong = DaysInYearLong * HoursInDayLong * 3600;
			SecondsInDayFloored = SecondsInDayLong;
			SecondsInYearFloored = SecondsInYearLong;
			return info;
		}


		///<summary>stop time warping</summary>
		public static void StopWarp(double maxSpeed = 0, bool onlyIfHighMode = true)
		{
			if (onlyIfHighMode && TimeWarp.WarpMode == TimeWarp.Modes.LOW)
				return;

			var warp = TimeWarp.fetch;
			warp.CancelAutoWarp();
			int maxRate = 0;
			for (int i = 0; i < warp.warpRates.Length; ++i)
			{
				if (warp.warpRates[i] < maxSpeed)
					maxRate = i;
			}
			TimeWarp.SetRate(maxRate, true, false);
		}

		///<summary>disable time warping above a specified level</summary>
		public static void DisableWarp(uint max_level)
		{
			for (uint i = max_level + 1u; i < 8; ++i)
			{
				TimeWarp.fetch.warpRates[i] = TimeWarp.fetch.warpRates[max_level];
			}
		}

		///<summary>get current time</summary>
		public static UInt64 Clocks()
		{
			return (UInt64)Stopwatch.GetTimestamp();
		}

		///<summary>convert from clocks to microseconds</summary>
		public static double Microseconds(UInt64 clocks)
		{
			return clocks * 1000000.0 / Stopwatch.Frequency;
		}


		public static double Milliseconds(UInt64 clocks)
		{
			return clocks * 1000.0 / Stopwatch.Frequency;
		}


		public static double Seconds(UInt64 clocks)
		{
			return clocks / (double)Stopwatch.Frequency;
		}

		///<summary>return human-readable timestamp of planetarium time</summary>
		public static string PlanetariumTimestamp()
		{
			double t = Planetarium.GetUniversalTime();
			const double len_min = 60.0;
			const double len_hour = len_min * 60.0;
			double len_day = len_hour * Lib.HoursInDayFloored;
			double len_year = len_day * Lib.DaysInYearFloored;

			double year = Math.Floor(t / len_year);
			t -= year * len_year;
			double day = Math.Floor(t / len_day);
			t -= day * len_day;
			double hour = Math.Floor(t / len_hour);
			t -= hour * len_hour;
			double min = Math.Floor(t / len_min);

			return BuildString
			(
			  "[",
			  ((uint)year + 1).ToString("D4"),
			  "/",
			  ((uint)day + 1).ToString("D2"),
			  " ",
			  ((uint)hour).ToString("D2"),
			  ":",
			  ((uint)min).ToString("D2"),
			  "]"
			);
		}

		///<summary>return true half the time</summary>
		public static int Alternate(int seconds, int elements)
		{
			return ((int)Time.realtimeSinceStartup / seconds) % elements;
		}
		#endregion

		#region REFLECTION
		private static readonly BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

		///<summary>
		/// return a value from a module using reflection
		/// note: useful when the module is from another assembly, unknown at build time
		/// note: useful when the value isn't persistent
		/// note: this function break hard when external API change, by design
		/// </summary>
		public static T ReflectionValue<T>(PartModule m, string value_name)
		{
			return (T)m.GetType().GetField(value_name, flags).GetValue(m);
		}

		public static T? SafeReflectionValue<T>(PartModule m, string value_name) where T : struct
		{
			FieldInfo fi = m.GetType().GetField(value_name, flags);
			if (fi == null)
				return null;
			return (T)fi.GetValue(m);
		}

		///<summary>
		/// set a value from a module using reflection
		/// note: useful when the module is from another assembly, unknown at build time
		/// note: useful when the value isn't persistent
		/// note: this function break hard when external API change, by design
		///</summary>
		public static void ReflectionValue<T>(PartModule m, string value_name, T value)
		{
			m.GetType().GetField(value_name, flags).SetValue(m, value);
		}

		///<summary> Sets the value of a private field via reflection </summary>
		public static void ReflectionValue<T>(object instance, string value_name, T value)
		{
			instance.GetType().GetField(value_name, flags).SetValue(instance, value);
		}

		///<summary> Returns the value of a private field via reflection </summary>
		public static T ReflectionValue<T>(object instance, string field_name)
		{
			return (T)instance.GetType().GetField(field_name, flags).GetValue(instance);
		}

		public static void ReflectionCall(object m, string call_name)
		{
			m.GetType().GetMethod(call_name, flags).Invoke(m, null);
		}

		public static T ReflectionCall<T>(object m, string call_name)
		{
			return (T)(m.GetType().GetMethod(call_name, flags).Invoke(m, null));
		}

		public static void ReflectionCall(object m, string call_name, Type[] types, object[] parameters)
		{
			m.GetType().GetMethod(call_name, flags, null, types, null).Invoke(m, parameters);
		}

		public static T ReflectionCall<T>(object m, string call_name, Type[] types, object[] parameters)
		{
			return (T)(m.GetType().GetMethod(call_name, flags, null, types, null).Invoke(m, parameters));
		}

		#endregion

		#region STRING
		/// <summary> return string limited to len, with ... at the end</summary>
		public static string Ellipsis(string s, uint len)
		{
			len = Math.Max(len, 3u);
			return s.Length <= len ? s : Lib.BuildString(s.Substring(0, (int)len - 3), "...");
		}

		/// <summary> return string limited to len, with ... in the middle</summary>
		public static string EllipsisMiddle(string s, int len)
		{
			if (s.Length > len)
			{
				len = (len - 3) / 2;
				return Lib.BuildString(s.Substring(0, len), "...", s.Substring(s.Length - len));
			}
			return s;
		}

		///<summary>tokenize a string</summary>
		public static List<string> Tokenize(string txt, char separator)
		{
			List<string> ret = new List<string>();
			string[] strings = txt.Split(separator);
			foreach (string s in strings)
			{
				string trimmed = s.Trim();
				if (trimmed.Length > 0) ret.Add(trimmed);
			}
			return ret;
		}

		///<summary>
		/// return message with the macro expanded
		///- variant: tokenize the string by '|' and select one
		///</summary>
		public static string ExpandMsg(string txt, Vessel v = null, ProtoCrewMember c = null, uint variant = 0)
		{
			// get variant
			var variants = txt.Split('|');
			if (variants.Length > variant) txt = variants[variant];

			// macro expansion
			string v_name = v != null ? (v.isEVA ? "EVA" : v.vesselName) : "";
			string c_name = c != null ? c.name : "";
			return txt
			  .Replace("@", "\n")
			  .Replace("$VESSEL", BuildString("<b>", v_name, "</b>"))
			  .Replace("$KERBAL", "<b>" + c_name + "</b>")
			  .Replace("$ON_VESSEL", v != null && v.isActiveVessel ? "" : BuildString("On <b>", v_name, "</b>, "))
			  .Replace("$HIS_HER", c != null && c.gender == ProtoCrewMember.Gender.Male ? Local.Kerbal_his : Local.Kerbal_her);//"his""her"
		}

		///<summary>make the first letter uppercase</summary>
		public static string UppercaseFirst(string s)
		{
			return s.Length > 0 ? char.ToUpper(s[0]) + s.Substring(1).ToLower() : string.Empty;
		}

		///<summary>standardized kerbalism string colors</summary>
		[Obsolete("Use the Kolor class instead")]
		public enum Kolor
		{
			None,
			Green,
			Yellow,
			Orange,
			Red,
			PosRate,
			NegRate,
			Science,
			Cyan,
			LightGrey,
			DarkGrey
		}

		///<summary>return a colored "[V]" or "[X]" depending on the condition. Only work if placed at the begining of a line. To align other lines, use the "<pos=5em>" tag</summary>
		public static string Checkbox(bool condition)
		{
			return condition
				? " <color=#88FF00><mark=#88FF0033><mspace=1em><b><i>V </i></b></mspace></mark></color><pos=5em>"
				: " <color=#FF8000><mark=#FF800033><mspace=1em><b><i>X </i></b></mspace></mark></color><pos=5em>";
		}

		///<summary>return the hex representation for kerbalism Kolors</summary>
		[Obsolete("Use the Kolor class instead")]
		public static string KolorToHex(Kolor color)
		{
			switch (color)
			{
				case Kolor.None: return "#FFFFFF"; // use this in the Color() methods if no color tag is to be applied
				case Kolor.Green: return "#88FF00"; // green whith slightly less red than the ksp ui default (CCFF00), for better contrast with yellow
				case Kolor.Yellow: return "#FFD200"; // ksp ui yellow
				case Kolor.Orange: return "#FF8000"; // ksp ui orange
				case Kolor.Red: return "#FF3333"; // custom red
				case Kolor.PosRate: return "#88FF00"; // green
				case Kolor.NegRate: return "#FF8000"; // orange
				case Kolor.Science: return "#6DCFF6"; // ksp science color
				case Kolor.Cyan: return "#00FFFF"; // cyan
				case Kolor.LightGrey: return "#CCCCCC"; // light grey
				case Kolor.DarkGrey: return "#999999"; // dark grey	
				default: return "#FEFEFE";
			}
		}

		[Obsolete("Use the Kolor class instead")] public static Color KolorNone = new Color(1.000f, 1.000f, 1.000f);
		[Obsolete("Use the Kolor class instead")] public static Color KolorGreen = new Color(0.533f, 1.000f, 0.000f);
		[Obsolete("Use the Kolor class instead")] public static Color KolorYellow = new Color(1.000f, 0.824f, 0.000f);
		[Obsolete("Use the Kolor class instead")] public static Color KolorOrange = new Color(1.000f, 0.502f, 0.000f);
		[Obsolete("Use the Kolor class instead")] public static Color KolorRed = new Color(1.000f, 0.200f, 0.200f);
		[Obsolete("Use the Kolor class instead")] public static Color KolorPosRate = new Color(0.533f, 1.000f, 0.000f);
		[Obsolete("Use the Kolor class instead")] public static Color KolorNegRate = new Color(1.000f, 0.502f, 0.000f);
		[Obsolete("Use the Kolor class instead")] public static Color KolorScience = new Color(0.427f, 0.812f, 0.965f);
		[Obsolete("Use the Kolor class instead")] public static Color KolorCyan = new Color(0.000f, 1.000f, 1.000f);
		[Obsolete("Use the Kolor class instead")] public static Color KolorLightGrey = new Color(0.800f, 0.800f, 0.800f);
		[Obsolete("Use the Kolor class instead")] public static Color KolorDarkGrey = new Color(0.600f, 0.600f, 0.600f);

		///<summary>return the unity Colot  for kerbalism Kolors</summary>
		[Obsolete("Use the Kolor class instead")]
		public static Color KolorToColor(Kolor color)
		{
			switch (color)
			{
				case Kolor.None: return KolorNone;
				case Kolor.Green: return KolorGreen;
				case Kolor.Yellow: return KolorYellow;
				case Kolor.Orange: return KolorOrange;
				case Kolor.Red: return KolorRed;
				case Kolor.PosRate: return KolorPosRate;
				case Kolor.NegRate: return KolorNegRate;
				case Kolor.Science: return KolorScience;
				case Kolor.Cyan: return KolorCyan;
				case Kolor.LightGrey: return KolorLightGrey;
				case Kolor.DarkGrey: return KolorDarkGrey;
				default: return new Color(1.000f, 1.000f, 1.000f);
			}
		}

		///<summary>return string with the specified color and bold if stated</summary>
		[Obsolete("Use KsmString instead")]
		public static string Color(string s, Kolor color, bool bold = false)
		{
			return !bold ? BuildString("<color=", KolorToHex(color), ">", s, "</color>") : BuildString("<color=", KolorToHex(color), "><b>", s, "</b></color>");
		}

		///<summary>return string with different colors depending on the specified condition. "KColor.Default" will not apply any coloring</summary>
		[Obsolete("Use KsmString instead")]
		public static string Color(bool condition, string s, Kolor colorIfTrue, Kolor colorIfFalse = Kolor.None, bool bold = false)
		{
			return condition ? Color(s, colorIfTrue, bold) : colorIfFalse == Kolor.None ? bold ? Bold(s) : s : Color(s, colorIfFalse, bold);
		}

		///<summary>return different colored strings depending on the specified condition. "KColor.Default" will not apply any coloring</summary>
		[Obsolete("Use KsmString instead")]
		public static string Color(bool condition, string sIfTrue, Kolor colorIfTrue, string sIfFalse, Kolor colorIfFalse = Kolor.None, bool bold = false)
		{
			return condition ? Color(sIfTrue, colorIfTrue, bold) : colorIfFalse == Kolor.None ? bold ? Bold(sIfFalse) : sIfFalse : Color(sIfFalse, colorIfFalse, bold);
		}

		///<summary>return string in bold</summary>
		[Obsolete("Use KsmString instead")]
		public static string Bold(string s)
		{
			return BuildString("<b>", s, "</b>");
		}

		///<summary>return string in italic</summary>
		[Obsolete("Use KsmString instead")]
		public static string Italic(string s)
		{
			return BuildString("<i>", s, "</i>");
		}

		///<summary>add spaces on caps</summary>
		public static string SpacesOnCaps(string s)
		{
			return System.Text.RegularExpressions.Regex.Replace(s, "[A-Z]", " $0").TrimStart();
		}

		///<summary>convert to smart_case</summary>
		public static string SmartCase(string s)
		{
			return SpacesOnCaps(s).ToLower().Replace(' ', '_');
		}

		///<summary>converts_from_this to this</summary>
		public static string SpacesOnUnderscore(string s)
		{
			return s.Replace('_', ' ');
		}

		///<summary>select a string at random</summary>
		public static string TextVariant(params string[] list)
		{
			return list.Length == 0 ? string.Empty : list[RandomInt(list.Length)];
		}

		/// <summary> insert lines break to have a max line length of 'maxCharPerLine' characters </summary>
		public static string WordWrapAtLength(string longText, int maxCharPerLine)
		{

			longText = longText.Replace("\n", "");
			int currentPosition = 0;
			int textLength = longText.Length;
			while (true)
			{
				// if the remaining text is shorter that maxCharPerLine, return.
				if (currentPosition + maxCharPerLine >= textLength)
					break;

				// get position of first space before maxCharPerLine
				int nextSpacePosition = longText.LastIndexOf(' ', currentPosition + maxCharPerLine);

				// we found a space in the next line, replace it with a new line
				if (nextSpacePosition > currentPosition)
				{
					char[] longTextArray = longText.ToCharArray();
					longTextArray[nextSpacePosition] = '\n';
					longText = new string(longTextArray);
					currentPosition = nextSpacePosition;

				}
				// else break the word
				else
				{
					nextSpacePosition = currentPosition + maxCharPerLine;
					longText = longText.Insert(nextSpacePosition, "-\n");
					textLength += 2;
					currentPosition = nextSpacePosition + 2;
				}
			}
			return longText;

		}

		/// <summary> Remove all rtf/html tags </summary>
		public static string RemoveTags(string text)
		{
			return Regex.Replace(text, "<.*?>", string.Empty);
		}
		#endregion

		#region BUILD STRING

		/// <summary>
		/// Append "\n"<br/>
		/// StringBuilder.AppendLine() is platform-dependant and can use "\r\n" which cause trouble in KSP.
		/// </summary>
		[Obsolete("Use KsmString instead")]
		public static void AppendKSPNewLine(this StringBuilder sb)
		{
			sb.Append("\n");
		}

		/// <summary>
		/// Append "value" and add a "\n" to the end<br/>
		/// StringBuilder.AppendLine() is platform-dependant and can use "\r\n" which cause trouble in KSP.
		/// </summary>
		[Obsolete("Use KsmString instead")]
		public static void AppendKSPLine(this StringBuilder sb, string value)
		{
			sb.Append(value);
			sb.Append("\n");
		}

		/// <summary> Format to "label: <b>value</b>\n" (match the format of Specifics)</summary>
		[Obsolete("Use KsmString instead")]
		public static void AppendInfo(this StringBuilder sb, string label, string value, float valuePos = -1f, TextPos unit = TextPos.pixel)
		{
			sb.Append(label);

			if (valuePos >= 0f)
			{
				sb.Append("<pos=");
				sb.Append(valuePos.ToString(CultureInfo.InvariantCulture));
				switch (unit)
				{
					case TextPos.pixel: sb.Append("px"); break;
					case TextPos.percentage: sb.Append("%"); break;
					case TextPos.fontUnit: sb.Append("em"); break;
				}
				sb.Append(">");
				sb.Append("<b>");
				sb.Append(value);
				sb.Append("</b>\n");
			}
			else
			{
				sb.Append(": <b>");
				sb.Append(value);
				sb.Append("</b>\n");
			}
		}

		/// <summary> Append "??? value\n"</summary>
		[Obsolete("Use KsmString instead")]
		public static void AppendList(this StringBuilder sb, string value)
		{
			sb.Append("??? ");
			sb.Append(value);
			sb.Append("\n");
		}
		[Obsolete("Use KsmString instead")]
		public static void AppendBold(this StringBuilder sb, string value)
		{
			sb.Append("<b>");
			sb.Append(value);
			sb.Append("</b>");
		}
		[Obsolete("Use KsmString instead")]
		public static void AppendItalic(this StringBuilder sb, string value)
		{
			sb.Append("<i>");
			sb.Append(value);
			sb.Append("</i>");
		}

		/// <summary>
		/// Append the text, specifying the horizontal alignement
		/// </summary>
		/// <param name="value">text string</param>
		/// <param name="alignment">horizontal alignement</param>
		/// <param name="closed">should the tag be closed, preventing formatting to be applied to the next string(s)</param>
		[Obsolete("Use KsmString instead")]
		public static void AppendAlignement(this StringBuilder sb, string value, TextAlignment alignment, bool closed = true)
		{
			switch (alignment)
			{
				case TextAlignment.Left: sb.Append("<align=left>"); break;
				case TextAlignment.Center: sb.Append("<align=center>"); break;
				case TextAlignment.Right: sb.Append("<align=right>"); break;
			}
			sb.Append(value);
			if (closed)
			{
				sb.Append("</align>");
			}
		}

		public enum TextPos
		{
			pixel,
			percentage,
			fontUnit
		}

		/// <summary>
		/// Append with a pos=x tag to specify the horizontal position of the text. For best results, use left aligned text.
		/// </summary>
		/// <param name="value">text string</param>
		/// <param name="pos">horizontal position</param>
		/// <param name="unit">is position in pixels (default), % of the parent width, or in font units (em)</param>
		/// <param name="closed">should the tag be closed, preventing formatting to be applied to the next string(s)</param>
		[Obsolete("Use KsmString instead")]
		public static void AppendAtPos(this StringBuilder sb, string value, float pos, TextPos unit = TextPos.pixel, bool closed = false)
		{
			sb.Append("<pos=");
			sb.Append(pos.ToString(CultureInfo.InvariantCulture));
			switch (unit)
			{
				case TextPos.pixel: sb.Append("px"); break;
				case TextPos.percentage: sb.Append("%"); break;
				case TextPos.fontUnit: sb.Append("em"); break;
			}
			sb.Append(">");

			if (!string.IsNullOrEmpty(value))
			{
				sb.Append(value);
			}

			if (closed)
			{
				sb.Append("</pos>");
			}

		}

		[Obsolete("Use KsmString instead")]
		public static void AppendColor(this StringBuilder sb, string value, Kolor color, bool bold = false)
		{
			sb.Append("<color=");
			sb.Append(KolorToHex(color));
			sb.Append(">");
			if (bold)
			{
				sb.Append("<b>");
				sb.Append(value);
				sb.Append("</b>");
			}
			else
			{
				sb.Append(value);
			}
			sb.Append("</color>");
		}

		[Obsolete("Use KsmString instead")]
		public static void AppendCondition(this StringBuilder sb, bool condition, string whenTrue, string whenFalse)
		{
			if (condition)
			{
				sb.Append(whenTrue);
			}
			else
			{
				sb.Append(whenFalse);
			}
		}

		[Obsolete("Use KsmString instead")]
		public static void AppendColor(this StringBuilder sb, bool condition, string sIfTrue, Kolor colorIfTrue, string sIfFalse, Kolor colorIfFalse = Kolor.None, bool bold = false)
		{
			sb.Append("<color=");
			if (condition)
			{
				sb.Append(KolorToHex(colorIfTrue));
				sb.Append(">");
				if (bold)
				{
					sb.Append("<b>");
					sb.Append(sIfTrue);
					sb.Append("</b>");
				}
				else
				{
					sb.Append(sIfTrue);
				}
			}
			else
			{
				sb.Append(KolorToHex(colorIfFalse));
				sb.Append(">");
				if (bold)
				{
					sb.Append("<b>");
					sb.Append(sIfFalse);
					sb.Append("</b>");
				}
				else
				{
					sb.Append(sIfFalse);
				}
			}

			sb.Append("</color>");
		}

		// public static string Color(bool condition, string s, Kolor colorIfTrue, Kolor colorIfFalse = Kolor.None, bool bold = false)
		[Obsolete("Use KsmString instead")]
		public static void AppendColor(this StringBuilder sb, bool condition, string value, Kolor colorIfTrue, Kolor colorIfFalse = Kolor.None, bool bold = false)
		{
			sb.Append("<color=");
			if (condition)
			{
				sb.Append(KolorToHex(colorIfTrue));
			}
			else
			{
				sb.Append(KolorToHex(colorIfFalse));
			}

			sb.Append(">");
			if (bold)
			{
				sb.Append("<b>");
				sb.Append(value);
				sb.Append("</b>");
			}
			else
			{
				sb.Append(value);
			}
			sb.Append("</color>");
		}

		[Obsolete("Use KsmString instead")]
		public static void Append(this StringBuilder sb, string a, string b)
		{
			sb.Append(a);
			sb.Append(b);
		}
		[Obsolete("Use KsmString instead")]
		public static void Append(this StringBuilder sb, string a, string b, string c)
		{
			sb.Append(a);
			sb.Append(b);
			sb.Append(c);
		}
		[Obsolete("Use KsmString instead")]
		public static void Append(this StringBuilder sb, string a, string b, string c, string d)
		{
			sb.Append(a);
			sb.Append(b);
			sb.Append(c);
			sb.Append(d);
		}
		[Obsolete("Use KsmString instead")]
		public static void Append(this StringBuilder sb, string a, string b, string c, string d, string e)
		{
			sb.Append(a);
			sb.Append(b);
			sb.Append(c);
			sb.Append(d);
			sb.Append(e);
		}
		[Obsolete("Use KsmString instead")]
		public static void Append(this StringBuilder sb, string a, string b, string c, string d, string e, string f)
		{
			sb.Append(a);
			sb.Append(b);
			sb.Append(c);
			sb.Append(d);
			sb.Append(e);
			sb.Append(f);
		}
		[Obsolete("Use KsmString instead")]
		public static void Append(this StringBuilder sb, string a, string b, string c, string d, string e, string f, string g)
		{
			sb.Append(a);
			sb.Append(b);
			sb.Append(c);
			sb.Append(d);
			sb.Append(e);
			sb.Append(f);
			sb.Append(g);
		}
		[Obsolete("Use KsmString instead")]
		public static void Append(this StringBuilder sb, string a, string b, string c, string d, string e, string f, string g, string h)
		{
			sb.Append(a);
			sb.Append(b);
			sb.Append(c);
			sb.Append(d);
			sb.Append(e);
			sb.Append(f);
			sb.Append(g);
			sb.Append(h);
		}
		[Obsolete("Use KsmString instead")]
		public static void Append(this string value, StringBuilder sb)
		{
			sb.Append(value);
		}
		[Obsolete("Use KsmString instead")]
		public static void AppendLine(this string value, StringBuilder sb)
		{
			sb.Append(value);
			sb.Append("\n");
		}
		[Obsolete("Use KsmString instead")]
		public static void AppendList(this string value, StringBuilder sb)
		{
			sb.Append("??? ");
			sb.Append(value);
			sb.Append("\n");
		}
		[Obsolete("Use KsmString instead")]
		public static void AppendBold(this string value, StringBuilder sb)
		{
			sb.Append("<b>");
			sb.Append(value);
			sb.Append("</b>");
		}
		[Obsolete("Use KsmString instead")]
		public static void AppendItalic(this string value, StringBuilder sb)
		{
			sb.Append("<i>");
			sb.Append(value);
			sb.Append("</i>");
		}
		[Obsolete("Use KsmString instead")]
		public static void AppendAlignement(this string value, StringBuilder sb, TextAlignment alignment, bool closed = true)
		{
			switch (alignment)
			{
				case TextAlignment.Left: sb.Append("<align=left>"); break;
				case TextAlignment.Center: sb.Append("<align=center>"); break;
				case TextAlignment.Right: sb.Append("<align=right>"); break;
			}
			sb.Append(value);
			if (closed)
			{
				sb.Append("</align>");
			}
		}
		[Obsolete("Use KsmString instead")]
		public static void AppendAtPos(this string value, StringBuilder sb, float pos, TextPos unit = TextPos.pixel, bool closed = false)
		{
			sb.Append("<pos=");
			sb.Append(pos.ToString(CultureInfo.InvariantCulture));
			switch (unit)
			{
				case TextPos.pixel: sb.Append("px"); break;
				case TextPos.percentage: sb.Append("%"); break;
				case TextPos.fontUnit: sb.Append("em"); break;
			}
			sb.Append(">");
			sb.Append(value);
			if (closed)
			{
				sb.Append("</pos>");
			}
		}
		[Obsolete("Use KsmString instead")]
		public static void AppendColor(this string value, StringBuilder sb, Kolor color, bool bold = false)
		{
			sb.Append("<color=");
			sb.Append(KolorToHex(color));
			sb.Append(">");
			if (bold)
			{
				sb.Append("<b>");
				sb.Append(value);
				sb.Append("</b>");
			}
			else
			{
				sb.Append(value);
			}
			sb.Append("</color>");
		}

		// compose a set of strings together, without creating temporary objects
		// note: the objective here is to minimize number of temporary variables for GC
		// note: okay to call recursively, as long as all individual concatenation is atomic
		static StringBuilder sb = new StringBuilder(256);

		[Obsolete("Use KsmString instead")]
		public static string BuildString(string a, string b)
		{
			sb.Length = 0;
			sb.Append(a);
			sb.Append(b);
			return sb.ToString();
		}
		[Obsolete("Use KsmString instead")]
		public static string BuildString(string a, string b, string c)
		{
			sb.Length = 0;
			sb.Append(a);
			sb.Append(b);
			sb.Append(c);
			return sb.ToString();
		}
		[Obsolete("Use KsmString instead")]
		public static string BuildString(string a, string b, string c, string d)
		{
			sb.Length = 0;
			sb.Append(a);
			sb.Append(b);
			sb.Append(c);
			sb.Append(d);
			return sb.ToString();
		}
		[Obsolete("Use KsmString instead")]
		public static string BuildString(string a, string b, string c, string d, string e)
		{
			sb.Length = 0;
			sb.Append(a);
			sb.Append(b);
			sb.Append(c);
			sb.Append(d);
			sb.Append(e);
			return sb.ToString();
		}
		[Obsolete("Use KsmString instead")]
		public static string BuildString(string a, string b, string c, string d, string e, string f)
		{
			sb.Length = 0;
			sb.Append(a);
			sb.Append(b);
			sb.Append(c);
			sb.Append(d);
			sb.Append(e);
			sb.Append(f);
			return sb.ToString();
		}
		[Obsolete("Use KsmString instead")]
		public static string BuildString(string a, string b, string c, string d, string e, string f, string g)
		{
			sb.Length = 0;
			sb.Append(a);
			sb.Append(b);
			sb.Append(c);
			sb.Append(d);
			sb.Append(e);
			sb.Append(f);
			sb.Append(g);
			return sb.ToString();
		}
		[Obsolete("Use KsmString instead")]
		public static string BuildString(string a, string b, string c, string d, string e, string f, string g, string h)
		{
			sb.Length = 0;
			sb.Append(a);
			sb.Append(b);
			sb.Append(c);
			sb.Append(d);
			sb.Append(e);
			sb.Append(f);
			sb.Append(g);
			sb.Append(h);
			return sb.ToString();
		}
		[Obsolete("Use KsmString instead")]
		public static string BuildString(params string[] args)
		{
			sb.Length = 0;
			foreach (string s in args) sb.Append(s);
			return sb.ToString();
		}
		#endregion

		#region HUMAN READABLE

		public const string InlineSpriteScience = "<sprite=\"CurrencySpriteAsset\" name=\"Science\" color=#6DCFF6>";
		public const string InlineSpriteFunds = "<sprite=\"CurrencySpriteAsset\" name=\"Funds\" color=#B4D455>";
		public const string InlineSpriteReputation = "<sprite=\"CurrencySpriteAsset\" name=\"Reputation\" color=#E0D503>";
		public const string InlineSpriteFlask = "<sprite=\"CurrencySpriteAsset\" name=\"Flask\" color=#CE5DAE>";

		///<summary> Pretty-print a resource rate (rate is per second). Return an absolute value if a negative one is provided</summary>
		public static string HumanReadableRate(double rate, string format = "F3", string unit = "", bool showSign = false)
		{
			if (rate == 0.0)
				return Local.Generic_NONE;//"none"

			if (unit != "")
				unit = Lib.BuildString(" ", unit);

			string sign;
			if (showSign)
				sign = rate >= 0.0 ? "+" : "-";
			else
				sign = string.Empty;

			rate = Math.Abs(rate);

			if (Input.GetKey(KeyCode.LeftAlt))
			{
				int exponent = rate == 0.0 ? 0 : (int)Math.Floor(Math.Log10(rate));
				switch (exponent)
				{
					case 11: return BuildString(sign, (rate * 1e-9).ToString("0.0"), "e+9", unit, Local.Generic_perSecond);//"/s"
					case 10: return BuildString(sign, (rate * 1e-9).ToString("0.00"), "e+9", unit, Local.Generic_perSecond);//"/s"
					case 9: return BuildString(sign, (rate * 1e-9).ToString("0.000"), "e+9", unit, Local.Generic_perSecond);//"/s"
					case 8: return BuildString(sign, (rate * 1e-6).ToString("0.0"), "e+6", unit, Local.Generic_perSecond);//"/s"
					case 7: return BuildString(sign, (rate * 1e-6).ToString("0.00"), "e+6", unit, Local.Generic_perSecond);//"/s"
					case 6: return BuildString(sign, (rate * 1e-6).ToString("0.000"), "e+6", unit, Local.Generic_perSecond);//"/s"
					case 5: return BuildString(sign, (rate * 1e-3).ToString("0.0"), "e+3", unit, Local.Generic_perSecond);//"/s"
					case 4: return BuildString(sign, (rate * 1e-3).ToString("0.00"), "e+3", unit, Local.Generic_perSecond);//"/s"
					case 3: return BuildString(sign, (rate * 1e-3).ToString("0.000"), "e+3", unit, Local.Generic_perSecond);//"/s"
					case 2: return BuildString(sign, rate.ToString("0.0"), unit, Local.Generic_perSecond);//"/s"
					case 1: return BuildString(sign, rate.ToString("0.00"), unit, Local.Generic_perSecond);//"/s"
					case 0: return BuildString(sign, rate.ToString("0.000"), unit, Local.Generic_perSecond);//"/s"
					case -1: return BuildString(sign, (rate * 1e3).ToString("0.0"), "e-3", unit, Local.Generic_perSecond);//"/s"
					case -2: return BuildString(sign, (rate * 1e3).ToString("0.00"), "e-3", unit, Local.Generic_perSecond);//"/s"
					case -3: return BuildString(sign, (rate * 1e3).ToString("0.000"), "e-3", unit, Local.Generic_perSecond);//"/s"
					case -4: return BuildString(sign, (rate * 1e6).ToString("0.0"), "e-6", unit, Local.Generic_perSecond);//"/s"
					case -5: return BuildString(sign, (rate * 1e6).ToString("0.00"), "e-6", unit, Local.Generic_perSecond);//"/s"
					case -6: return BuildString(sign, (rate * 1e6).ToString("0.000"), "e-6", unit, Local.Generic_perSecond);//"/s"
					case -7: return BuildString(sign, (rate * 1e9).ToString("0.0"), "e-9", unit, Local.Generic_perSecond);//"/s"
					case -8: return BuildString(sign, (rate * 1e9).ToString("0.00"), "e-9", unit, Local.Generic_perSecond);//"/s"
					case -9: return BuildString(sign, (rate * 1e9).ToString("0.000"), "e-9", unit, Local.Generic_perSecond);//"/s"
					case -10: return BuildString(sign, (rate * 1e12).ToString("0.0"), "e-12", unit, Local.Generic_perSecond);//"/s"
					case -11: return BuildString(sign, (rate * 1e12).ToString("0.00"), "e-12", unit, Local.Generic_perSecond);//"/s"
					case -12: return BuildString(sign, (rate * 1e12).ToString("0.000"), "e-12", unit, Local.Generic_perSecond);//"/s"
					default: return BuildString(sign, rate.ToString("0.000e+0"), " ", unit, Local.Generic_perSecond);//"/s"
				}
			}

			if (rate >= 0.01)
				return BuildString(sign, rate.ToString(format), unit, Local.Generic_perSecond);//"/s"

			rate *= 60.0; // per-minute
			if (rate >= 0.01)
				return BuildString(sign, rate.ToString(format), unit, Local.Generic_perMinute);//"/m"

			rate *= 60.0; // per-hour
			if (rate >= 0.01)
				return BuildString(sign, rate.ToString(format), unit, Local.Generic_perHour);//"/h"

			rate *= HoursInDayExact;  // per-day
			if (rate >= 0.01)
				return BuildString(sign, rate.ToString(format), unit, Local.Generic_perDay);//"/d"

			return BuildString(sign, (rate * DaysInYearExact).ToString(format), unit, Local.Generic_perYear);//"/y"
		}

		///<summary> Pretty-print a duration (duration is in seconds, must be positive) </summary>
		public static string HumanReadableDuration(double d, bool fullprecison = false)
		{
			if (!fullprecison)
			{
				if (double.IsInfinity(d) || double.IsNaN(d)) return Local.Generic_PERPETUAL;//"perpetual"
				d = Math.Round(d);
				if (d <= 0.0) return Local.Generic_NONE;//"none"

				ulong durationLong = (ulong)d;

				// seconds
				if (d < 60.0)
				{
					ulong seconds = durationLong % 60ul;
					return BuildString(seconds.ToString(), "s");
				}
				// minutes + seconds
				if (d < 3600.0)
				{
					ulong seconds = durationLong % 60ul;
					ulong minutes = (durationLong / 60ul) % 60ul;
					return BuildString(minutes.ToString(), "m ", seconds.ToString("00"), "s");
				}
				// hours + minutes
				if (d < SecondsInDayFloored)
				{
					ulong minutes = (durationLong / 60ul) % 60ul;
					ulong hours = (durationLong / 3600ul) % HoursInDayLong;
					return BuildString(hours.ToString(), "h ", minutes.ToString("00"), "m");
				}
				ulong days = (durationLong / SecondsInDayLong) % DaysInYearLong;
				// days + hours
				if (d < SecondsInYearFloored)
				{
					ulong hours = (durationLong / 3600ul) % HoursInDayLong;
					return BuildString(days.ToString(), "d ", hours.ToString(), "h");
				}
				// years + days
				ulong years = durationLong / SecondsInYearLong;
				return BuildString(years.ToString(), "y ", days.ToString(), "d");
			}
			else
			{
				if (double.IsInfinity(d) || double.IsNaN(d)) return Local.Generic_NEVER;//"never"
				d = Math.Round(d);
				if (d <= 0.0) return Local.Generic_NONE;//"none"

				double hours_in_day = HoursInDayFloored;
				double days_in_year = DaysInYearFloored;

				long duration = (long)d;
				long seconds = duration % 60;
				duration /= 60;
				long minutes = duration % 60;
				duration /= 60;
				long hours = duration % (long)hours_in_day;
				duration /= (long)hours_in_day;
				long days = duration % (long)days_in_year;
				long years = duration / (long)days_in_year;

				string result = string.Empty;
				if (years > 0) result += years + "y ";
				if (years > 0 || days > 0) result += days + "d ";
				if (years > 0 || days > 0 || hours > 0) result += hours.ToString("D2") + ":";
				if (years > 0 || days > 0 || hours > 0 || minutes > 0) result += minutes.ToString("D2") + ":";
				result += seconds.ToString("D2");

				return result;
			}
		}

		public static string HumanReadableCountdown(double duration, bool compact = false)
		{
			return BuildString("T-", HumanReadableDuration(duration, !compact));
		}

		///<summary> Pretty-print a range (range is in meters) </summary>
		public static string HumanReadableDistance(double distance)
		{
			if (distance == 0.0) return Local.Generic_NONE;//"none"
			if (distance < 0.0) return Lib.BuildString("-", HumanReadableDistance(-distance));
			if (distance < 1000.0) return BuildString(distance.ToString("F1"), " m");
			distance /= 1000.0;
			if (distance < 1000.0) return BuildString(distance.ToString("F1"), " Km");
			distance /= 1000.0;
			if (distance < 1000.0) return BuildString(distance.ToString("F1"), " Mm");
			distance /= 1000.0;
			if (distance < 1000.0) return BuildString(distance.ToString("F1"), " Gm");
			distance /= 1000.0;
			if (distance < 1000.0) return BuildString(distance.ToString("F1"), " Tm");
			distance /= 1000.0;
			if (distance < 1000.0) return BuildString(distance.ToString("F1"), " Pm");
			distance /= 1000.0;
			return BuildString(distance.ToString("F1"), " Em");
		}

		///<summary> Pretty-print a speed (in meters/sec) </summary>
		public static string HumanReadableSpeed(double speed)
		{
			return Lib.BuildString(HumanReadableDistance(speed), "/s");
		}

		///<summary> Pretty-print temperature </summary>
		public static string HumanReadableTemp(double temp)
		{
			return BuildString(temp.ToString("F1"), " K");
		}

		///<summary> Pretty-print angle </summary>
		public static string HumanReadableAngle(double angle)
		{
			return BuildString(angle >= 0.0001 ? angle.ToString("F1") : "0", " ??");
		}

		///<summary> Pretty-print irrandiance (W/m??) </summary>
		public static string HumanReadableIrradiance(double irradiance)
		{
			return (irradiance >= 0.1 || irradiance == 0.0) ? irradiance.ToString("0.0 W/m??") : irradiance.ToString("0.0E+0 W/m??");
		}

		///<summary> Pretty-print thermal flux (kW)</summary>
		public static string HumanReadableThermalFlux(double flux)
		{
			return (flux <= -0.001 || flux >= 0.001 || flux == 0.0) ? flux.ToString("0.000 kWth") : flux.ToString("0.0E+0 kWth");
		}

		///<summary> Pretty-print a number using the "0.000" format, or using the "0.0E+0" scientific notation is below 0.001 or above -0.001 </summary>
		public static string HumanReadableSmallNumber(double number)
		{
			return (number <= -0.001 || number >= 0.001 || number == 0.0) ? number.ToString("0.000") : number.ToString("0.0E+0");
		}

		///<summary> Pretty-print magnetic strength </summary>
		public static string HumanReadableField(double strength)
		{
			return BuildString(strength.ToString("F1"), " ??T"); //< micro-tesla
		}

		///<summary> Pretty-print radiation rate </summary>
		private const string radPerHour = "rad/h";
		public static string HumanReadableRadiation(double rad, bool nominal = true, bool dangerColor = true)
		{
			if (nominal && rad <= Radiation.Nominal)
			{
				if (dangerColor)
					return Color(Local.Generic_NOMINAL, Kolor.Green);//"nominal"
				else
					return Local.Generic_NOMINAL;//"nominal"
			}

			rad *= 3600.0;

			if (Settings.RadiationInSievert)
			{
				rad /= 100.0;
				return BuildString((rad).ToString("F3"), " Sv/h");
			}

			if (rad < 0.00001)
			{
				if (dangerColor)
					return Color(BuildString((rad * 1000000.0).ToString("F3"), " ??rad/h"), Kolor.Green);
				else
					return BuildString((rad * 1000000.0).ToString("F3"), " ??rad/h");
			}
			else if (rad < 0.01)
			{
				if (dangerColor)
					return Color(BuildString((rad * 1000.0).ToString("F3"), " mrad/h"), Kolor.Green);
				else
					return BuildString((rad * 1000.0).ToString("F3"), " mrad/h");
			}

			if (dangerColor)
			{
				if (rad < 0.5)
					return Color(BuildString(rad.ToString("F3"), " rad/h"), Kolor.Yellow);
				else
					return Color(BuildString(rad.ToString("F3"), " rad/h"), Kolor.Red);
			}
			else
			{
				return BuildString(rad.ToString("F3"), " rad/h");
			}
		}

		///<summary> Pretty-print percentage </summary>
		public static string HumanReadablePerc(double v, string format = "F0")
		{
			return BuildString((v * 100.0).ToString(format), "%");
		}

		///<summary> Pretty-print pressure (value is in kPa) </summary>
		public static string HumanReadablePressure(double v)
		{
			return Lib.BuildString(v.ToString("F1"), " kPa");
		}

		///<summary> Pretty-print volume (value is in m^3) </summary>
		public static string HumanReadableVolume(double v)
		{
			return Lib.BuildString(v.ToString("F2"), " m??");
		}

		///<summary> Pretty-print surface (value is in m^2) </summary>
		public static string HumanReadableSurface(double v)
		{
			return Lib.BuildString(v.ToString("F2"), " m??");
		}

		///<summary> Pretty-print mass </summary>
		public static string HumanReadableMass(double v)
		{
			if (v <= double.Epsilon) return "0 kg";
			if (v > 1) return Lib.BuildString(v.ToString("F3"), " t");
			v *= 1000;
			if (v > 1) return Lib.BuildString(v.ToString("F2"), " kg");
			v *= 1000;
			return Lib.BuildString(v.ToString("F2"), " g");
		}

		///<summary> Pretty-print cost </summary>
		public static string HumanReadableCost(double v)
		{
			return Lib.BuildString(v.ToString("F0"), " $");
		}

		///<summary> Format an amount and capacity using kilo / mega abbreviations </summary>
		public static string HumanReadableStorage(double amount, double capacity)
		{
			if (capacity >= 1000000.0)
				return Lib.BuildString((amount / 1000000.0).ToString("0.00"), " / ", (capacity / 1000000.0).ToString("0.00 M"));
			else if (capacity >= 1000.0)
				return Lib.BuildString((amount / 1000.0).ToString("0.00"), " / ", (capacity / 1000.0).ToString("0.00 k"));
			else
				return Lib.BuildString(amount.ToString("0.0"), " / ", capacity.ToString("0.0"));
		}

		///<summary> Format a large (positive) number using kilo / mega abbreviations </summary>
		public static string HumanReadableAmountCompact(double value)
		{
			value = Math.Abs(value);
			if (value >= 1000000.0)
				return (value / 1000000.0).ToString("0.00M");
			else if (value >= 1000.0)
				return (value / 1000.0).ToString("0.00k");
			else
				return value.ToString("F1");
		}

		///<summary> Format a value to 2 decimal places, or return 'none' </summary>
		public static string HumanReadableAmount(double value, string append = "")
		{
			return (Math.Abs(value) <= double.Epsilon ? Local.Generic_NONE : BuildString(value.ToString("F2"), append));//"none"
		}

		///<summary> Format an integer value, or return 'none' </summary>
		public static string HumanReadableInteger(uint value, string append = "")
		{
			return (Math.Abs(value) <= 0 ? Local.Generic_NONE : BuildString(value.ToString("F0"), append));//"none"
		}
		// Note : config / code base unit for data rate / size is in megabyte (1000^2 bytes)
		// For UI purposes we use the decimal units (B/kB/MB...), not the binary (1024^2 bytes) units

		public const double bitsPerMB = 1000.0 * 1000.0 * 8.0;

		public const double BPerMB = 1000.0 * 1000.0;
		public const double kBPerMB = 1000.0;
		public const double GBPerMB = 1.0 / 1000.0;
		public const double TBPerMB = 1.0 / (1000.0 * 1000.0);

		public const double MBPerBTenth = 1.0 / (1000.0 * 1000.0 * 10.0);
		public const double MBPerkB = 1.0 / 1000.0;
		public const double MBPerGB = 1000.0;
		public const double MBPerTB = 1000.0 * 1000.0;

		///<summary> Format data size, the size parameter is in MB (megabytes) </summary>
		public static string HumanReadableDataSize(double size)
		{
			if (size < MBPerBTenth)  // min size is 0.1 byte
				return "0.0 B";
			if (size < MBPerkB)
				return (size * BPerMB).ToString("0.0 B");
			if (size < 1.0)
				return (size * kBPerMB).ToString("0.00 kB");
			if (size < MBPerGB)
				return size.ToString("0.00 MB");
			if (size < MBPerTB)
				return (size * GBPerMB).ToString("0.00 GB");

			return (size * TBPerMB).ToString("0.00 TB");
		}

		///<summary> Format data rate, the rate parameter is in MB/s </summary>
		public static string HumanReadableDataRate(double rate)
		{
			if (rate < MBPerBTenth) // min rate is 0.1 byte/s
				return "0.0 B/s";
			if (rate < MBPerkB)
				return (rate * BPerMB).ToString("0.0 B/s");
			if (rate < 1.0)
				return (rate * kBPerMB).ToString("0.00 kB/s");
			if (rate < MBPerGB)
				return rate.ToString("0.00 MB/s");
			if (rate < MBPerTB)
				return (rate * GBPerMB).ToString("0.00 GB/s");

			return (rate * TBPerMB).ToString("0.00 TB/s");
		}

		public static string HumanReadableSampleSize(double size)
		{
			return HumanReadableSampleSize(SampleSizeToSlots(size));
		}

		public static string HumanReadableSampleSize(int slots)
		{
			if (slots <= 0) return Lib.BuildString(Local.Generic_NO, Local.Generic_SLOT);//"no "

			return Lib.BuildString(slots.ToString(), " ", slots > 1 ? Local.Generic_SLOTS : Local.Generic_SLOT);
		}

		public static int SampleSizeToSlots(double size)
		{
			int result = (int)(size / 1024);
			if (result * 1024 < size) ++result;
			return result;
		}

		public static double SlotsToSampleSize(int slots)
		{
			return slots * 1024;
		}

		///<summary> Format science credits </summary>
		public static string HumanReadableScience(double value, bool compact = true, bool addSciSymbol = false)
		{
			if (compact)
			{
				if (addSciSymbol)
				{
					return Lib.Color(Lib.BuildString(value.ToString("F1"), InlineSpriteScience), Kolor.Science, true); 
				}
				return Lib.Color(value.ToString("F1"), Kolor.Science, true);
			}
			else
			{
				return Lib.Color(Lib.BuildString(value.ToString("F1"), " ", Local.SCIENCEARCHIVE_CREDITS), Kolor.Science);//CREDITS
			}
		}
		#endregion

		#region GAME LOGIC

		// note : HighLogic.LoadedSceneIsEditor / HighLogic.LoadedSceneIsFlight is sometimes
		// set a bit latter than HighLogic.LoadedScene. Example : from PartModule.Update()
		// in the editor, in case of an editor -> space center scene switch, for a single
		// Update(), HighLogic.LoadedSceneIsEditor can sometimes return true while checking for
		// HighLogic.LoadedScene will return SPACECENTER.

		///<summary>return true in spacecenter, flight, and tracking station scenes</summary>
		public static bool IsGameRunning
		{
			get
			{
				switch (HighLogic.LoadedScene)
				{
					case GameScenes.SPACECENTER:
					case GameScenes.FLIGHT:
					case GameScenes.TRACKSTATION:
						return true;
					default:
						return false;
				}
			}
		}

		///<summary>return true if the current scene is flight</summary>
		public static bool IsFlight => HighLogic.LoadedScene == GameScenes.FLIGHT;

		///<summary>return true if the current scene is editor</summary>
		public static bool IsEditor => HighLogic.LoadedScene == GameScenes.EDITOR;

		public static bool IsCareer => HighLogic.fetch.currentGame.Mode == Game.Modes.CAREER;

		///<summary>return true if the current scene is not the main menu</summary>
		public static bool IsGame()
		{
			
			return HighLogic.LoadedSceneIsGame;
		}

		///<summary>return true if game is paused</summary>
		public static bool IsPaused()
		{
			return FlightDriver.Pause || Planetarium.Pause;
		}

		///<summary>return true if a tutorial scenario or making history mission is active</summary>
		public static bool IsScenario()
		{
			return HighLogic.CurrentGame.Mode == Game.Modes.SCENARIO
				|| HighLogic.CurrentGame.Mode == Game.Modes.SCENARIO_NON_RESUMABLE
				|| HighLogic.CurrentGame.Mode == Game.Modes.MISSION_BUILDER
				|| HighLogic.CurrentGame.Mode == Game.Modes.MISSION;
		}

		///<summary>disable the module and return true if a tutorial scenario is active</summary>
		public static bool DisableScenario(PartModule m)
		{
			if (IsScenario())
			{
				m.enabled = false;
				m.isEnabled = false;
				return true;
			}
			return false;
		}

		///<summary>if current game is neither science or career, disable the module and return false</summary>
		public static bool ModuleEnableInScienceAndCareer(PartModule m)
		{
			switch (HighLogic.CurrentGame.Mode)
			{
				case Game.Modes.CAREER:
				case Game.Modes.SCIENCE_SANDBOX:
					return true;
				default:
					m.enabled = false;
					m.isEnabled = false;
					return false;
			}
		}
		#endregion

		#region BODY

		/// <summary>
		/// return the body localized display name (without the "^n" string)
		/// </summary>
		public static string LocalizedDisplayName(this CelestialBody body)
		{
			return body.displayName.LocalizeRemoveGender();
		}

		///<summary
		/// return selected body in tracking-view/map-view
		/// >if a vessel is selected, return its main body
		///</summary>
		public static CelestialBody MapViewSelectedBody()
		{
			var target = PlanetariumCamera.fetch.target;
			return
				target == null ? null : target.celestialBody ?? target.vessel?.mainBody;
		}

		/* this appears to be broken / working unreliably, use a raycast instead
		/// <summary
		/// return terrain height at point specified
		///- body terrain must be loaded for this to work: use it only for loaded vessels
		/// </summary>
		public static double TerrainHeight(CelestialBody body, Vector3d pos)
		{
			PQS pqs = body.pqsController;
			if (pqs == null) return 0.0;
			Vector2d latlong = body.GetLatitudeAndLongitude(pos);
			Vector3d radial = QuaternionD.AngleAxis(latlong.y, Vector3d.down) * QuaternionD.AngleAxis(latlong.x, Vector3d.forward) * Vector3d.right;
			return (pos - body.position).magnitude - pqs.GetSurfaceHeight(radial);
		}
		*/
		#endregion

		#region VESSEL
		///<summary>return true if landed somewhere</summary>
		public static bool Landed(Vessel v)
		{
			if (v.loaded) return v.Landed || v.Splashed;
			else return v.protoVessel.landed || v.protoVessel.splashed;
		}

		///<summary>return vessel position</summary>
		public static Vector3d VesselPosition(Vessel v)
		{
			// the issue
			//   - GetWorldPos3D() return mainBody position for a few ticks after scene changes
			//   - we can detect that, and fall back to evaluating position from the orbit
			//   - orbit is not valid if the vessel is landed, and for a tick on prelaunch/staging/decoupling
			//   - evaluating position from latitude/longitude work in all cases, but is probably the slowest method

			// get vessel position
			Vector3d pos = v.GetWorldPos3D();

			// during scene changes, it will return mainBody position
			if (Vector3d.SqrMagnitude(pos - v.mainBody.position) < 1.0)
			{
				// try to get it from orbit
				pos = v.orbit.getTruePositionAtUT(Planetarium.GetUniversalTime());

				// if the orbit is invalid (landed, or 1 tick after prelaunch/staging/decoupling)
				if (double.IsNaN(pos.x))
				{
					// get it from lat/long (work even if it isn't landed)
					pos = v.mainBody.GetWorldSurfacePosition(v.latitude, v.longitude, v.altitude);
				}
			}

			// victory
			return pos;
		}


		///<summary>return set of crew on a vessel. Works on loaded and unloaded vessels</summary>
		public static List<ProtoCrewMember> CrewList(Vessel v)
		{
			return v.loaded ? v.GetVesselCrew() : v.protoVessel.GetVesselCrew();
		}

		///<summary>return crew count of a vessel. Works on loaded and unloaded vessels</summary>
		public static int CrewCount(Vessel v)
		{
			return v.isEVA ? 1 : CrewList(v).Count;
		}

		///<summary>return crew count of a protovessel</summary>
		public static int CrewCount(ProtoVessel pv)
		{
			return pv.vesselType == VesselType.EVA ? 1 : pv.GetVesselCrew().Count();
		}

		

		///<summary>return crew capacity of a vessel</summary>
		public static int CrewCapacity(Vessel v)
		{
			if (v.isEVA) return 1;
			if (v.loaded)
			{
				return v.GetCrewCapacity();
			}
			else
			{
				int capacity = 0;
				foreach (ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
				{
					capacity += p.partInfo.partPrefab.CrewCapacity;
				}
				return capacity;
			}
		}

		///<summary> return true if the vessel is a kerbal eva, and is flagged as dead</summary>
		public static bool IsEVADead(Vessel v)
		{
			if (!v.isEVA) return false;
			return DB.GetOrCreateKerbalData(CrewList(v)[0]).isEvaDead;
		}


		///<summary>return true if this is a 'vessel'</summary>
		public static bool IsVessel(Vessel v)
		{
			// something weird is going on
			if (v == null) return false;

			// if the vessel is in DEAD status, we consider it invalid
			if (v.state == Vessel.State.DEAD) return false;

			// if the vessel is a debris, a flag or an asteroid, ignore it
			// - the user can change vessel type, in that case he is actually disabling this mod for the vessel
			//   the alternative is to scan the vessel for ModuleCommand, but that is slower, and rescue vessels have no module command
			// - flags have type set to 'station' for a single update, can still be detected as they have vesselID == 0
			switch (v.vesselType)
			{
				case VesselType.Debris:
				case VesselType.Flag:
				case VesselType.SpaceObject:
				case VesselType.Unknown:
				case VesselType.DeployedSciencePart:
					return false;
			}

			// [disabled] when going to eva (and possibly other occasions), for a single update the vessel is not properly set
			// this can be detected by vessel.distanceToSun being 0 (an impossibility otherwise)
			// in this case, just wait a tick for the data being set by the game engine
			// if (v.loaded && v.distanceToSun <= double.Epsilon)
			//	return false;

			//
			//if (!v.loaded && v.protoVessel == null)
			//	continue;

			// the vessel is valid
			return true;
		}



		public static bool IsControlUnit(Vessel v)
		{
			return Serenity.GetScienceCluster(v) != null;
		}

		// TODO : refactor the whole "is vessel controllable / linked" thing
		[Obsolete("Due for a refactor")]
		public static bool IsPowered(Vessel v, VesselResourceKSP ecRes)
		{
			var cluster = Serenity.GetScienceCluster(v);
			if (cluster != null)
				return cluster.IsPowered;

			return ecRes.Amount > 0.0; // nope nope nope
		}

		public static Guid VesselID(Vessel v)
		{
			return v.id;
		}

		public static Guid VesselID(ProtoVessel pv)
		{
			return pv.vesselID;
		}

		public static void TerminateVesselPopup(Vessel v)
		{
			if (v.DiscoveryInfo.Level != DiscoveryLevels.Owned)
			{
				PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
					new MultiOptionDialog("StopTrackingObject", Localizer.Format("#autoLOC_481619"), Localizer.Format("#autoLOC_5050047"), HighLogic.UISkin,
						new DialogGUIButton(Localizer.Format("#autoLOC_481620"), () => TerminateVessel(v)),
						new DialogGUIButton(Localizer.Format("#autoLOC_481621"), null, true)),
					persistAcrossScenes: false, HighLogic.UISkin);
			}
			else
			{
				PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
					new MultiOptionDialog("TerminateMission", Localizer.Format("#autoLOC_481625"), Localizer.Format("#autoLOC_5050048"), HighLogic.UISkin,
						new DialogGUIButton(Localizer.Format("#autoLOC_481626"), () => TerminateVessel(v)),
						new DialogGUIButton(Localizer.Format("#autoLOC_481627"), null, true)),
					persistAcrossScenes: false, HighLogic.UISkin);
			}
		}

		public static bool TerminateVessel(Vessel v)
		{
			if (v.loaded)
				return false;

			if (v.DiscoveryInfo.Level != DiscoveryLevels.Owned)
			{
				if (SpaceTracking.Instance != null)
				{
					MethodInfo buildVesselsListInfo;

					try
					{
						buildVesselsListInfo = AccessTools.Method(typeof(SpaceTracking), "buildVesselsList");
					}
					catch (Exception e)
					{
						Log($"Error terminating vessel from tracking station {e}");
						return false;
					}

					Debug.Log("Stopped Tracking " + v.GetDisplayName() + ".");
					SpaceTracking.StopTrackingObject(v);
					SpaceTracking.Instance.SetVessel(null, false);
					buildVesselsListInfo.Invoke(SpaceTracking.Instance, null);
				}
				else
				{
					Debug.Log("Stopped Tracking " + v.GetDisplayName() + ".");
					SpaceTracking.StopTrackingObject(v);
				}

			}
			else
			{
				if (SpaceTracking.Instance != null)
				{
					Dictionary<Vessel, MapObject> scaledTargets;
					MethodInfo buildVesselsListInfo;

					try
					{
						FieldInfo scaledTargetsInfo = AccessTools.Field(typeof(SpaceTracking), "scaledTargets");
						scaledTargets = (Dictionary<Vessel, MapObject>)scaledTargetsInfo.GetValue(SpaceTracking.Instance);
						buildVesselsListInfo = AccessTools.Method(typeof(SpaceTracking), "buildVesselsList");
					}
					catch (Exception e)
					{
						Log($"Error terminating vessel from tracking station {e}");
						return false;
					}

					GameEvents.onVesselTerminated.Fire(v.protoVessel);
					SpaceTracking.Instance.SetVessel(null, false);
					UnityEngine.Object.Destroy(scaledTargets[v].gameObject);
					scaledTargets.Remove(v);
					List<ProtoCrewMember> vesselCrew = v.GetVesselCrew();
					for (int i = 0; i < vesselCrew.Count; i++)
					{
						ProtoCrewMember protoCrewMember = vesselCrew[i];
						Debug.Log("Crewmember " + protoCrewMember.name + " is lost.");
						protoCrewMember.StartRespawnPeriod();
					}
					UnityEngine.Object.DestroyImmediate(v.gameObject);
					buildVesselsListInfo.Invoke(SpaceTracking.Instance, null);
				}
				else
				{
					GameEvents.onVesselTerminated.Fire(v.protoVessel);
					List<ProtoCrewMember> vesselCrew = v.GetVesselCrew();
					int count = vesselCrew.Count;
					for (int i = 0; i < count; i++)
					{
						ProtoCrewMember protoCrewMember = vesselCrew[i];
						UnityEngine.Debug.Log("Crewmember " + protoCrewMember.name + " is lost.");
						protoCrewMember.StartRespawnPeriod();
					}
					UnityEngine.Object.DestroyImmediate(v.gameObject);
				}

				GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
			}

			if (HighLogic.LoadedScene == GameScenes.SPACECENTER && KSCVesselMarkers.fetch != null)
			{
				KSCVesselMarkers.fetch.StartCoroutine(CallbackUtil.DelayedCallback(1, KSCVesselMarkers.fetch.RefreshMarkers));
			}

			return true;
		}

		public static void RecoverVesselPopup(Vessel v)
		{
			PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
				new MultiOptionDialog("Recover Vessel", Localizer.Format("#autoLOC_481635"), Localizer.Format("#autoLOC_5050049"), HighLogic.UISkin,
					new DialogGUIButton(Localizer.Format("#autoLOC_481636"), () => RecoverVessel(v)),
					new DialogGUIButton(Localizer.Format("#autoLOC_481637"), null, true)),
				persistAcrossScenes: false, HighLogic.UISkin);
		}

		public static bool CanRecoverVessel(Vessel v)
		{
			if (!v.IsRecoverable)
				return false;

			if (v.loaded && FlightGlobals.ClearToSave() == ClearToSaveStatus.NOT_WHILE_ON_A_LADDER)
				return false;

			return true;
		}

		public static bool RecoverVessel(Vessel v)
		{
			if (!v.IsRecoverable)
				return false;

			if (v.loaded)
			{
				ClearToSaveStatus clearToSaveStatus = FlightGlobals.ClearToSave();
				if (clearToSaveStatus == ClearToSaveStatus.CLEAR && HighLogic.CurrentGame.Parameters.Flight.CanLeaveToSpaceCenter)
				{
					GameEvents.OnVesselRecoveryRequested.Fire(v);
				}
				else if (clearToSaveStatus == ClearToSaveStatus.NOT_WHILE_ON_A_LADDER)
				{
					return false;
				}
			}
			else
			{
				if (HighLogic.LoadedScene == GameScenes.TRACKSTATION && SpaceTracking.Instance != null)
				{
					Dictionary<Vessel, MapObject> scaledTargets;
					MethodInfo buildVesselsListInfo;

					try
					{
						FieldInfo scaledTargetsInfo = AccessTools.Field(typeof(SpaceTracking), "scaledTargets");
						scaledTargets = (Dictionary<Vessel, MapObject>)scaledTargetsInfo.GetValue(SpaceTracking.Instance);
						buildVesselsListInfo = AccessTools.Method(typeof(SpaceTracking), "buildVesselsList");

					}
					catch (Exception e)
					{
						Log($"Error recovering vessel from tracking station {e}");
						return false;
					}

					SpaceTracking.Instance.SetVessel(null, false);
					GameEvents.onVesselRecovered.Fire(v.protoVessel, data1: false);
					UnityEngine.Object.Destroy(scaledTargets[v].gameObject);
					scaledTargets.Remove(v);
					UnityEngine.Object.DestroyImmediate(v.gameObject);
					buildVesselsListInfo.Invoke(SpaceTracking.Instance, null);
				}
				else
				{
					GameEvents.onVesselRecovered.Fire(v.protoVessel, data1: false);
					UnityEngine.Object.DestroyImmediate(v.gameObject);
				}

				GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);

				if (HighLogic.LoadedScene == GameScenes.SPACECENTER && KSCVesselMarkers.fetch != null)
				{
					KSCVesselMarkers.fetch.StartCoroutine(CallbackUtil.DelayedCallback(1, KSCVesselMarkers.fetch.RefreshMarkers));
				}
				return true;
			}

			return false;
		}

		#endregion

		#region PART
		///<summary>get list of parts recursively, useful from the editors</summary>
		public static List<Part> GetPartsRecursively(Part root)
		{
			List<Part> ret = new List<Part>
			{
				root
			};
			foreach (Part p in root.children)
			{
				ret.AddRange(GetPartsRecursively(p));
			}
			return ret;
		}

		///<summary>return the name (not the title) of a part</summary>
		public static string PartName(Part p)
		{
			return p.partInfo.name;
		}

		/// <summary>
		/// In the editor, remove the symmetry constraint for this part and its symmetric counterparts. 
		/// This method is available in stock (Part.RemoveFromSymmetry()) since 1.7.2, copied here for 1.4-1.6 compatibility
		/// </summary>
		public static void EditorClearSymmetry(Part part)
		{
			part.CleanSymmetryReferences();
			if (part.stackIcon != null)
			{
				part.stackIcon.RemoveIcon();
				part.stackIcon.CreateIcon();
				if (StageManager.Instance != null) StageManager.Instance.SortIcons(true);
			}
			EditorLogic.fetch.SetBackup();
		}

		/// <summary>
		/// return the nth module (zero based count) of the specified type on the part, or null if not found
		/// </summary>
		public static PartModule FindModule(this Part part, string moduleType, int modulePosition)
		{
			int positionCount = 0;
			for (int i = 0; i < part.Modules.Count; i++)
			{
				if (part.Modules[i].moduleName.Equals(moduleType))
				{
					if (positionCount == modulePosition)
					{
						return part.Modules[i];
					}
					positionCount++;
				}
			}
			return null;
		}

		#endregion

		#region CREW

		public static int CrewCount(Part part)
		{
			// outside of the editors, it is easy
			if (IsFlight)
				return part.protoModuleCrew.Count;

			// in the editor we need something more involved

			VesselCrewManifest manifest = CrewAssignmentDialog.Instance?.GetManifest();
			PartCrewManifest part_manifest = manifest?.PartManifests.Find(k => k.PartID == part.craftID);
			if (part_manifest != null)
			{
				int result = 0;
				foreach (var s in part_manifest.partCrew)
				{
					if (!string.IsNullOrEmpty(s)) result++;
				}
				return result;
			}

			return 0;
		}

		public static int CrewCount(ProtoPartSnapshot protoPart)
		{
			return protoPart.protoModuleCrew.Count;
		}

		///<summary>return true if a part is manned, even in the editor</summary>
		public static bool IsCrewed(Part p)
		{
			return CrewCount(p) > 0;
		}

		public static void EditorClearPartCrew(Part part)
		{
			PartCrewManifest partCrewManifest = ShipConstruction.ShipManifest.GetPartCrewManifest(part.craftID);

			if (partCrewManifest != null)
			{
				for (int i = 0; i < partCrewManifest.partCrew.Length; i++)
				{
					partCrewManifest.RemoveCrewFromSeat(i);
				}
			}

			CrewAssignmentDialog.Instance.RefreshCrewLists(ShipConstruction.ShipManifest, false, true);
		}


		// TODO : clean that, merge the two methods
		public static List<ProtoCrewMember> TryTransferCrewToPart(List<ProtoCrewMember> fromPartCrewMembers, Part fromPart, Part toPart, bool postTransferMessage = true)
		{
			List<ProtoCrewMember> crewLeft = new List<ProtoCrewMember>(fromPartCrewMembers.Count);
			ProtoCrewMember[] crewToTransfer = fromPartCrewMembers.ToArray();
			//CrewTransfer transfer = null;
			foreach (ProtoCrewMember crew in crewToTransfer)
			{
				if (!toPart.crewTransferAvailable || toPart.protoModuleCrew.Count >= toPart.CrewCapacity)
				{
					crewLeft.Add(crew);
					continue;
				}

				CrewTransfer.CrewTransferData transferData = new CrewTransfer.CrewTransferData()
				{
					crewMember = crew,
					sourcePart = fromPart,
					destPart = toPart,
					canTransfer = true
				};

				GameEventsHabitat.disableCrewTransferFailMessage = true; // avoid getting spammed for each transfer failure
				GameEvents.onCrewTransferSelected.Fire(transferData);
				if (!transferData.canTransfer)
				{
					crewLeft.Add(crew);
					continue;
				}

				fromPart.RemoveCrewmember(crew);
				toPart.AddCrewmember(crew);

				//if (postTransferMessage)
				// ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_111636", crew.name, toPart.partInfo.title), transfer.scMsgWarning);

				GameEvents.onCrewTransferred.Fire(new GameEvents.HostedFromToAction<ProtoCrewMember, Part>(crew, fromPart, toPart));
			}

			// (2) only rebuild the IVA / portraits if there was a transfer on the active vessel
			if (fromPart.vessel.isActiveVessel && crewLeft.Count < fromPartCrewMembers.Count)
			{
				FlightGlobals.ActiveVessel.DespawnCrew();
				//transfer.StartCoroutine(CallbackUtil.DelayedCallback(1, transfer.waitAndCompleteTransfer));
			}

			return crewLeft;
		}

		public static List<ProtoCrewMember> TryTransferCrewElsewhere(Part crewedPart, bool postTransferMessage = true)
		{
			List<ProtoCrewMember> crewLeft = crewedPart.protoModuleCrew;
			int initialCrewCount = crewLeft.Count;
			foreach (Part otherPart in crewedPart.vessel.Parts)
			{
				if (otherPart == crewedPart || !otherPart.crewTransferAvailable || otherPart.protoModuleCrew.Count >= otherPart.CrewCapacity)
					continue;

				crewLeft = TryTransferCrewToPart(crewLeft, crewedPart, otherPart, postTransferMessage);

				if (crewLeft.Count == 0)
					break;
			}

			if (initialCrewCount > crewLeft.Count)
			{
				Kerbalism.Fetch.StartCoroutine(CallbackUtil.DelayedCallback(1, FlightGlobals.ActiveVessel.SpawnCrew));
			}

			return crewLeft;
		}

		/// <summary>
		/// Rebuild the IVAs and and Kerbal portrait gallery. Called from the habitat module in conjunction
		/// with the InternalModel_SpawnCrew patch to make kerbals put/take off their helmets. <br/>
		/// Note : depreciated in favor of InternalModel/Kerbal patches
		/// </summary>
		public static void RefreshIVAAndPortraits()
		{
			// Prevent (redundant) calls to this from doing anything while the respawn coroutine hasn't run.
			if (isIVARefreshRequested)
				return;

			isIVARefreshRequested = true;
			FlightGlobals.ActiveVessel.DespawnCrew();

			// Note : these methods are normally callbacks added to GameEvents.onCrewTransferred,
			// but we don't want to call that here and it doesn't seem there is any other way than doing it manually
			KerbalPortraitGallery.Instance.StartReset(FlightGlobals.ActiveVessel);
			ReflectionValue<InternalSpaceOverlay>(KerbalPortraitGallery.Instance, "ivaOverlay")?.Dismiss();

			Kerbalism.Fetch.StartCoroutine(SpawnIVAAndPortraits());
		}

		private static bool isIVARefreshRequested = false;

		private static IEnumerator SpawnIVAAndPortraits()
		{
			// wait exactly one frame. More : doesn't work, less : doesn't work. don't ask, IDK.
			for (int i = 0; i < 1; i++)
				yield return null;

			isIVARefreshRequested = false;
			FlightGlobals.ActiveVessel.SpawnCrew();
		}

		public static void EnablePartIVA(Part part, bool enable)
		{
			if (IsFlight)
			{
				if (part.vessel.isActiveVessel)
				{
					if (enable)
					{
						Lib.LogDebug("Part '{0}', Spawning IVA.", Lib.LogLevel.Message, part.partInfo.title);
						part.SpawnIVA();
					}
					else
					{
						Lib.LogDebug("Part '{0}', Destroying IVA.", Lib.LogLevel.Message, part.partInfo.title);
						part.DespawnIVA();
					}
				}
			}
		}
		public static void EnablePartCrewTransfer(Part part, bool enable)
		{
			part.crewTransferAvailable = enable;

			if (Lib.IsEditor)
			{
				GameEvents.onEditorPartEvent.Fire(ConstructionEventType.PartTweaked, part);
				GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
			}
			else if (Lib.IsFlight)
			{
				GameEvents.onVesselWasModified.Fire(part.vessel);
			}

			part.CheckTransferDialog();
			MonoUtilities.RefreshContextWindows(part);
		}

		#endregion

		#region PART VOLUME/SURFACE 


		#endregion

		#region MODULE
		///<summary>
		/// return all modules implementing a specific type in a vessel
		/// note: modules having isEnabled = false are not returned
		/// </summary>
		public static List<T> FindModules<T>(Vessel v, Predicate<T> predicate = null) where T : class
		{
			List<T> ret = new List<T>();
			for (int i = 0; i < v.parts.Count; ++i)
			{
				Part p = v.parts[i];
				for (int j = 0; j < p.Modules.Count; ++j)
				{
					PartModule m = p.Modules[j];
					if (m.isEnabled)
						if (m is T t)
							if (predicate == null || predicate.Invoke(t))
								ret.Add(t);
				}
			}
			return ret;
		}

		public static bool HasPart(Vessel v, string part_name)
		{
			if (Cache.HasVesselObjectsCache(v, "has_part:" + part_name))
				return Cache.VesselObjectsCache<bool>(v, "has_part:" + part_name);

			bool ret = false;
			foreach (string name in Tokenize(part_name, ','))
			{
				if (v.loaded)
					ret = v.parts.Find(k => k.name.StartsWith(part_name, StringComparison.Ordinal)) != null;
				else
					ret = v.protoVessel.protoPartSnapshots.Find(k => k.partName.StartsWith(part_name, StringComparison.Ordinal)) != null;
				if (ret) break;
			}

			Cache.SetVesselObjectsCache(v, "has_part:" + part_name, ret);
			return ret;
		}

		/// <summary>
		/// return all proto modules with a specified name in a vessel.
		/// note: disabled modules are not returned
		/// </summary>
		public static List<ProtoPartModuleSnapshot> FindModules(ProtoVessel v, string module_name)
		{
			var ret = Cache.VesselObjectsCache<List<ProtoPartModuleSnapshot>>(v, "mod:" + module_name);
			if (ret != null)
				return ret;

			ret = new List<ProtoPartModuleSnapshot>(8);
			for (int i = 0; i < v.protoPartSnapshots.Count; ++i)
			{
				ProtoPartSnapshot p = v.protoPartSnapshots[i];
				ret.AddRange(FindModules(p, module_name));
			}

			Cache.SetVesselObjectsCache(v, "mod:" + module_name, ret);
			return ret;
		}

		///<summary>
		/// return all proto modules with a specified name in a part
		/// note: disabled modules are not returned
		/// </summary>
		public static List<ProtoPartModuleSnapshot> FindModules(ProtoPartSnapshot p, string module_name)
		{
			List<ProtoPartModuleSnapshot> ret = new List<ProtoPartModuleSnapshot>(8);
			for (int j = 0; j < p.modules.Count; ++j)
			{
				ProtoPartModuleSnapshot m = p.modules[j];
				if (m.moduleName == module_name && Proto.GetBool(m, "isEnabled"))
				{
					ret.Add(m);
				}
			}
			return ret;
		}

		///<summary>
		/// return true if a module implementing a specific type and satisfying the predicate specified exist in a vessel
		/// note: disabled modules are ignored
		///</summary>
		public static bool HasModule<T>(Vessel v, Predicate<T> filter) where T : class
		{
			for (int i = 0; i < v.parts.Count; ++i)
			{
				Part p = v.parts[i];
				for (int j = 0; j < p.Modules.Count; ++j)
				{
					PartModule m = p.Modules[j];
					if (m.isEnabled)
					{
						if (m is T t && filter(t))
							return true;
					}
				}
			}
			return false;
		}

		///<summary>
		/// return true if a proto module with the specified name and satisfying the predicate specified exist in a vessel
		///note: disabled modules are not returned
		///</summary>
		public static bool HasModule(ProtoVessel v, string module_name, Predicate<ProtoPartModuleSnapshot> filter)
		{
			for (int i = 0; i < v.protoPartSnapshots.Count; ++i)
			{
				ProtoPartSnapshot p = v.protoPartSnapshots[i];
				for (int j = 0; j < p.modules.Count; ++j)
				{
					ProtoPartModuleSnapshot m = p.modules[j];
					if (m.moduleName == module_name && Proto.GetBool(m, "isEnabled") && filter(m))
					{
						return true;
					}
				}
			}
			return false;
		}

		private static ProtoPartSnapshot lastProtoPartPrefabSearch;
		private static bool lastProtoPartPrefabSearchIsSync;


		/// <summary>
		/// Find the PartModule instance in the Part prefab corresponding to a ProtoPartModuleSnapshot
		/// </summary>
		/// <param name="protoModuleIndex">
		/// The index of the ProtoPartModuleSnapshot in the protoPart.
		/// If the prefab isn't synchronized due to a configs change, the parameter is updated to the module index in the prefab
		/// </param>
		public static bool TryFindModulePrefab(ProtoPartSnapshot protoPart, ref int protoModuleIndex, out PartModule prefab)
		{
			if (protoPart == lastProtoPartPrefabSearch)
			{
				if (lastProtoPartPrefabSearchIsSync)
				{
					prefab = protoPart.partPrefab.Modules[protoModuleIndex];
					return true;
				}
			}
			else
			{
				lastProtoPartPrefabSearch = protoPart;
				lastProtoPartPrefabSearchIsSync = ArePrefabModulesInSync(protoPart.partPrefab, protoPart.modules);
				if (lastProtoPartPrefabSearchIsSync)
				{
					prefab = protoPart.partPrefab.Modules[protoModuleIndex];
					return true;
				}
			}

			prefab = null;
			int protoIndexInType = 0;
			ProtoPartModuleSnapshot protoModule = protoPart.modules[protoModuleIndex];
			foreach (ProtoPartModuleSnapshot otherppms in protoPart.modules)
			{
				if (otherppms.moduleName == protoModule.moduleName)
				{
					if (otherppms == protoModule)
						break;

					protoIndexInType++;
				}
			}

			int prefabIndexInType = 0;
			string protoKsmModuleId = null;
			for (int i = 0; i < protoPart.partPrefab.Modules.Count; i++)
			{
				if (protoPart.partPrefab.Modules[i] is IMultipleKsmModule ksmModule)
				{
					if (protoKsmModuleId == null )
						protoKsmModuleId = protoModule.moduleValues.GetValue(ksmModule.ConfigValueIdName);

					if (!string.IsNullOrEmpty(protoKsmModuleId) && ksmModule.KsmModuleId == protoKsmModuleId)
					{
						prefab = protoPart.partPrefab.Modules[i];
						protoModuleIndex = i;
						break;
					}
				}

				if (protoPart.partPrefab.Modules[i].moduleName == protoModule.moduleName)
				{
					if (prefabIndexInType == protoIndexInType)
					{
						prefab = protoPart.partPrefab.Modules[i];
						protoModuleIndex = i;
						break;
					}

					prefabIndexInType++;
				}
			}

			if (prefab == null)
			{
				Log($"PartModule prefab not found for {protoModule.moduleName} on {protoPart.partPrefab.partName}, has the part configuration changed ?", Lib.LogLevel.Warning);
				return false;
			}

			return true;
		}

		// note : we don't check KsmPartModule ids here for performance reasons.
		// This mean that a config change where the only modification is a KsmPartModule derivative being replaced
		// by another of the same type won't be detected. The likeliness of that happening seems extremely low.
		private static bool ArePrefabModulesInSync(Part partPrefab, List<ProtoPartModuleSnapshot> protoPartModules)
		{
			if (partPrefab.Modules.Count != protoPartModules.Count)
				return false;

			for (int i = 0; i < protoPartModules.Count; i++)
			{
				if (partPrefab.Modules[i].moduleName != protoPartModules[i].moduleName)
				{
					return false;
				}
			}

			return true;
		}

		public static double GetBaseConverterEfficiencyBonus(BaseConverter module, VesselDataBase vessel)
		{
			int expLevel = -1;
			if (module.UseSpecialistBonus)
			{
				foreach (KerbalData kerbal in vessel.Crew)
				{
					if (kerbal.stockKerbal.HasEffect(module.ExperienceEffect))
					{
						expLevel = Math.Max(expLevel, kerbal.stockKerbal.experienceLevel);
					}

					if (expLevel == 4)
						break;
				}
			}

			if (expLevel < 0)
				return module.EfficiencyBonus;
			else
				return module.EfficiencyBonus * (module.SpecialistBonusBase + (module.SpecialistEfficiencyFactor * (expLevel + 1)));
		}

		#endregion

		#region RESOURCE

		/// <summary> density in t/m3 of a resource (KSP "density" is in ton/unit and doesn't account for the resource volume property) </summary>
		public static double VolumetricMassDensity(this PartResourceDefinition res)
		{
			return (res.density * 1000.0) / res.volume;
		}

		/// <summary> Returns the amount of a resource in a part </summary>
		public static double Amount(Part part, string resource_name, bool ignore_flow = false)
		{
			foreach (PartResource res in part.Resources)
			{
				if ((res.flowState || ignore_flow) && res.resourceName == resource_name) return res.amount;
			}
			return 0.0;
		}

		/// <summary> Returns the capacity of a resource in a part </summary>
		public static double Capacity(Part part, string resource_name, bool ignore_flow = false)
		{
			foreach (PartResource res in part.Resources)
			{
				if ((res.flowState || ignore_flow) && res.resourceName == resource_name) return res.maxAmount;
			}
			return 0.0;
		}

		/// <summary> Returns the level of a resource in a part </summary>
		public static double Level(Part part, string resource_name, bool ignore_flow = false)
		{
			foreach (PartResource res in part.Resources)
			{
				if ((res.flowState || ignore_flow) && res.resourceName == resource_name)
				{
					return res.maxAmount > 0.0 ? res.amount / res.maxAmount : 0.0;
				}
			}
			return 0.0;
		}

		/// <summary> Adds the specified resource amount and capacity to a part,
		/// the resource is created if it doesn't already exist </summary>
		///<summary>poached from https://github.com/blowfishpro/B9PartSwitch/blob/master/B9PartSwitch/Extensions/PartExtensions.cs
		public static PartResource AddResource(Part p, string res_name, double amount, double capacity)
		{
			var reslib = PartResourceLibrary.Instance.resourceDefinitions;
			// if the resource is not known, log a warning and do nothing
			if (!reslib.Contains(res_name))
			{
				Lib.Log(Lib.BuildString("error while adding ", res_name, ": the resource doesn't exist"), Lib.LogLevel.Error);
				return null;
			}

			var resourceDefinition = reslib[res_name];
			PartResource resource = p.Resources[resourceDefinition.name];
			if (resource == null)
			{
				resource = new PartResource(p);
				resource.SetInfo(resourceDefinition);
				resource.maxAmount = capacity;
				resource.amount = amount;
				resource.flowState = true;
				resource.isTweakable = resourceDefinition.isTweakable;
				resource.isVisible = resourceDefinition.isVisible;
				resource.hideFlow = false;
				p.Resources.dict.Add(resourceDefinition.name.GetHashCode(), resource);

				PartResource simulationResource = new PartResource(resource);
				simulationResource.simulationResource = true;
				p.SimulationResources?.dict.Add(resourceDefinition.name.GetHashCode(), simulationResource);

				// flow mode is a property that call some code using SimulationResource in its setter.
				// consequently it must be set after simulationResource is registered to avoid the following log error spam :
				// [PartSet]: Failed to add Resource XXXXX to Simulation PartSet:XX as corresponding Part XXXX SimulationResource was not found.
				resource.flowMode = PartResource.FlowMode.Both;

				GameEvents.onPartResourceListChange.Fire(p);
			}
			else
			{
				PartResource simulationResource = p.SimulationResources?[resourceDefinition.name];

				resource.maxAmount = capacity;
				resource.amount = amount;
				if (simulationResource != null)
				{
					simulationResource.maxAmount = capacity;
					simulationResource.amount = amount;
				}
			}

			return resource;
		}

		/// <summary> Removes the specified resource amount and capacity from a part,
		/// the resource is removed completely if the capacity reaches zero </summary>
		public static void RemoveResource(Part p, string res_name, double amount, double capacity)
		{
			// if the resource is not in the part, do nothing
			if (!p.Resources.Contains(res_name))
				return;

			// get the resource
			var res = p.Resources[res_name];

			// reduce amount and capacity
			res.amount -= amount;
			res.maxAmount -= capacity;

			// clamp amount to capacity just in case
			res.amount = Math.Min(res.amount, res.maxAmount);

			Lib.Log("remove resource " + res_name + "(-" + amount + "): maxAmount = " + res.maxAmount);

			// if the resource is empty
			if (res.maxAmount <= 0.005) //< deal with precision issues
			{
				var reslib = PartResourceLibrary.Instance.resourceDefinitions;
				var resourceDefinition = reslib[res_name];

				p.Resources.dict.Remove(resourceDefinition.name.GetHashCode());
				p.SimulationResources?.dict.Remove(resourceDefinition.name.GetHashCode());

				GameEvents.onPartResourceListChange.Fire(p);
			}
		}

		///<summary>note: the resource must exist</summary>
		public static void SetResourceCapacity( Part p, string res_name, double capacity )
		{
			Lib.Log("set resource capacity " + res_name + " to " + capacity);
			// if the resource is not in the part, log a warning and do nothing
			if (!p.Resources.Contains( res_name ))
			{
				Lib.Log( Lib.BuildString( "error while setting capacity for ", res_name, ": the resource is not in the part" ), Lib.LogLevel.Error);
				return;
			}

			// set capacity and clamp amount
			var res = p.Resources[res_name];
			res.maxAmount = capacity;
			res.amount = Math.Min( res.amount, capacity );
		}

		///<summary>note: the resource must exist</summary>
		public static void SetResource( Part p, string res_name, double amount, double capacity )
		{
			Lib.Log("set resource " + res_name + " to amount " + amount + " capacity " + capacity);
			// if the resource is not in the part, log a warning and do nothing
			if (!p.Resources.Contains( res_name ))
			{
				Lib.Log( Lib.BuildString( "error while setting capacity for ", res_name, ": the resource is not in the part" ), Lib.LogLevel.Error);
				return;
			}

			// set capacity and clamp amount
			var res = p.Resources[res_name];
			res.maxAmount = capacity;
			res.amount = Math.Min( amount, capacity );
		}

		/// <summary> Set flow of a resource in the specified part. Does nothing if the resource does not exist in the part </summary>
		public static void SetResourceFlow(Part p, string res_name, bool enable)
		{
			// if the resource is not in the part, do nothing
			if (p.Resources.Contains( res_name ))
			{
				// set flow state
				var res = p.Resources[res_name];
				res.flowState = enable;
			} else {
				Lib.LogDebugStack("Resource " + res_name + " not in part " + p.name);
			}
		}

		/// <summary> Fills a resource in the specified part to its capacity </summary>
		public static void FillResource(Part p, string res_name)
		{
			// if the resource is not in the part, do nothing
			if (p.Resources.Contains(res_name))
			{
				PartResource res = p.Resources[res_name];
				res.amount = res.maxAmount;
			}
			else {
				Lib.LogDebugStack("Resource " + res_name + " not in part " + p.name); }
		}

		/// <summary> Sets the amount of a resource in the specified part to zero </summary>
		public static void EmptyResource(Part p, string res_name)
		{
			// if the resource is not in the part, do nothing
			if (p.Resources.Contains(res_name))
				p.Resources[res_name].amount = 0.0;
			else {
				Lib.LogDebugStack("Resource " + res_name + " not in part " + p.name); }
		}

		/// <summary> Returns the definition of a resource, or null if it doesn't exist </summary>
		public static PartResourceDefinition GetDefinition( string name )
		{
			// shortcut to the resource library
			var reslib = PartResourceLibrary.Instance.resourceDefinitions;

			// return the resource definition, or null if it doesn't exist
			return reslib.Contains( name ) ? reslib[name] : null;
		}

		/// <summary> Returns name of propellant used on eva </summary>
		public static string EvaPropellantName()
		{
			// first, get the kerbal eva part prefab
			Part p = PartLoader.getPartInfoByName( "kerbalEVA" ).partPrefab;

			// then get the KerbalEVA module prefab
			KerbalEVA m = p.FindModuleImplementing<KerbalEVA>();

			// finally, return the propellant name
			return m.propellantResourceName;
		}


		/// <summary> Returns capacity of propellant on eva </summary>
		public static double EvaPropellantCapacity()
		{
			// first, get the kerbal eva part prefab
			Part p = PartLoader.getPartInfoByName( "kerbalEVA" ).partPrefab;

			// then get the first resource and return capacity
			return p.Resources.Count == 0 ? 0.0 : p.Resources[0].maxAmount;
		}
		#endregion

		#region SCIENCE DATA

		///<summary>return true if there is experiment data on the vessel</summary>
		[Obsolete("Unused, require refactoring")]
		public static bool HasData( Vessel v )
		{
			// stock science system
			if (!Features.Science)
			{
				// if vessel is loaded
				if (v.loaded)
				{
					// iterate over all science containers/experiments and return true if there is data
					return Lib.HasModule<IScienceDataContainer>( v, k => k.GetData().Length > 0 );
				}
				// if not loaded
				else
				{
					// iterate over all science containers/experiments proto modules and return true if there is data
					return Lib.HasModule( v.protoVessel, "ModuleScienceContainer", k => k.moduleValues.GetNodes( "ScienceData" ).Length > 0 )
						|| Lib.HasModule( v.protoVessel, "ModuleScienceExperiment", k => k.moduleValues.GetNodes( "ScienceData" ).Length > 0 );
				}
			}
			// our own science system
			else
			{
				//foreach (var drive in DriveHandler.GetAllDrives(v.GetVesselData(), true))
				//	if (drive.files.Count > 0) return true;
				return false;
			}
		}

		///<summary>remove one experiment at random from the vessel</summary>
		[Obsolete("Unused, require refactoring")]
		public static void RemoveData( Vessel v )
		{
			// stock science system
			if (!Features.Science)
			{
				// if vessel is loaded
				if (v.loaded)
				{
					// get all science containers/experiments with data
					List<IScienceDataContainer> modules = Lib.FindModules<IScienceDataContainer>( v ).FindAll( k => k.GetData().Length > 0 );

					// remove a data sample at random
					if (modules.Count > 0)
					{
						IScienceDataContainer container = modules[Lib.RandomInt( modules.Count )];
						ScienceData[] data = container.GetData();
						container.DumpData( data[Lib.RandomInt( data.Length )] );
					}
				}
				// if not loaded
				else
				{
					// get all science containers/experiments with data
					var modules = new List<ProtoPartModuleSnapshot>();
					modules.AddRange( Lib.FindModules( v.protoVessel, "ModuleScienceContainer" ).FindAll( k => k.moduleValues.GetNodes( "ScienceData" ).Length > 0 ) );
					modules.AddRange( Lib.FindModules( v.protoVessel, "ModuleScienceExperiment" ).FindAll( k => k.moduleValues.GetNodes( "ScienceData" ).Length > 0 ) );

					// remove a data sample at random
					if (modules.Count > 0)
					{
						ProtoPartModuleSnapshot container = modules[Lib.RandomInt( modules.Count )];
						ConfigNode[] data = container.moduleValues.GetNodes( "ScienceData" );
						container.moduleValues.RemoveNode( data[Lib.RandomInt( data.Length )] );
					}
				}
			}
			// our own science system
			else
			{
				// select a file at random and remove it
				//foreach (var drive in DriveHandler.GetAllDrives(v.GetVesselData(), true))
				//{
				//	if (drive.files.Count > 0) //< it should always be the case
				//	{
				//		SubjectData filename = null;
				//		int i = Lib.RandomInt(drive.files.Count);
				//		foreach (DriveFile file in drive.files.Values)
				//		{
				//			if (i-- == 0)
				//			{
				//				filename = file.subjectData;
				//				break;
				//			}
				//		}
				//		drive.files.Remove(filename);
				//		break;
				//	}
				//}
			}
		}


		// -- TECH ------------------------------------------------------------------

		///<summary>return true if the tech has been researched</summary>
		public static bool HasTech( string tech_id )
		{
			// if science is disabled, all technologies are considered available
			if (HighLogic.CurrentGame.Mode == Game.Modes.SANDBOX) return true;

			// if RnD is not initialized
			if (ResearchAndDevelopment.Instance == null)
			{
				// this should not happen, throw exception
				throw new Exception( "querying tech '" + tech_id + "' while TechTree is not ready" );
			}

			// get the tech
			return ResearchAndDevelopment.GetTechnologyState( tech_id ) == RDTech.State.Available;
		}

		///<summary>return number of techs researched among the list specified</summary>
		public static int CountTech( string[] techs )
		{
			int n = 0;
			foreach (string tech_id in techs) n += HasTech( tech_id ) ? 1 : 0;
			return n;
		}
		#endregion

		#region ASSETS

		///<summary> Get a texture loaded by KSP from the GameData folder. Ex "Kerbalism/Textures/small-info". Height/width must be a power of 2</summary>
		public static Texture2D GetTexture(string path)
		{
			return GameDatabase.Instance.GetTexture(path, false);
		}

		///<summary> Get a texture loaded by KSP from the default Kerbalism/Textures folder. Height/width must be a power of 2</summary>
		public static Texture2D GetKerbalismTexture(string path)
		{
			return GameDatabase.Instance.GetTexture(gameDataDirectory + "/Textures/" + path, false);
		}

		///<summary> Loads a .png texture from the folder defined in <see cref="Textures.TexturePath"/> </summary>
		[Obsolete("Use GetKerbalismTexture(string path) / GetTexture(string path) instead")]
		public static Texture2D GetTexture( string name, int width, int height)
		{
			Texture2D texture = new Texture2D( width, height, TextureFormat.ARGB32, false );
			ImageConversion.LoadImage(texture, System.IO.File.ReadAllBytes(Path.Combine(Textures.TexturePath, name + ".png")));
			return texture;
		}

		///<summary> Returns a scaled copy of the source texture </summary>
		public static Texture2D ScaledTexture( Texture2D src, int width, int height, FilterMode mode = FilterMode.Trilinear )
		{
			ScaleWithGPU( src, width, height, mode );

			Texture2D texture = new Texture2D( width, height, TextureFormat.ARGB32, false );
			texture.Resize( width, height );
			texture.ReadPixels( new Rect( 0, 0, width, height ), 0, 0, true );
			return texture;
		}

		///<summary> Scales the texture data of the source texture </summary>
		public static void ScaleTexture( Texture2D texture, int width, int height, FilterMode mode = FilterMode.Trilinear )
		{
			ScaleWithGPU( texture, width, height, mode );

			texture.Resize( width, height );
			texture.ReadPixels( new Rect( 0, 0, width, height ), 0, 0, true );
			texture.Apply( true );
		}

		///<summary>Renders the source texture into the RTT - used by the scaling methods ScaledTexture() and ScaleTexture() </summary>
		private static void ScaleWithGPU( Texture2D src, int width, int height, FilterMode fmode )
		{
			src.filterMode = fmode;
			src.Apply( true );

			RenderTexture rtt = new RenderTexture( width, height, 32 );
			Graphics.SetRenderTarget( rtt );
			GL.LoadPixelMatrix( 0, 1, 1, 0 );
			GL.Clear( true, true, new Color( 0, 0, 0, 0 ) );
			Graphics.DrawTexture( new Rect( 0, 0, 1, 1 ), src );
		}

		/// <summary>
		/// This makes Kerbalism possibly future-proof for new KSP versions, without requiring user
		/// intervention. With this, chances are that older Kerbalism versions will continue to work
		/// with newer KSP versions (until now the shader folder had to be copied).
		/// <para>
		/// Since KSP 1.5 (and possibly before), it has not been necessary to recompile the shaders.
		/// Kerbalism contained the same set of shader files for 1.5, 1.6, 1.7, 1.8 and 1.9. Chances
		/// are that future versions of KSP will still continue to work with the old shaders. To avoid
		/// the need to keep multiple copies of the same files, or manually rename the shader folder
		/// after a KSP update, use the default shader folder for all versions. If needed, this can be
		/// changed for future versions if they ever should require a new set of shaders.
		/// </para>
		/// </summary>
		private static string GetShaderPath()
		{
			string platform = "windows";
			if (Application.platform == RuntimePlatform.LinuxPlayer) platform = "linux";
			else if (Application.platform == RuntimePlatform.OSXPlayer) platform = "osx";

			int version = Versioning.version_major * 100 + Versioning.version_minor;

			string shadersFolder;
			switch (version)
			{
				// should it ever be necessary...
				//case 105: // 1.5
				//case 106: // 1.6
				//case 107: // 1.7
				//case 108: // 1.8
				//case 109: // 1.9
				//	shadersFolder = "15";
				//	break;
				//case 110: // 1.10
				//	shadersFolder = "110";
				//	break;
				default:
					shadersFolder = "15";
					break;
			}

			return Path.Combine(KerbalismRootPath, "Shaders", shadersFolder, "_" + platform);
		}

		public static Dictionary<string, Material> shaders;
		///<summary> Returns a material from the specified shader </summary>
		public static Material GetShader( string name )
		{
			if (shaders == null)
			{
				shaders = new Dictionary<string, Material>();
#pragma warning disable CS0618 // WWW is obsolete
				using (WWW www = new WWW("file://" + GetShaderPath()))
#pragma warning restore CS0618
				{
					AssetBundle bundle = www.assetBundle;
					Shader[] pre_shaders = bundle.LoadAllAssets<Shader>();
					foreach (Shader shader in pre_shaders)
					{
						string key = shader.name.Replace("Custom/", string.Empty);
						if (shaders.ContainsKey(key))
							shaders.Remove(key);
						shaders.Add(key, new Material(shader));
					}
					bundle.Unload(false);
					www.Dispose();
				}
			}

			Material mat;
			if (!shaders.TryGetValue( name, out mat ))
			{
				throw new Exception( "shader " + name + " not found" );
			}
			return mat;
		}
		#endregion

		#region CONFIG

		///<summary>get a config node from the config system</summary>
		public static ConfigNode ParseConfig(string path)
		{
			return GameDatabase.Instance.GetConfigNode(path) ?? new ConfigNode();
		}

		///<summary>get a set of config nodes from the config system</summary>
		public static ConfigNode[] ParseConfigs(string path)
		{
			return GameDatabase.Instance.GetConfigNodes(path);
		}

		///<summary>get a value from config</summary>
		public static T ConfigValue<T>(ConfigNode cfg, string key, T def_value)
		{
			object value;
			if (TryParseValue(cfg.GetValue(key), typeof(T), out value))
				return (T)value;
			else
				return def_value;
		}

		public static T ConfigEnum<T>(ConfigNode cfg, string key, T def_value)
		{
			if (TryParseValue(cfg.GetValue(key), typeof(T), out object value))
			{
				return (T)value;
			}
			return def_value;
		}

		public static KERBALISM.Kolor ConfigKolor(ConfigNode cfg, string key, KERBALISM.Kolor def_value)
		{
			string val = cfg.GetValue(key);
			if (!string.IsNullOrEmpty(val) && Serialization.GetParser<KERBALISM.Kolor>().Deserialize(val, out KERBALISM.Kolor kolor))
			{
				return kolor;
			}

			return def_value;
		}

		public static double ConfigDuration(ConfigNode cfg, string key, bool applyTimeMultiplier, string defaultValue)
		{
			string durationStr = cfg.GetValue(key);
			if (string.IsNullOrEmpty(durationStr) || !TryParseDuration(durationStr, applyTimeMultiplier, out double duration))
			{
				TryParseDuration(defaultValue, applyTimeMultiplier, out duration);
			}

			return duration;
		}

		public static bool ConfigDuration(ConfigNode cfg, string key, bool applyTimeMultiplier, out double duration)
		{
			string durationStr = cfg.GetValue(key);
			if (string.IsNullOrEmpty(durationStr) || !TryParseDuration(durationStr, applyTimeMultiplier, out duration))
			{
				duration = 1.0;
				return false;
			}

			return true;
		}

		public static void ParseFleeExpressionComfortCall(ref string expression)
		{
			Regex regex = new Regex(@"Comfort\(""(.*?)""\)");
			try
			{
				expression = regex.Replace(expression, ComfortNameEvaluator);
			}
			catch (Exception e)
			{
				Log($"Can't parse modifier expression {expression}", LogLevel.Error);
				throw;
			}
		}

		private static string ComfortNameEvaluator(Match match)
		{
			if (match.Groups.Count != 2)
				throw new Exception($"Error parsing Comfort call : {match.Value}");

			if (!ComfortDefinition.definitionsByName.TryGetValue(match.Groups[1].Value, out ComfortDefinition comfort))
				throw new Exception($"Error parsing Comfort call : {match.Value}, comfort {match.Groups[1].Value} not found !");

			return nameof(VesselDataBase.Habitat) + "." + nameof(VesselHabitat.comforts) + "[" + comfort.definitionIndex + "]";
		}

		public static void ParseFleeExpressionProcessCall(ref string expression)
		{
			Regex regex = new Regex(@"Process\(""(.*?)""\)");
			try
			{
				expression = regex.Replace(expression, ProcessNameEvaluator);
			}
			catch (Exception e)
			{
				Log($"Can't parse modifier expression {expression}", LogLevel.Error);
				throw;
			}
		}

		private static string ProcessNameEvaluator(Match match)
		{
			if (match.Groups.Count != 2)
				throw new Exception($"Error parsing Process call : {match.Value}");

			if (!ProcessDefinition.definitionsByName.TryGetValue(match.Groups[1].Value, out ProcessDefinition process))
				throw new Exception($"Error parsing Process call : {match.Value}, process {match.Groups[1].Value} not found !");

			return nameof(VesselDataBase.VesselProcesses) + "[" + process.definitionIndex + "]";
		}

		public static void ParseFleeExpressionResHandlerResourceCall(ref string expression)
		{
			Regex regex = new Regex(@"Resource\(""(.*?)""\)");
			try
			{
				expression = regex.Replace(expression, ResourceNameEvaluator);
			}
			catch (Exception e)
			{
				Log($"Can't parse modifier expression {expression}", LogLevel.Error);
				throw;
			}
		}

		private static string ResourceNameEvaluator(Match match)
		{
			if (match.Groups.Count != 2)
				throw new Exception($"Error parsing Resource call : {match.Value}");

			if (!VesselResHandler.allKSPResourceIdsByName.TryGetValue(match.Groups[1].Value, out int resId))
				throw new Exception($"Error parsing Resource call : {match.Value}, resource {match.Groups[1].Value} not found !");

			return nameof(VesselDataBase.ResHandler) + "." + nameof(VesselResHandler.GetKSPResource) + "(" + resId + ")";
		}

		/// <summary>
		/// Given a gamedatabase PART{} node, return the game valid name of the part, by doing Replace('_', '.')
		/// </summary>
		public static string ConfigPartInternalName(ConfigNode partConfig)
		{
			return partConfig.GetValue("name")?.Replace('_', '.');
		}

		///<summary>parse a serialized (config) value. Supports all value types including enums and common KSP/Unity types (vector, quaternion, color, matrix4x4...)</summary>
		public static bool TryParseValue(string strValue, Type typeOfValue, out object value)
		{
			value = null;
			if (string.IsNullOrEmpty(strValue))
				return false;

			if (typeof(Enum).IsAssignableFrom(typeOfValue))
			{
				try
				{
					if (!Enum.IsDefined(typeOfValue, strValue)) return false;
					value = Enum.Parse(typeOfValue, strValue);
					return true;
				}
				catch
				{
					return false;
				}
			}
			else if (typeOfValue == typeof(string))
			{
				value = strValue;
				return true;
			}
			else if
			(
				typeOfValue == typeof(bool)
				|| typeOfValue == typeof(byte)
				|| typeOfValue == typeof(char)
				|| typeOfValue == typeof(decimal)
				|| typeOfValue == typeof(double)
				|| typeOfValue == typeof(short)
				|| typeOfValue == typeof(int)
				|| typeOfValue == typeof(long)
				|| typeOfValue == typeof(sbyte)
				|| typeOfValue == typeof(float)
				|| typeOfValue == typeof(string)
				|| typeOfValue == typeof(ushort)
				|| typeOfValue == typeof(uint)
				|| typeOfValue == typeof(ulong)
			)
			{
				try { value = Convert.ChangeType(strValue, typeOfValue); } catch { return false; }
				return true;
			}
			else if (typeOfValue == typeof(Vector2))
			{
				if (!ParseExtensions.TryParseVector2(strValue, out Vector2 parsed)) return false;
				value = parsed;
				return true;
			}
			else if (typeOfValue == typeof(Vector2d))
			{
				if (!ParseExtensions.TryParseVector2d(strValue, out Vector2d parsed)) return false;
				value = parsed;
				return true;
			}
			else if (typeOfValue == typeof(Vector3))
			{
				if (!ParseExtensions.TryParseVector3(strValue, out Vector3 parsed)) return false;
				value = parsed;
				return true;
			}
			else if (typeOfValue == typeof(Vector3d))
			{
				if (!ParseExtensions.TryParseVector3d(strValue, out Vector3d parsed)) return false;
				value = parsed;
				return true;
			}
			else if (typeOfValue == typeof(Vector4))
			{
				if (!ParseExtensions.TryParseVector4(strValue, out Vector4 parsed)) return false;
				value = parsed;
				return true;
			}
			else if (typeOfValue == typeof(Vector4d))
			{
				if (!ParseExtensions.TryParseVector4d(strValue, out Vector4d parsed)) return false;
				value = parsed;
				return true;
			}
			else if (typeOfValue == typeof(Color))
			{
				if (!ParseExtensions.TryParseColor(strValue, out Color parsed)) return false;
				value = parsed;
				return true;
			}
			else if (typeOfValue == typeof(Color32))
			{
				if (!ParseExtensions.TryParseColor32(strValue, out Color32 parsed)) return false;
				value = parsed;
				return true;
			}
			else if (typeOfValue == typeof(Quaternion))
			{
				if (!ParseExtensions.TryParseQuaternion(strValue, out Quaternion parsed)) return false;
				value = parsed;
				return true;
			}
			else if (typeOfValue == typeof(QuaternionD))
			{
				if (!ParseExtensions.TryParseQuaternionD(strValue, out QuaternionD parsed)) return false;
				value = parsed;
				return true;
			}
			else if (typeOfValue == typeof(Matrix4x4))
			{
				value = ConfigNode.ParseMatrix4x4(strValue);
				return true;
			}
			else if (typeOfValue == typeof(Guid))
			{
				try { value = new Guid(strValue); } catch { return false; }
			}

			return false;
		}



		/// <summary> Parse a duration "3y120d5h2m93s into seconds </summary>
		public static bool TryParseDuration(string durationString, bool applyTimeMultiplier, out double result)
		{
			result = 0.0;

			string str = durationString.ToLower();

			if (str == "perpetual")
			{
				result = double.MaxValue;
				return true;
			}

			int p = str.IndexOf('y');
			if(p > 0)
			{
				if (!double.TryParse(str.Substring(0, p), out double parsed)) goto error;
				result += parsed * Settings.ConfigsSecondsInYear;
				str = str.Substring(p + 1);
			}

			p = str.IndexOf('d');
			if (p > 0)
			{
				if (!double.TryParse(str.Substring(0, p), out double parsed)) goto error;
				result += parsed * Settings.ConfigsSecondsInDays;
				str = str.Substring(p + 1);
			}

			p = str.IndexOf('h');
			if (p > 0)
			{
				if (!double.TryParse(str.Substring(0, p), out double parsed)) goto error;
				result += parsed * 3600;
				str = str.Substring(p + 1);
			}

			p = str.IndexOf('m');
			if (p > 0)
			{
				if (!double.TryParse(str.Substring(0, p), out double parsed)) goto error;
				result += parsed * 60;
				str = str.Substring(p + 1);
			}

			p = str.IndexOf('s');
			if (p > 0)
			{
				if (!double.TryParse(str.Substring(0, p), out double parsed)) goto error;
				result += parsed;
				str = str.Substring(p + 1);
			}

			if (!string.IsNullOrEmpty(str))
			{
				if (!double.TryParse(str, out double parsed)) goto error;
				result += parsed;
			}

			if (applyTimeMultiplier)
			{
				result *= Settings.ConfigsDurationMultiplier;
			}
				
			return true;

			error:;
			Log($"Couldn't parse misformatted duration : {durationString}", LogLevel.Error);
			result = 1.0;
			return false;
		}
		#endregion

		#region UI

		///<summary>return true if last GUILayout element was clicked</summary>
		public static bool IsClicked( int button = 0 )
		{
			return Event.current.type == EventType.MouseDown
				&& Event.current.button == button
				&& GUILayoutUtility.GetLastRect().Contains( Event.current.mousePosition );
		}

		///<summary>return true if the mouse is inside the last GUILayout element</summary>
		public static bool IsHover()
		{
			return GUILayoutUtility.GetLastRect().Contains( Event.current.mousePosition );
		}

		///<summary>
		/// render a text field with placeholder
		/// - id: an unique name for the text field
		/// - text: the previous text field content
		/// - placeholder: the text to show if the content is empty
		/// - style: GUIStyle to use for the text field
		///</summary>
		public static string TextFieldPlaceholder( string id, string text, string placeholder, GUIStyle style )
		{
			GUI.SetNextControlName( id );
			text = GUILayout.TextField( text, style );

			if (Event.current.type == EventType.Repaint)
			{
				if (GUI.GetNameOfFocusedControl() == id)
				{
					if (text == placeholder) text = "";
				}
				else
				{
					if (text.Length == 0) text = placeholder;
				}
			}
			return text;
		}

		///<summary>used to make rmb ui status toggles look all the same</summary>
		public static string StatusToggle( string title, string status )
		{
			return Lib.BuildString( "<b>", title, "</b>: ", status );
		}


		///<summary>show a modal popup window where the user can choose among two options</summary>
		public static PopupDialog Popup( string title, string msg, params DialogGUIBase[] buttons)
		{
			return PopupDialog.SpawnPopupDialog
			(
				new Vector2( 0.5f, 0.5f ),
				new Vector2( 0.5f, 0.5f ),
				new MultiOptionDialog( title, msg, title, HighLogic.UISkin, buttons),
				false,
				HighLogic.UISkin,
				true,
				string.Empty
			);
		}

		public static PopupDialog Popup(string title, string msg, float width, params DialogGUIBase[] buttons)
		{
			return PopupDialog.SpawnPopupDialog
			(
				new Vector2(0.5f, 0.5f),
				new Vector2(0.5f, 0.5f),
				new MultiOptionDialog(title, msg, title, HighLogic.UISkin, width, buttons),
				false,
				HighLogic.UISkin,
				true,
				string.Empty
			);
		}

		public static string Greek() {
			string[] letters = {
				"Alpha",
				"Beta",
				"Gamma",
				"Delta",
				"Epsilon",
				"Zeta",
				"Eta",
				"Theta",
				"Iota",
				"Kappa",
				"Lambda",
				"Mu",
				"Nu",
				"Xi",
				"Omicron",
				"Pi",
				"Sigma",
				"Tau",
				"Upsilon",
				"Phi",
				"Chi",
				"Psi",
				"Omega"
			};
			System.Random rand = new System.Random();
			int index = rand.Next(letters.Length);
			return (string)letters[index];
		}

		private static string ecAbbreviation;
		public static string ECAbbreviation
		{
			get
			{
				if (ecAbbreviation == null)
				{
					ecAbbreviation = PartResourceLibrary.Instance.GetDefinition(PartResourceLibrary.ElectricityHashcode).abbreviation;
				}

				return ecAbbreviation;
			}
		}

		public static bool IsPAWOpen(Part part)
		{
			return part.PartActionWindow != null && part.PartActionWindow.gameObject.activeSelf;
		}

		public static bool IsPAWCreated(Part part)
		{
			return part.PartActionWindow != null;
		}

		#endregion

		#region PROTO
		public static class Proto
		{
			private static string sVal;

			public static bool GetBool( ProtoPartModuleSnapshot m, string name, bool def_value = false )
			{
				sVal = m.moduleValues.GetValue(name);
				if (sVal != null && bool.TryParse(sVal, out bool val))
				{
					sVal = null;
					return val;
				}
				sVal = null;
				return def_value;
			}

			public static uint GetUInt( ProtoPartModuleSnapshot m, string name, uint def_value = 0 )
			{
				sVal = m.moduleValues.GetValue(name);
				if (sVal != null && uint.TryParse(sVal, out uint val))
				{
					sVal = null;
					return val;
				}
				sVal = null;
				return def_value;
			}

			public static int GetInt(ProtoPartModuleSnapshot m, string name, int def_value = 0)
			{
				sVal = m.moduleValues.GetValue(name);
				if (sVal != null && int.TryParse(sVal, out int val))
				{
					sVal = null;
					return val;
				}
				sVal = null;
				return def_value;
			}

			public static float GetFloat( ProtoPartModuleSnapshot m, string name, float def_value = 0.0f )
			{
				// note: we set NaN and infinity values to zero, to cover some weird inter-mod interactions
				sVal = m.moduleValues.GetValue(name);
				if (sVal != null && float.TryParse(sVal, out float val) && !float.IsNaN(val) && !float.IsInfinity(val))
				{
					sVal = null;
					return val;
				}
				sVal = null;
				return def_value;
			}

			public static double GetDouble( ProtoPartModuleSnapshot m, string name, double def_value = 0.0 )
			{
				// note: we set NaN and infinity values to zero, to cover some weird inter-mod interactions
				sVal = m.moduleValues.GetValue(name);
				if (sVal != null && double.TryParse(sVal, out double val) && !double.IsNaN(val) && !double.IsInfinity(val))
				{
					sVal = null;
					return val;
				}
				sVal = null;
				return def_value;
			}

			public static string GetString( ProtoPartModuleSnapshot m, string name, string def_value = "" )
			{
				string s = m.moduleValues.GetValue( name );
				return s ?? def_value;
			}

			public static T GetEnum<T>(ProtoPartModuleSnapshot m, string name, T def_value)
			{
				sVal = m.moduleValues.GetValue(name);
				if (sVal != null && Enum.IsDefined(typeof(T), sVal))
				{
					sVal = null;
					return (T)Enum.Parse(typeof(T), sVal); ;
				}
				sVal = null;
				return def_value;
			}

			public static T GetEnum<T>(ProtoPartModuleSnapshot m, string name)
			{
				sVal = m.moduleValues.GetValue(name);
				if (sVal != null && Enum.IsDefined(typeof(T), sVal))
				{
					sVal = null;
					return (T)Enum.Parse(typeof(T), sVal);
				}
				sVal = null;
				return (T)Enum.GetValues(typeof(T)).GetValue(0);
			}

			///<summary>set a value in a proto module</summary>
			public static void Set<T>( ProtoPartModuleSnapshot module, string value_name, T value )
			{
				module.moduleValues.SetValue( value_name, value.ToString(), true );
			}
		}
		#endregion

		#region STRING PARSING
		public static class Parse
		{
			public static bool ToBool( string s, bool def_value = false )
			{
				return s != null && bool.TryParse( s, out bool v) ? v : def_value;
			}

			public static int ToInt(string s, int def_value = 0)
			{
				return s != null && int.TryParse(s, out int v) ? v : def_value;
			}

			public static uint ToUInt( string s, uint def_value = 0u )
			{
				return s != null && uint.TryParse( s, out uint v ) ? v : def_value;
			}

			public static Guid ToGuid (string s)
			{
				return new Guid(s);
			}

			public static float ToFloat( string s, float def_value = 0.0f )
			{
				return s != null && float.TryParse( s, out float v ) ? v : def_value;
			}

			public static double ToDouble( string s, double def_value = 0.0 )
			{
				return s != null && double.TryParse( s, out double v ) ? v : def_value;
			}

			private static bool TryParseColor( string s, out UnityEngine.Color c )
			{
				string[] split = s.Replace( " ", String.Empty ).Split( ',' );
				if (split.Length < 3)
				{
					c = new UnityEngine.Color( 0, 0, 0 );
					return false;
				}
				if (split.Length == 4)
				{
					c = new UnityEngine.Color( ToFloat( split[0], 0f ), ToFloat( split[1], 0f ), ToFloat( split[2], 0f ), ToFloat( split[3], 1f ) );
					return true;
				}
				c = new UnityEngine.Color( ToFloat( split[0], 0f ), ToFloat( split[1], 0f ), ToFloat( split[2], 0f ) );
				return true;
			}

			public static UnityEngine.Color ToColor( string s, UnityEngine.Color def_value )
			{
				UnityEngine.Color v;
				return s != null && TryParseColor( s, out v ) ? v : def_value;
			}
		}
#endregion
	}

	#region UTILITY CLASSES

	public class ObjectPair<T, U>
	{
		public T Key;
		public U Value;

		public ObjectPair(T key, U Value)
		{
			this.Key = key;
			this.Value = Value;
		}
	}

	public class ObjectPool<T> where T : IDisposable, new()
	{
		private readonly Queue<T> pool = new Queue<T>();

		public T Get()
		{
			if (pool.Count == 0)
			{
				return new T();
			}
			else
			{
				return pool.Dequeue();
			}
		}

		public void Return(T value)
		{
			value.Dispose();
			pool.Enqueue(value);
		}
	}

	#endregion


} // KERBALISM
