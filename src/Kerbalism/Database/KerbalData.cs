using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{
	public class KerbalData
	{
		public ProtoCrewMember stockKerbal;

		/// <summary> Initialized to true, then set to false the first first time the Kerbal is a crew of a newly launched vessel</summary>
		public bool isRescue;
		/// <summary> A generic flag to disable rules, for API / mod support use</summary>
		public bool disableRules;
		/// <summary> the eva kerbal died, and is now a floating body</summary>
		public bool isEvaDead;

		/// <summary> Check if rules should be enabled for this Kerbal </summary>
		public bool RulesEnabled => !isRescue && !disableRules && !isEvaDead;

		public List<KerbalRule> rules = new List<KerbalRule>();

		public KerbalData(ProtoCrewMember stockKerbal, bool initialize)
		{
			this.stockKerbal = stockKerbal;
			isRescue = true;
			disableRules = false;
			isEvaDead = false;

			foreach (KerbalRuleDefinition ruleDefinition in Profile.rules)
			{
				rules.Add(new KerbalRule(this, ruleDefinition, initialize));
			}
		}

		public static KerbalData Load(KerbalRoster stockRoster, ConfigNode kerbalNode)
		{
			string name = Lib.ConfigValue(kerbalNode, "name", string.Empty);
			ProtoCrewMember stockKerbal = stockRoster[name];

			if (stockKerbal == null)
				return null;

			KerbalData kerbalData = new KerbalData(stockKerbal, false)
			{
				isRescue = Lib.ConfigValue(kerbalNode, "isRescue", true),
				disableRules = Lib.ConfigValue(kerbalNode, "disableRules", false),
				isEvaDead = Lib.ConfigValue(kerbalNode, "isEvaDead", false)
			};

			KerbalRule.Load(kerbalData, kerbalNode);

			return kerbalData;
		}

		public void Save(ConfigNode parentNode)
		{
			ConfigNode kerbalNode = parentNode.AddNode("KERBAL");

			kerbalNode.AddValue("name", stockKerbal.name);
			kerbalNode.AddValue("isRescue", isRescue);
			kerbalNode.AddValue("disableRules", disableRules);
			kerbalNode.AddValue("isEvaDead", isEvaDead);

			KerbalRule.Save(this, kerbalNode);
		}

		public void OnFixedUpdate(VesselDataBase vesselData, double elapsedSec)
		{
			foreach (KerbalRule rule in rules)
			{
				rule.OnFixedUpdate(vesselData, elapsedSec);
			}
		}

		public void OnVesselRecovered()
		{
			// set roster status of eva dead kerbals
			if (isEvaDead)
			{
				stockKerbal.Die();
			}

			foreach (KerbalRule rule in rules)
			{
				rule.OnVesselRecovered();
			}
		}

		/// <summary>
		/// Kill a kerbal. If the kerbal is on EVA, it will be set to our special "eva dead" state and really killed when recovered or manually deleted.
		/// Works with unassigned Kerbals, and trigger the stock reputation penalty in career.
		/// </summary>
		public void Kill()
		{
			if (isEvaDead || stockKerbal.rosterStatus == ProtoCrewMember.RosterStatus.Dead)
			{
				return;
			}

			if (stockKerbal.rosterStatus == ProtoCrewMember.RosterStatus.Available)
			{
				stockKerbal.Die();
				return;
			}

			if (stockKerbal.rosterStatus == ProtoCrewMember.RosterStatus.Assigned)
			{
				// if the kerbal is assigned, but not on any part, it is on EVA
				if (!TryGetKerbalPart(out Vessel vessel, out Part part, out ProtoPartSnapshot protoPart))
				{
					Lib.Log($"Can't kill assigned Kerbal {stockKerbal.name} : the part it is on can't be found", Lib.LogLevel.Error);
					return;
				}

				if (vessel.isEVA)
				{
					// if the kerbal is on EVA, flag it as dead (see Modules\StockModules\KerbalEVAHandler)
					// ProtoCrewMember.Die() will be called if the kerbal is recovered.
					isEvaDead = true;
					vessel.vesselName += "'s body";
					return;
				}

				if (part != null)
				{
					part.RemoveCrewmember(stockKerbal);
				}
				else
				{
					protoPart.RemoveCrew(stockKerbal);
					protoPart.pVesselRef.RemoveCrew(stockKerbal);
				}

				vessel.RemoveCrew(stockKerbal);
				vessel.RebuildCrewList();
				stockKerbal.Die();
			}
		}

		private bool TryGetKerbalPart(out Vessel vessel, out Part part, out ProtoPartSnapshot protoPart)
		{
			foreach (Vessel flightVessel in FlightGlobals.Vessels)
			{
				if (flightVessel.GetVesselCrew().Count == 0)
					continue;

				if (flightVessel.loaded)
				{
					foreach (Part vesselPart in flightVessel.parts)
					{
						foreach (ProtoCrewMember partCrew in vesselPart.protoModuleCrew)
						{
							if (partCrew == stockKerbal)
							{
								vessel = flightVessel;
								part = vesselPart;
								protoPart = null;
								return true;
							}
						}
					}
				}
				else
				{
					foreach (ProtoPartSnapshot vesselPart in flightVessel.protoVessel.protoPartSnapshots)
					{
						foreach (ProtoCrewMember partCrew in vesselPart.protoModuleCrew)
						{
							if (partCrew == stockKerbal)
							{
								vessel = flightVessel;
								part = null;
								protoPart = vesselPart;
								return true;
							}
						}
					}
				}
			}

			vessel = null;
			part = null;
			protoPart = null;
			return false;
		}

		public override string ToString()
		{
			return stockKerbal.name;
		}
	}
}

