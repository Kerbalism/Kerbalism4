using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Threading;
using UnityEngine;

namespace KERBALISM
{
	// the radiation system
	public static class Radiation
    {
	    #region DECLARATIONS
		static Dictionary<string, RadiationModel> models = new Dictionary<string, RadiationModel>(16);
		static Dictionary<string, RadiationBody> bodies = new Dictionary<string, RadiationBody>(32);

		// thread used to do the particle-fitting
		static Thread preprocess_thread;

		// material used to render the fields
		static Material mat;

		// current visualization modes
		public static bool show_inner;
		public static bool show_outer;
		public static bool show_pause;

		/// <summary>
		/// 3 mrad/h is used to never allow zero radiation,
		/// and as a threshold for discarding low values
		/// </summary>
		public const double Nominal = 0.0003 / 3600.0;

		// habitat raytracing constants and cache
		private static int partsLayerMask = (1 << LayerMask.NameToLayer("PhysicalObjects")) | LayerUtil.DefaultEquivalent;
		private static RaycastHit[] sunRayHitsCache = new RaycastHit[100];
		private static HashSet<uint> hittedPartsCache = new HashSet<uint>();

		#endregion

		#region INIT

		/// <summary> pseudo-ctor </summary>
		public static void Init()
        {
            // if radiation is disabled
            if (!Features.Radiation)
            {
                // create default environments for all planets
                foreach (CelestialBody body in FlightGlobals.Bodies)
                {
                    bodies.Add(body.bodyName, new RadiationBody(body));
                }

                // and do nothing else
                return;
            }

            // parse RadiationModel
            var rad_nodes = Lib.ParseConfigs("RadiationModel");
            foreach (var rad_node in rad_nodes)
            {
                string name = Lib.ConfigValue(rad_node, "name", "");
                if (!models.ContainsKey(name)) models.Add(name, new RadiationModel(rad_node));
            }

            // parse RadiationBody
            var body_nodes = Lib.ParseConfigs("RadiationBody");
            foreach (var body_node in body_nodes)
            {
                string name = Lib.ConfigValue(body_node, "name", "");
                if (!bodies.ContainsKey(name))
                {
                    CelestialBody body = FlightGlobals.Bodies.Find(k => k.bodyName == name);
                    if (body == null) continue; // skip non-existing bodies
                    bodies.Add(name, new RadiationBody(body_node, models, body));
                }
            }

            // create body environments for all the other planets
            foreach (CelestialBody body in FlightGlobals.Bodies)
            {
                if (!bodies.ContainsKey(body.bodyName))
                {
                    bodies.Add(body.bodyName, new RadiationBody(body));
                }
            }

            // remove unused models
            List<string> to_remove = new List<string>();
            foreach (var rad_pair in models)
            {
                bool used = false;
                foreach (var body_pair in bodies)
                {
                    if (body_pair.Value.model == rad_pair.Value) { used = true; break; }
                }
                if (!used) to_remove.Add(rad_pair.Key);
            }
            foreach (string s in to_remove) models.Remove(s);

            // start particle-fitting thread
            preprocess_thread = new Thread(Preprocess)
            {
                Name = "particle-fitting",
                IsBackground = true
            };
            preprocess_thread.Start();
        }

		#endregion

		#region PARTICULES RENDERING

		/// <summary>  do the particle-fitting in another thread</summary>
		public static void Preprocess()
        {
            // #############################################################
            // # DO NOT LOG FROM A THREAD IN UNITY. IT IS NOT THREAD SAFE. #
            // #############################################################

            // deduce number of particles
            int inner_count = 150000;
            int outer_count = 600000;
            int pause_count = 250000;
            if (Settings.LowQualityRendering)
            {
                inner_count /= 5;
                outer_count /= 5;
                pause_count /= 5;
            }

            // create all magnetic fields and do particle-fitting
            List<string> done = new List<string>();
            foreach (var pair in models)
            {
                // get radiation data
                RadiationModel mf = pair.Value;

                // skip if model is already done
                if (done.Contains(mf.name)) continue;

                // add to the skip list
                done.Add(mf.name);

                // particle-fitting for the inner radiation belt
                if (mf.has_inner)
                {
                    mf.inner_pmesh = new ParticleMesh(mf.InnerFunc, mf.InnerDomain(), mf.InnerOffset(), inner_count, mf.inner_quality);
                }

                // particle-fitting for the outer radiation belt
                if (mf.has_outer)
                {
                    mf.outer_pmesh = new ParticleMesh(mf.OuterFunc, mf.OuterDomain(), mf.OuterOffset(), outer_count, mf.outer_quality);
                }

                // particle-fitting for the magnetopause
                if (mf.has_pause)
                {
                    mf.pause_pmesh = new ParticleMesh(mf.PauseFunc, mf.PauseDomain(), mf.PauseOffset(), pause_count, mf.pause_quality);
                }
            }
        }

		/// <summary> generate gsm-space frame of reference</summary>
		// - origin is at body position
		// - the x-axis point to reference body
		// - the rotation axis is used as y-axis initial guess
		// - the space is then orthonormalized
		// - if the reference body is the same as the body,
		//   the galactic rotation vector is used as x-axis instead
		public static Space GsmSpace(RadiationBody rb, bool tilted)
        {
            CelestialBody body = rb.body;
            CelestialBody reference = FlightGlobals.Bodies[rb.reference];

            Space gsm;
            gsm.origin = ScaledSpace.LocalToScaledSpace(body.position);
            gsm.scale = ScaledSpace.InverseScaleFactor * (float)body.Radius;
            if (body != reference)
            {
                gsm.x_axis = ((Vector3)ScaledSpace.LocalToScaledSpace(reference.position) - gsm.origin).normalized;
                if (!tilted)
                {
                    gsm.y_axis = body.RotationAxis; //< initial guess
                    gsm.z_axis = Vector3.Cross(gsm.x_axis, gsm.y_axis).normalized;
                    gsm.y_axis = Vector3.Cross(gsm.z_axis, gsm.x_axis).normalized; //< orthonormalize
                }
                else
                {
                    /* "Do not try and tilt the planet, that's impossible.
					 * Instead, only try to realize the truth...there is no tilt.
					 * Then you'll see that it is not the planet that tilts, it is
					 * the rest of the universe."
					 * 
					 * - The Matrix
					 * 
					 * 		 
					 * the orbits are inclined (with respect to the equator of the
					 * Earth), but all axes are parallel. and aligned with the unity
					 * world z axis. or is it y? whatever, KSP uses two conventions
					 * in different places.
					 * if you use Principia, the current main body (or if there is
					 * none, e.g. in the space centre or tracking station, the home
					 * body) is not tilted (its axis is the unity vertical.	 
					 * you can fetch the full orientation (tilt and rotation) of any
					 * body (including the current main body) in the current unity
					 * frame (which changes of course, because sometimes KSP uses a
					 * rotating frame, and because Principia tilts the universe
					 * differently if the current main body changes) as the
					 * orientation of the scaled space body
					 * 		 
					 * body.scaledBody.transform.rotation or something along those lines
					 * 
					 * - egg
					 */

                    Vector3 pole = rb.geomagnetic_pole;
                    Quaternion rotation = body.scaledBody.transform.rotation;
                    gsm.y_axis = (rotation * pole).normalized;

                    gsm.z_axis = Vector3.Cross(gsm.x_axis, gsm.y_axis).normalized;
                    gsm.x_axis = Vector3.Cross(gsm.y_axis, gsm.z_axis).normalized; //< orthonormalize
                }
            }
            else
            {
                // galactic
                gsm.x_axis = new Vector3(1.0f, 0.0f, 0.0f);
                gsm.y_axis = new Vector3(0.0f, 1.0f, 0.0f);
                gsm.z_axis = new Vector3(0.0f, 0.0f, 1.0f);
            }

            gsm.origin = gsm.origin + gsm.y_axis * (gsm.scale * rb.geomagnetic_offset);

            return gsm;
        }


		/// <summary>  render the fields of the interesting body</summary>
		public static void Render()
        {
            // get interesting body
            CelestialBody body = Interesting_body();

            // maintain visualization modes
            if (body == null)
            {
                show_inner = false;
                show_outer = false;
                show_pause = false;
            }
            else
            {
                if (Input.GetKeyDown(KeyCode.Keypad0))
                {
                    if (show_inner || show_outer || show_pause)
                    {
                        show_inner = false;
                        show_outer = false;
                        show_pause = false;
                    }
                    else
                    {
                        show_inner = true;
                        show_outer = true;
                        show_pause = true;
                    }
                }
                if (Input.GetKeyDown(KeyCode.Keypad1))
                {
                    show_inner = true;
                    show_outer = false;
                    show_pause = false;
                }
                if (Input.GetKeyDown(KeyCode.Keypad2))
                {
                    show_inner = false;
                    show_outer = true;
                    show_pause = false;
                }
                if (Input.GetKeyDown(KeyCode.Keypad3))
                {
                    show_inner = false;
                    show_outer = false;
                    show_pause = true;
                }
            }


            // if there is an active body, and at least one of the modes is active
            if (body != null && (show_inner || show_outer || show_pause))
            {
                // if we don't know if preprocessing is completed
                if (preprocess_thread != null)
                {
                    // if the preprocess thread has not done yet
                    if (preprocess_thread.IsAlive)
                    {
                        // disable all modes
                        show_inner = false;
                        show_outer = false;
                        show_pause = false;

                        // tell the user and do nothing
                        Message.Post("<color=#00ffff>"+Local.Fittingparticles_msg +"<b></b></color>", Local.ComebackLater_msg);//"Fitting particles to signed distance fields""Come back in a minute"
                        return;
                    }

                    // wait for particle-fitting thread to cleanup
                    preprocess_thread.Join();

                    // preprocessing is complete
                    preprocess_thread = null;
                }


                // load and configure shader
                if (mat == null)
                {
                    if (!Settings.LowQualityRendering)
                    {
                        // load shader
                        mat = Lib.GetShader("MiniParticle");

                        // configure shader
                        mat.SetColor("POINT_COLOR", new Color(0.33f, 0.33f, 0.33f, 0.1f));
                    }
                    else
                    {
                        // load shader
                        mat = Lib.GetShader("PointParticle");

                        // configure shader
                        mat.SetColor("POINT_COLOR", new Color(0.33f, 0.33f, 0.33f, 0.1f));
                        mat.SetFloat("POINT_SIZE", 4.0f);
                    }
                }

                // generate radii-normalized GMS space
                RadiationBody rb = Info(body);
                Space gsm_tilted = GsmSpace(rb, true);

                // [debug] show axis
                //LineRenderer.Commit(gsm_tilted.origin, gsm_tilted.origin + gsm_tilted.x_axis * gsm_tilted.scale * 5.0f, Color.red);
                //LineRenderer.Commit(gsm_tilted.origin, gsm_tilted.origin + gsm_tilted.y_axis * gsm_tilted.scale * 5.0f, Color.green);
                //LineRenderer.Commit(gsm_tilted.origin, gsm_tilted.origin + gsm_tilted.z_axis * gsm_tilted.scale * 5.0f, Color.blue);

                // enable material
                mat.SetPass(0);

                // get magnetic field data
                RadiationModel mf = rb.model;

                // render active body fields
                Matrix4x4 m_tilted = gsm_tilted.LookAt();
                if (show_inner && mf.has_inner && rb.inner_visible) mf.inner_pmesh.Render(m_tilted);
                if (show_outer && mf.has_outer && rb.outer_visible) mf.outer_pmesh.Render(m_tilted);
                if (show_pause && mf.has_pause && rb.pause_visible) mf.pause_pmesh.Render(GsmSpace(rb, false).LookAt());
            }
        }

		#endregion

		#region BODY RELATED METHODS

		/// <summary> access radiation data about a specific body </summary>
		public static RadiationBody Info(CelestialBody body)
		{
			RadiationBody rb;
			return bodies.TryGetValue(body.bodyName, out rb) ? rb : null; //< this should never happen
		}

		/// <summary> Returns the radiation emitted by the body at the center, adjusted by solar activity cycle </summary>
		private static double RadiationR0(RadiationBody rb)
		{
			// for easier configuration, the radiation model sets the radiation on the surface of the body.
			// from there, it decreases according to the inverse square law with distance from the surface.

			var r0 = rb.radiation_r0; // precomputed

			// if there is a solar cycle, add a bit of radiation variation relative to current activity

			if (rb.solar_cycle > 0)
			{
				var activity = rb.SolarActivity() * 0.3;
				r0 = r0 + r0 * activity;
			}

			return r0;
		}

		/// <summary>
		/// Return the factor for the radiation in a belt at the given depth
		/// </summary>
		/// <param name="depth">distance from the border of the belt, in panetary radii. negative while in belt.</param>
		/// <param name="scale">represents the 'thickness' of the belt (radius of the outer torus)</param>
		/// <param name="gradient">represents how steeply the value will increase</param>
		public static double RadiationInBelt(double depth, double scale, double gradient = 1.5)
		{
			return Lib.Clamp(gradient * -depth / scale, 0.0, 1.0);
		}

		/// <summary> return the surface radiation for the body specified (used by body info panel)</summary>
		public static double ComputeSurface(CelestialBody b, double gamma_transparency)
		{
			if (!Features.Radiation) return 0.0;

			// store stuff
			Space gsm;
			Vector3 p;
			double D;

			// transform to local space once
			Vector3d position = ScaledSpace.LocalToScaledSpace(b.position);

			// accumulate radiation
			double radiation = 0.0;
			CelestialBody body = b;
			while (body != null)
			{
				RadiationBody rb = Info(body);
				RadiationModel mf = rb.model;

				var activity = rb.SolarActivity(false);

				if (mf.HasField())
				{
					// generate radii-normalized GSM space
					gsm = GsmSpace(rb, true);

					// move the poing in GSM space
					p = gsm.TransformIn(position);

					// accumulate radiation and determine pause/belt flags
					if (mf.has_inner)
					{
						D = mf.InnerFunc(p);
						// allow for radiation field to grow/shrink with solar activity
						D -= activity * 0.25 / mf.inner_radius;

						var r = RadiationInBelt(D, mf.inner_radius, rb.radiation_inner_gradient);
						radiation += r * rb.radiation_inner * (1 + activity * 0.3);
					}
					if (mf.has_outer)
					{
						D = mf.OuterFunc(p);
						// allow for radiation field to grow/shrink with solar activity
						D -= activity * 0.25 / mf.outer_radius;

						var r = RadiationInBelt(D, mf.outer_radius, rb.radiation_outer_gradient);
						radiation += r * rb.radiation_outer * (1 + activity * 0.3);
					}
					if (mf.has_pause)
					{
						gsm = GsmSpace(rb, false);
						p = gsm.TransformIn(position);
						D = mf.PauseFunc(p);
						radiation += Lib.Clamp(D / -0.1332f, 0.0f, 1.0f) * rb.RadiationPause();
					}
				}

				if (rb.radiation_surface > 0 && body != b)
				{
					// add surface radiation emitted from other body
					double distance = (b.position - body.position).magnitude;
					var r0 = RadiationR0(rb);
					var r1 = DistanceRadiation(r0, distance);

					// Lib.Log("Surface radiation on " + b + " from " + body + ": " + Lib.HumanReadableRadiation(r1) + " distance " + distance);

					// clamp to max. surface radiation. when loading on a rescaled system, the vessel can appear to be within the sun for a few ticks
					radiation += Math.Min(r1, rb.radiation_surface);
				}

				// avoid loops in the chain
				body = (body.referenceBody != null && body.referenceBody.referenceBody == body) ? null : body.referenceBody;
			}

			// add extern radiation
			radiation += Settings.ExternRadiation;

			// Lib.Log("Radiation subtotal on " + b + ": " + Lib.HumanReadableRadiation(radiation) + ", gamma " + gamma_transparency);

			// scale radiation by gamma transparency if inside atmosphere
			radiation *= gamma_transparency;
			// Lib.Log("srf scaled on " + b + ": " + Lib.HumanReadableRadiation(radiation));

			// add surface radiation of the body itself
			RadiationBody bodyInfo = Info(b);
			// clamp to max. bodyInfo.radiation_surface to avoid extreme radiation effects while loading a vessel on rescaled systems
			radiation += Math.Min(bodyInfo.radiation_surface, DistanceRadiation(RadiationR0(bodyInfo), b.Radius));

			// Lib.Log("Radiation on " + b + ": " + Lib.HumanReadableRadiation(radiation) + ", own surface radiation " + Lib.HumanReadableRadiation(DistanceRadiation(RadiationR0(Info(b)), b.Radius)));

			// Lib.Log("radiation " + radiation + " nominal " + Nominal);

			// clamp radiation to positive range
			// note: we avoid radiation going to zero by using a small positive value
			radiation = Math.Max(radiation, Nominal);

			return radiation;
		}

		/// <summary> deduce first interesting body for radiation in the body chain</summary>
		private static CelestialBody Interesting_body(CelestialBody body)
		{
			if (Info(body).model.HasField()) return body;      // main body has field
			else if (body.referenceBody != null                 // it has a ref body
			  && body.referenceBody.referenceBody != body)      // avoid loops in planet setup (eg: OPM)
				return Interesting_body(body.referenceBody);      // recursively
			else return null;                                   // nothing in chain
		}

		private static CelestialBody Interesting_body()
		{
			var target = PlanetariumCamera.fetch.target;
			return
				target == null
			  ? null
			  : target.celestialBody != null
			  ? Interesting_body(target.celestialBody)
			  : target.vessel != null
			  ? Interesting_body(target.vessel.mainBody)
			  : null;
		}

		#endregion

		#region VESSEL RELATED METHODS

		/// <summary> return the total environent radiation at position specified </summary>
		public static double Compute(VesselData vd, Vector3d position, double gammaTransparency, double sunlight, out bool blackout,
									 out bool magnetosphere, out bool innerBelt, out bool outerBelt, out bool interstellar)
		{
			// prepare out parameters
			blackout = false;
			magnetosphere = false;
			innerBelt = false;
			outerBelt = false;
			interstellar = false;

			// no-op when Radiation is disabled
			if (!Features.Radiation) return 0.0;

			// store stuff
			Space gsm;
			Vector3 p;
			double D;
			double r;

			// accumulate radiation
			double radiation = 0.0;
			double mainBodyRadiation = 0.0;
			CelestialBody body = vd.Vessel.mainBody;

			vd.radiationInnerBelt = 0.0;
			vd.radiationOuterBelt = 0.0;
			vd.radiationMagnetopause = 0.0;
			vd.radiationBodies = 0.0;
			vd.radiationSolar = 0.0;
			
			while (body != null)
			{
				// Compute radiation values from overlapping 3d fields (belts + magnetospheres)
				RadiationBody rb = Info(body);
				RadiationModel mf = rb.model;

				// activity is [-0.15..1.05]
				var activity = rb.SolarActivity(false);

				if (mf.HasField())
				{
					// transform to local space once
					var scaled_position = ScaledSpace.LocalToScaledSpace(position);

					// generate radii-normalized GSM space
					gsm = GsmSpace(rb, true);

					// move the point in GSM space
					p = gsm.TransformIn(scaled_position);

					// accumulate radiation and determine pause/belt flags
					if (mf.has_inner)
					{
						D = mf.InnerFunc(p);
						innerBelt |= D < 0;

						// allow for radiation field to grow/shrink with solar activity
						D -= activity * 0.25 / mf.inner_radius;
						r = RadiationInBelt(D, mf.inner_radius, rb.radiation_inner_gradient);
						vd.radiationInnerBelt += r * rb.radiation_inner * (1 + activity * 0.3);
						
					}
					if (mf.has_outer)
					{
						D = mf.OuterFunc(p);
						outerBelt |= D < 0;

						// allow for radiation field to grow/shrink with solar activity
						D -= activity * 0.25 / mf.outer_radius;
						r = RadiationInBelt(D, mf.outer_radius, rb.radiation_outer_gradient);
						vd.radiationOuterBelt += r * rb.radiation_outer * (1 + activity * 0.3);
						
					}
					if (mf.has_pause)
					{
						gsm = GsmSpace(rb, false);
						p = gsm.TransformIn(scaled_position);
						D = mf.PauseFunc(p);

						vd.radiationMagnetopause += Lib.Clamp(D / -0.1332f, 0.0f, 1.0f) * rb.RadiationPause();
						

						magnetosphere |= D < 0.0f && !Sim.IsStar(rb.body); //< ignore heliopause
						interstellar |= D > 0.0f && Sim.IsStar(rb.body); //< outside heliopause
					}
				}

				if (rb.radiation_surface > 0)
				{
					if (Sim.IsBodyVisible(vd.Vessel, position, body, vd.VisibleBodies, out _, out double distance))
					{
						var r0 = RadiationR0(rb);
						var r1 = DistanceRadiation(r0, distance);

						// clamp to max. surface radiation.
						// when loading on a rescaled system, the vessel can appear to be within the sun for a few ticks
						var rad = Math.Min(r1, rb.radiation_surface);

						if (Sim.IsStar(body))
						{
							vd.radiationSolar += rad;
						}
						else
						{
							if (body == vd.MainBody)
								mainBodyRadiation += rad; // TODO : this should be affected by the body gamma transparency, but the other way around
							else
								vd.radiationBodies += rad;
						}
					}
				}

				// avoid loops in the chain
				body = (body.referenceBody != null && body.referenceBody.referenceBody == body) ? null : body.referenceBody;
			}

			// add all sources and apply gamma transparency if inside atmosphere
			vd.radiationInnerBelt *= gammaTransparency;
			vd.radiationOuterBelt *= gammaTransparency;
			vd.radiationMagnetopause *= gammaTransparency;
			vd.radiationSolar *= gammaTransparency;
			vd.radiationBodies *= gammaTransparency;

			radiation += vd.radiationInnerBelt;
			radiation += vd.radiationOuterBelt;
			radiation += vd.radiationMagnetopause;
			radiation += vd.radiationSolar;
			radiation += vd.radiationBodies;

			// add extern radiation
			radiation += Settings.ExternRadiation * gammaTransparency;

			// main body surface radiation is unaffected by gamma transparency
			radiation += mainBodyRadiation;
			vd.radiationBodies += mainBodyRadiation;

#if DEBUG_RADIATION
			if (v.loaded) Lib.Log("Radiation " + v + " after gamma: " + Lib.HumanReadableRadiation(radiation) + " transparency: " + gamma_transparency);
#endif
			//var passiveShielding = PassiveShield.Total(v);
			//shieldedRadiation -= passiveShielding;

			// clamp radiation to positive range
			// note: we avoid radiation going to zero by using a small positive value
			radiation = Math.Max(radiation, 0.0);

#if DEBUG_RADIATION
			if (v.loaded) Lib.Log("Radiation " + v + " after clamp: " + Lib.HumanReadableRadiation(radiation) + " shielded " + Lib.HumanReadableRadiation(shieldedRadiation));
#endif
			// return radiation
			return radiation;
		}

		/// <summary>
		/// Return the proportion of radiation blocked by a material thickness,
		/// given a known Half-Value Layer (HVL) for that material. 
		/// <br/>Some values for low-medium (1 MeV) energy gamma/xray radiation :
		/// <br/> - lead (density 11.3) : 0.8 cm
		/// <br/> - iron (density 7.9) : 1.5 cm
		/// <br/> - aluminum (density 2.8) : 4.2 cm
		/// <br/> - water (density 1.0) : 9.7 cm
		/// <br/> - air (density 0.0013) : 8400 cm
		/// <br/> - concrete (density 2.3) : 4.7 cm
		/// <br/> Note that these values will be quite lower for low energy radiation (ex : water HVL = 5 cm for 0.3 MeV)
		/// but not much higher for high energy radiation (ex : water HVL will stop increasing at ~40 MeV, at a cap of 43cm)
		/// <br/> For our concerns, since radiation sources are likely medium energy (for example the SNAP-27 based RTG emits 
		/// gamma energy mostly in the 0.2 - 2.0 MeV range), taking 1.0 MeV as a baseline doesn't feel too wrong.
		/// </summary>
		/// <param name="hvl">thickness of material required to divide incoming radiation by two</param>
		/// <param name="thickness">thickness of material</param>
		/// <returns></returns>
		public static double HVLBlockingFactor(double hvl, double thickness)
		{
			return 1.0 - Math.Pow(0.5, thickness / hvl);
		}

		/// <summary>
		/// return an estimate of proportion of radiation blocked by a material thickness,
		/// given that material density, using a linear HVL/density approximation where a density of 1 gives a HVL of 10cm.
		/// (see HVLBlockingFactor() for details)
		/// </summary>
		/// <param name="density">material density</param>
		/// <param name="thickness">thickness of material</param>
		/// <returns></returns>
		public static double DensityBlockingFactor(double density, double thickness, bool highEnergy)
		{
			return 1 - Math.Pow(0.5, thickness / (highEnergy ? ResourceHVLDefinition.waterHVL_Gamma25MeV : ResourceHVLDefinition.waterHVL_Gamma1MeV / density));
		}

		/// <summary> show warning message when a vessel cross a radiation belt</summary>
		public static void BeltWarnings(Vessel v, VesselData vd)
		{
			// if radiation is enabled
			if (Features.Radiation)
			{
				// we only show the warning for manned vessels
				bool must_warn = vd.CrewCount > 0;

				// are we inside a belt
				bool inside_belt = vd.EnvInnerBelt || vd.EnvOuterBelt;

				// show the message
				if (inside_belt && !vd.msg_belt && must_warn)
				{
					Message.Post(Local.BeltWarnings_msg.Format("<b>" + v.vesselName + "</b>", "<i>" + v.mainBody.bodyName + "</i>"), Local.BeltWarnings_msgSubtext);//<<1>> is crossing <<2>> radiation belt"Exposed to extreme radiation"
					vd.msg_belt = true;
				}
				else if (!inside_belt && vd.msg_belt)
				{
					// no message after crossing the belt
					vd.msg_belt = false;
				}
			}
		}

		#endregion

		/// <summary> Calculate radiation at a given distance to an emitter by inverse square law </summary>
		public static double DistanceRadiation(double radiation, double distance)
        {
            // result = radiation / (4 * Pi * r^2)
            return radiation / Math.Max(1.0, 4 * Math.PI * distance * distance);
        }

		/// <summary> Calculate radiation at a given distance (squared) to an emitter by inverse square law </summary>
		public static double DistanceSqrRadiation(double radiation, double distanceSqr)
		{
			// result = radiation / (4 * Pi * r^2)
			return radiation / Math.Max(1.0, 4 * Math.PI * distanceSqr);
		}
    }

} // KERBALISM
