using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	public partial class VesselData : VesselDataBase
	{
		/// <summary> update the rescue state of kerbals when a vessel is loaded, return true if the vessek</summary>
		public static bool CheckRescueStatus(Vessel v, out bool rescueJustLoaded)
		{
			bool isRescue = false;
			rescueJustLoaded = false;

			// deal with rescue missions
			foreach (ProtoCrewMember c in Lib.CrewList(v))
			{
				// get kerbal data
				// note : this whole thing rely on KerbalData.rescue being initialized to true
				// when DB.Kerbal() (which is a get-or-create) is called for the first time
				KerbalData kd = DB.GetOrCreateKerbalData(c);

				// flag the kerbal as not rescue at prelaunch
				// if the KerbalData wasn't created during prelaunch, that code won't be called
				// and KerbalData.rescue will stay at the default "true" value
				if (v.situation == Vessel.Situations.PRELAUNCH)
				{
					kd.isRescue = false;
				}

				if (kd.isRescue)
				{
					if (!v.loaded)
					{
						isRescue |= true;
					}
					// we de-flag a rescue kerbal when the rescue vessel is first loaded
					else
					{
						rescueJustLoaded |= true;
						isRescue &= false;

						// flag the kerbal as non-rescue
						// note: enable life support mechanics for the kerbal
						kd.isRescue = false;

						// show a message
						Message.Post(Lib.BuildString(Local.Rescuemission_msg1, " <b>", c.name, "</b>"), Lib.BuildString((c.gender == ProtoCrewMember.Gender.Male ? Local.Kerbal_Male : Local.Kerbal_Female), Local.Rescuemission_msg2));//We found xx  "He"/"She"'s still alive!"
					}
				}
			}
			return isRescue;
		}

		/// <summary> Gift resources to a rescue vessel, to be called when a rescue vessel is first being loaded</summary>
		private void OnRescueVesselLoaded()
		{
			// give the vessel some propellant usable on eva
			string evaFuelName = Lib.EvaPropellantName();
			double evaFuelPerCrew = Lib.EvaPropellantCapacity();

			foreach (PartData part in Parts)
			{
				int partCrewCount = part.LoadedPart.protoModuleCrew.Count;
				if (partCrewCount > 0)
				{
					PartResourceWrapper evaFuel = part.resources.Find(p => p.ResName == evaFuelName);
					double fuelAmount = evaFuelPerCrew * partCrewCount;
					if (evaFuel == null)
					{
						part.resources.AddResource(evaFuelName, fuelAmount, fuelAmount);
					}
					else if (evaFuel.Amount < fuelAmount)
					{
						if (evaFuel.Capacity < fuelAmount)
						{
							evaFuel.Capacity = fuelAmount;
						}
						evaFuel.Amount = fuelAmount;
					}

					foreach (Supply supply in Profile.supplies)
					{
						if (supply.grantedOnRescue == 0.0)
							continue;

						PartResourceWrapper supplyResource = part.resources.Find(p => p.ResName == supply.resource);
						double resourceAmount = supply.grantedOnRescue * partCrewCount;
						if (supplyResource == null)
						{
							part.resources.AddResource(evaFuelName, resourceAmount, resourceAmount);
						}
						else if (supplyResource.Amount < resourceAmount)
						{
							if (supplyResource.Capacity < resourceAmount)
							{
								supplyResource.Capacity = resourceAmount;
							}
							supplyResource.Amount = resourceAmount;
						}
					}
				}
			}

			resHandler.ForceHandlerSync();
		}
	}
}
