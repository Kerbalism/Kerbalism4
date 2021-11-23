using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using CommNet;
using HarmonyLib;
using UnityEngine.Profiling;

namespace KERBALISM
{
	public class CommHandler
	{
		private static bool CommNetStormPatchApplied = false;
		private bool resetTransmitters;
		protected VesselData vd;

		/// <summary>
		/// false while the network isn't initialized or when the transmitter list is not up-to-date
		/// </summary>
		public bool IsReady => NetworkIsReady && resetTransmitters == false;

		/// <summary>
		/// pseudo ctor for getting the right handler type
		/// </summary>
		public static CommHandler GetHandler(VesselData vd, bool isGroundController)
		{
			CommHandler handler;

			// Note : API CommHandlers may not be registered yet when this is called,
			// but this shouldn't be an issue, as the derived types UpdateTransmitters / UpdateNetwork
			// won't be called anymore once the API handler is registered.
			// This said, this isn't ideal, and it would be cleaner to have a "commHandledByAPI"
			// bool that mods should set once and for all before any vessel exist.

			if (!CommNetStormPatchApplied)
			{
				CommNetStormPatchApplied = true;

				//if (API.Comm.handlers.Count == 0 && !RemoteTech.Installed)
				if (!RemoteTech.Installed)
				{
					CommNetStormPatch();
				}
			}


			//if (API.Comm.handlers.Count > 0)
			//	handler = new CommHandler();
			//else if (RemoteTech.Installed)
			if (RemoteTech.Installed)
				handler = new CommHandlerRemoteTech();
			else if (isGroundController)
				handler = new CommHandlerCommNetSerenity();
			else
				handler = new CommHandlerCommNetVessel();

			handler.vd = vd;
			handler.resetTransmitters = true;

			return handler;
		}

		/// <summary> Update the provided Connection </summary>
		public void UpdateConnection(ConnectionInfo connection)
		{
			Profiler.BeginSample("Kerbalism.CommHandler.UpdateConnection");

			UpdateInputs(connection);

			//if (API.Comm.handlers.Count == 0)
			//{
				if (NetworkIsReady)
				{
					if (resetTransmitters)
					{
						UpdateTransmitters(connection, true);
						resetTransmitters = false;
					}
					else
					{
						UpdateTransmitters(connection, false);
					}

					UpdateNetwork(connection);
				}
			//}
			//else
			//{
			//	try
			//	{
			//		API.Comm.handlers[0].Invoke(null, new object[] { connection, vd.Vessel });
			//	}
			//	catch (Exception e)
			//	{
			//		Lib.Log("CommInfo handler threw exception " + e.Message + "\n" + e.ToString(), Lib.LogLevel.Error);
			//	}
			//}
			Profiler.EndSample();
		}

		/// <summary>
		/// Clear and re-find all transmitters partmodules on the vessel.
		/// Must be called when parts have been removed / added on the vessel.
		/// </summary>
		public void ResetPartTransmitters() => resetTransmitters = true;


		/// <summary>
		/// update the fields that can be used as an input by API handlers
		/// </summary>
		protected virtual void UpdateInputs(ConnectionInfo connection)
		{
			// TODO : all this will likely use last update state. Analyze what this is used for, it is likely that we don't need most of it
			// in any case, this should be moved to the end of Kerbalism.FixedUpdate()
			connection.transmitting = vd.vesselComms.transmittedFiles.Count > 0; //  do we really need it that ? 
			connection.storm = vd.EnvStorm;
			connection.powered = vd.ResHandler.ElectricCharge.Amount > 0.0; // TODO : use the recipe !
		}

		protected virtual bool NetworkIsReady => true;

		protected virtual void UpdateNetwork(ConnectionInfo connection) { }

		protected virtual void UpdateTransmitters(ConnectionInfo connection, bool searchTransmitters) { }

		private static double dampingExponent = 0;

		public static double DataRateDampingExponent
		{
			get
			{
				if (dampingExponent != 0)
					return dampingExponent;

				if (Settings.DampingExponentOverride != 0)
					return Settings.DampingExponentOverride;

				// KSP calculates the signal strength using a cubic formula based on distance (see below).
				// Based on that signal strength, we calculate a data rate. The goal is to get data rates that
				// are comparable to what NASA gets near Mars, depending on the distance between Earth and Mars
				// (~0.36 AU - ~2.73 AU).
				// The problem is that KSPs formula would be somewhat correct for signal strength in reality,
				// but the stock system is only 1/10th the size of the real solar system. Picture this: Jools
				// orbit is about as far removed from the sun as the real Mercury, which means that all other
				// planets would orbit the sun at a distance that is even smaller. In game, distance plays a
				// much smaller role than it would in reality, because the in-game distances are very small,
				// so signal strength just doesn't degrade fast enough with distance.
				//
				// We cannot change how KSP calculates signal strength, so we apply a damping formula
				// for the data rate. Basically, it goes like this:
				//
				// data rate = base rate * signal strength
				// (base rate would be the max. rate at 0 distance)
				//
				// To degrade the data rate with distance, Kerbalism will do this instead:
				//
				// data rate = base rate * (signal strength ^ damping exponent)
				// (this works because signal strength will always be in the range [0..1])
				//
				// The problem is, we don't know which solar system we'll be in, and how big it will be.
				// Popular systems like JNSQ are 2.7 times bigger than stock, RSS is 10 times bigger.
				// So we try to find a damping exponent that gives good results for the solar system we're in,
				// based on the distance of the home planet to the sun (1 AU).

				// range of DSN at max. level
				var maxDsnRange = GameVariables.Instance.GetDSNRange(1f);

				// signal strength at ~ average earth - mars distance
				var strengthAt2AU = SignalStrength(maxDsnRange, 2 * Sim.AU);

				// For our estimation, we assume a base rate similar to the stock communotron 88-88
				var baseRate = 0.48;

				// At 2 AU, this is the rate we want to get out of it
				var desiredRateAt2AU = 0.3;

				// dataRate = baseRate * (strengthAt2AU ^ exponent)
				// so...
				// exponent = log_strengthAt2AU(dataRate / baseRate)
				dampingExponent = Math.Log(desiredRateAt2AU / baseRate, strengthAt2AU);

				Lib.Log($"Calculated DataRateDampingExponent: {dampingExponent.ToString("F4")} (max. DSN range: {maxDsnRange.ToString("F0")}, strength at 2 AU: {strengthAt2AU.ToString("F3")})");

				return dampingExponent;
			}
		}

		public static double SignalStrength(double maxRange, double distance)
		{
			if (distance > maxRange)
				return 0.0;

			double relativeDistance = 1.0 - (distance / maxRange);
			double strength = (3.0 - (2.0 * relativeDistance)) * (relativeDistance * relativeDistance);

			if (strength < 0)
				return 0.0;

			return strength;
		}

		private static FieldInfo commNetVessel_inPlasma;
		private static FieldInfo commNetVessel_plasmaMult;

		private static void CommNetStormPatch()
		{
			commNetVessel_inPlasma = AccessTools.Field(typeof(CommNetVessel), "inPlasma");
			commNetVessel_plasmaMult = AccessTools.Field(typeof(CommNetVessel), "plasmaMult");

			MethodInfo CommNetVessel_OnNetworkPreUpdate_Info = AccessTools.Method(typeof(CommNetVessel), nameof(CommNetVessel.OnNetworkPreUpdate));
			MethodInfo CommNetVessel_OnNetworkPreUpdate_Postfix_Info = AccessTools.Method(typeof(CommHandler), nameof(CommNetVessel_OnNetworkPreUpdate_Postfix));

			Loader.HarmonyInstance.Patch(CommNetVessel_OnNetworkPreUpdate_Info, null, new HarmonyMethod(CommNetVessel_OnNetworkPreUpdate_Postfix_Info));
		}

		private static void CommNetVessel_OnNetworkPreUpdate_Postfix(CommNetVessel __instance)
		{
			if (!__instance.Vessel.TryGetVesselData(out VesselData vd))
				return;

			if (vd.EnvStormRadiation > 0.0)
			{
				commNetVessel_inPlasma.SetValue(__instance, true);
				double stormIntensity = vd.EnvStormRadiation / PreferencesRadiation.Instance.StormRadiation;
				stormIntensity = Lib.Clamp(stormIntensity, 0.0, 1.0);
				commNetVessel_plasmaMult.SetValue(__instance, stormIntensity);
			}
		}
	}
}
