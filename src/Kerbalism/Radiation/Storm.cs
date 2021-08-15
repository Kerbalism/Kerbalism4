using System.Collections.Generic;

namespace KERBALISM
{
    public static class Storm
    {
        public readonly static Dictionary<int, float> sunObservationQuality = new Dictionary<int, float>();

        internal static void CreateStorm(StormData bd, CelestialBody body, double distanceToSun)
        {
            // do nothing if storms are disabled
            if (!Features.Radiation) return;

            var now = Planetarium.GetUniversalTime();

            if (bd.storm_generation < now)
            {
                var sun = Sim.GetParentStar(body);
                var avgDuration = PreferencesRadiation.Instance.AvgStormDuration;

                // retry after 5 * average storm duration + jitter (to avoid recalc spikes)
                bd.storm_generation = now + avgDuration * (5 + Lib.RandomDouble());

                var rb = Radiation.Info(sun);
                var activity = rb.solar_cycle > 0 ? rb.SolarActivity() : 1.0;

                // StormFrequency is the likelihood of a storm every (5 * avg duration) interval.
                if (Lib.RandomDouble() < activity * PreferencesRadiation.Instance.stormFrequency)
                {
                    // storm duration depends on current solar activity
                    bd.storm_duration = avgDuration / 2.0 + avgDuration * activity * 2;

                    // if further out, the storm lasts longer (but is weaker)
                    bd.storm_duration /= Storm_frequency(distanceToSun);

                    // set a start time to give enough time for warning
                    bd.storm_time = now + Time_to_impact(distanceToSun);

                    // delay next storm generation by duration of this one
                    bd.storm_generation += bd.storm_duration;

                    // add a random error to the estimated storm duration if we don't observe the sun too well
                    var error = bd.storm_duration * 3 * Lib.RandomDouble() * (1 - SunObservationQuality(sun));
                    bd.displayed_duration = bd.storm_duration + error;

                    // show warning message only if you're lucky...
                    bd.display_warning = Lib.RandomFloat() < SunObservationQuality(sun);

                    Lib.LogDebug($"Storm on {body} starts in { Lib.HumanReadableDuration(bd.storm_time - now) }.  Duration: { Lib.HumanReadableDuration(bd.storm_duration) }");
                }
            }

            if (bd.storm_state == StormData.StormState.inprogress && bd.storm_time + bd.storm_duration < now)
            {
                // storm is over
                bd.Reset();
            }
            else if (bd.storm_state == StormData.StormState.inbound && bd.storm_time < now)
            {
                bd.storm_state = StormData.StormState.inprogress;
            }
            else if (bd.storm_state == StormData.StormState.none && bd.storm_time > now)
            {
                bd.storm_state = StormData.StormState.inbound;
            }
        }

        public static void Update(CelestialBody body)
        {
            // do nothing if storms are disabled
            if (!Features.Radiation) return;

            UpdateCommon(DB.Storm(body.name), body: body);
        }

        public static void Update(Vessel v, VesselData vd)
        {
            // do nothing if storms are disabled
            if (!Features.Radiation) return;

            // only consider vessels in interplanetary space
            if (!Sim.IsStar(v.mainBody)) return;

            // disregard EVAs
            if (v.isEVA) return;

            UpdateCommon(vd.stormData, vd: vd);
        }

        private static void UpdateCommon(StormData bd, CelestialBody body = null, VesselData vd = null)
        {
            if (body is object)
                CreateStorm(bd, body, body.orbit.semiMajorAxis);
            else
                CreateStorm(bd, vd.MainBody, vd.MainStar.distance);

            if (vd == null || vd.cfg_storm)
                MessageStorm(bd, body, vd);
            bd.msg_storm = bd.storm_state;
        }

        private static void MessageStorm(StormData bd, CelestialBody body = null, VesselData vd = null)
        {
            // Only message on state change
            if (bd.storm_state == bd.msg_storm) return;

            switch (bd.storm_state)
            {
                case StormData.StormState.inprogress:
                    string s1 = body is object ? Local.Storm_msg1.Format($"<b>{body.name}</b>")
                                                : Local.Storm_msg5.Format($"<b>{vd.VesselName}</b>");
                    Message.Post(Severity.danger, s1, Lib.BuildString(Local.Storm_msg1text, " ", Lib.HumanReadableDuration(bd.displayed_duration)));
                    break;

                case StormData.StormState.inbound:
                    if (bd.display_warning)
                    {
                        var tti = bd.storm_time - Planetarium.GetUniversalTime();
                        string s2 = body is object ? Local.Storm_msg2.Format($"<b>{body.name}</b>")
                                                    : Local.Storm_msg6.Format($"<b>{vd.VesselName}</b>");
                        Message.Post(Severity.warning, s2, Lib.BuildString(Local.Storm_msg2text, " ", Lib.HumanReadableDuration(tti)));
                    }
                    break;

                case StormData.StormState.none:
                    {
                        string s3 = body is object ? Local.Storm_msg3.Format($"<b>{body.name}</b>")
                                                    : Local.Storm_msg4.Format($"<b>{vd.VesselName}</b>");
                        Message.Post(Severity.relax, s3);
                        if (vd != null)
                            vd.msg_signal = false; // Avoid spamming 'signal is back' messages after the storm is over
                    }
                    break;
            }
        }


        // return storm frequency factor by distance from sun
        static double Storm_frequency(double dist)
        {
            return Sim.AU / dist;
        }


        // return time to impact from CME event, in seconds
        static double Time_to_impact(double dist)
        {
            return dist / PreferencesRadiation.Instance.StormEjectionSpeed;
        }


        // return true if body is relevant to the player
        // - body: reference body of the planetary system
        static bool Body_is_relevant(CelestialBody body)
        {
            // [disabled]
            // special case: home system is always relevant
            // note: we deal with the case of a planet mod setting homebody as a moon
            //if (body == Lib.PlanetarySystem(FlightGlobals.GetHomeBody())) return true;

            // for each vessel
            foreach (Vessel v in FlightGlobals.Vessels)
            {
                // if inside the system
                if (Sim.GetParentPlanet(v.mainBody) == body)
                {
					// get info from the cache
					if (!v.TryGetVesselData(out VesselData vd))
						continue;

                    // skip invalid vessels
                    if (!vd.IsSimulated) continue;

                    // obey message config
                    if (!vd.cfg_storm) continue;

                    // body is relevant
                    return true;
                }
            }
            return false;
        }


        // Identify bodies that we should not generate storms for
        public static bool Skip_body(CelestialBody body)
        {
            // skip all bodies if storms are disabled
            if (!Features.Radiation) return true;

            // skip the sun
            if (Sim.IsStar(body)) return true;

            // skip moons
            // note: referenceBody is never null here
            if (!Sim.IsStar(body.referenceBody)) return true;

            // do not skip the body
            return false;
        }

        /// <summary>return true if a storm is incoming</summary>
        public static bool Incoming(VesselData vd)
        {
			var bd = Sim.IsStar(vd.Vessel.mainBody) ? vd.stormData : DB.Storm(Sim.GetParentPlanet(vd.Vessel.mainBody).name);
            return bd.storm_state == StormData.StormState.inbound && bd.display_warning;
        }

        /// <summary>return true if a storm is in progress</summary>
        public static bool InProgress(VesselData vd)
        {
			var bd = Sim.IsStar(vd.Vessel.mainBody) ? vd.stormData : DB.Storm(Sim.GetParentPlanet(vd.Vessel.mainBody).name);
            return bd.storm_state == StormData.StormState.inprogress;
        }

		internal static float SunObservationQuality(CelestialBody sun)
		{
            if (!sunObservationQuality.ContainsKey(sun.flightGlobalsIndex))
                sunObservationQuality[sun.flightGlobalsIndex] = 1;
            return sunObservationQuality[sun.flightGlobalsIndex];
		}

		internal static void SetSunObservationQuality(CelestialBody sun, float quality)
		{
            sunObservationQuality[sun.flightGlobalsIndex] = quality;
		}
	}

} // KERBALISM
