using System;
using System.Collections.Generic;

namespace KERBALISM
{


	public sealed class Supply
	{
		public enum SupplyState { Empty, BelowThreshold, AboveThreshold, Full }
		public enum WarningMode { Disabled, OnEmpty, OnFull}

		public string resource;         // name of resource
		public double evaCapacity;      // how much resource capacity to add on eva
		public double grantedOnRescue;  // how much resource to gift to rescue missions

		public double levelThreshold;   // [0;1] resource level used to trigger low / fill messages

		public WarningMode warningUIMode; // determine if/when yellow/red UI indicators are shown
		public bool warnOnlyIfManned;   // if true, messages and UI warnings are not shown if there is nobody on board

		// message shown when going below threshold
		public string lowMessage;
		public Severity lowSeverity;
		public bool lowStopWarp;

		// message shown when going above threshold
		public string fillMessage;
		public Severity fillSeverity;
		public bool fillStopWarp;

		// message shown when amount reach zero
		public string emptyMessage;
		public Severity emptySeverity;
		public bool emptyStopWarp;

		// message shown when amount reach capacity
		public string fullMessage;
		public Severity fullSeverity;
		public bool fullStopWarp;

		public Supply(ConfigNode node)
		{
			resource = Lib.ConfigValue(node, "name", string.Empty);
			// check that resource exist
			if (resource.Length == 0)
				throw new Exception("skipping resource-less supply");
			if (Lib.GetDefinition(resource) == null)
				throw new Exception("resource " + resource + " doesn't exist");

			evaCapacity = Lib.ConfigValue(node, "evaCapacity", 0.0);
			grantedOnRescue = Lib.ConfigValue(node, "grantedOnRescue", 0.0);

			levelThreshold = Lib.ConfigValue(node, "levelThreshold", 0.15);

			warningUIMode = Lib.ConfigEnum(node, "warningUIMode", WarningMode.OnEmpty);
			warnOnlyIfManned = Lib.ConfigValue(node, "warnOnlyIfManned", true);

			lowMessage = Lib.ConfigValue(node, "lowMessage", string.Empty);
			if (lowMessage.Length > 0 && lowMessage[0] == '#') Lib.Log("Broken translation: " + lowMessage);
			lowSeverity = Lib.ConfigValue(node, "lowSeverity", Severity.warning);
			lowStopWarp = Lib.ConfigValue(node, "lowStopWarp", false);

			fillMessage = Lib.ConfigValue(node, "fillMessage", string.Empty);
			if (fillMessage.Length > 0 && fillMessage[0] == '#') Lib.Log("Broken translation: " + fillMessage);
			fillSeverity = Lib.ConfigValue(node, "fillSeverity", Severity.relax);
			fillStopWarp = Lib.ConfigValue(node, "fillStopWarp", false);

			emptyMessage = Lib.ConfigValue(node, "emptyMessage", string.Empty);
			if (emptyMessage.Length > 0 && emptyMessage[0] == '#') Lib.Log("Broken translation: " + emptyMessage);
			emptySeverity = Lib.ConfigValue(node, "emptySeverity", Severity.danger);
			emptyStopWarp = Lib.ConfigValue(node, "emptyStopWarp", false);

			fullMessage = Lib.ConfigValue(node, "fullMessage", string.Empty);
			if (fullMessage.Length > 0 && fullMessage[0] == '#') Lib.Log("Broken translation: " + fullMessage);
			fullSeverity = Lib.ConfigValue(node, "fullSeverity", Severity.danger);
			fullStopWarp = Lib.ConfigValue(node, "fullStopWarp", false);
		}

		private static Dictionary<string, SupplyState> CreateStateDictionary(VesselResHandler resHandler)
		{
			Dictionary<string, SupplyState> supplies = new Dictionary<string, SupplyState>(Profile.supplies.Count);

			foreach (Supply supply in Profile.supplies)
			{
				double level = resHandler.GetResource(supply.resource).Level;

				if (level == 0.0)
					supplies[supply.resource] = SupplyState.Empty;
				else if (level == 1.0)
					supplies[supply.resource] = SupplyState.Full;
				else if (level < supply.levelThreshold)
					supplies[supply.resource] = SupplyState.BelowThreshold;
				else
					supplies[supply.resource] = SupplyState.AboveThreshold;
			}

			return supplies;
		}

		public static void SendMessages(VesselData vd)
		{
			if (vd.supplies == null)
			{
				vd.supplies = CreateStateDictionary(vd.ResHandler);
			}

			// execute all supplies
			foreach (Supply supply in Profile.supplies)
			{
				bool notify =
					(supply.resource == "ElectricCharge" ? vd.cfg_ec : vd.cfg_supply)
					&& (supply.warnOnlyIfManned ? vd.CrewCount > 0 : true);

				double level = vd.ResHandler.GetResource(supply.resource).Level;

				SupplyState currentState = vd.supplies[supply.resource];

				if (level < 1e-6)
				{
					switch (currentState)
					{
						case SupplyState.Empty:
							continue;

						case SupplyState.BelowThreshold:
						case SupplyState.AboveThreshold:
						case SupplyState.Full:
							if (notify && supply.emptyMessage != string.Empty)
							{
								uint variant = vd.CrewCount > 0 ? 0 : 1u; // manned/probe message variant
								Message.Post(supply.emptySeverity, Lib.ExpandMsg(supply.emptyMessage, vd.Vessel, null, variant), "", supply.emptyStopWarp);
							}
							break;
					}

					vd.supplies[supply.resource] = SupplyState.Empty;
				}
				else if (level > 1.0 - 1e-6)
				{
					switch (currentState)
					{
						case SupplyState.Full:
							continue;

						case SupplyState.Empty:
						case SupplyState.BelowThreshold:
						case SupplyState.AboveThreshold:
							if (notify && supply.fullMessage != string.Empty)
							{
								uint variant = vd.CrewCount > 0 ? 0 : 1u; // manned/probe message variant
								Message.Post(supply.fullSeverity, Lib.ExpandMsg(supply.fullMessage, vd.Vessel, null, variant), "", supply.fullStopWarp);
							}
							break;
					}

					vd.supplies[supply.resource] = SupplyState.Full;
				}
				else if (level < supply.levelThreshold)
				{
					switch (currentState)
					{
						case SupplyState.BelowThreshold:
							continue;

						case SupplyState.Empty:
							break;

						case SupplyState.AboveThreshold:
						case SupplyState.Full:
							if (notify && supply.lowMessage != string.Empty)
							{
								uint variant = vd.CrewCount > 0 ? 0 : 1u; // manned/probe message variant
								Message.Post(supply.lowSeverity, Lib.ExpandMsg(supply.lowMessage, vd.Vessel, null, variant), "", supply.lowStopWarp);
							}
							break;
					}

					vd.supplies[supply.resource] = SupplyState.BelowThreshold;
				}
				else
				{
					switch (currentState)
					{
						case SupplyState.AboveThreshold:
							continue;

						case SupplyState.Full:
							break;

						case SupplyState.Empty:
						case SupplyState.BelowThreshold:
							if (notify && supply.fillMessage != string.Empty)
							{
								uint variant = vd.CrewCount > 0 ? 0 : 1u; // manned/probe message variant
								Message.Post(supply.fillSeverity, Lib.ExpandMsg(supply.fillMessage, vd.Vessel, null, variant), "", supply.fillStopWarp);
							}
							break;
					}

					vd.supplies[supply.resource] = SupplyState.AboveThreshold;
				}
			}
		}


		public void SetupEva(Part p)
		{
			// do nothing if no resource on eva
			if (evaCapacity == 0.0) return;

			// create new resource capacity in the eva kerbal
			Lib.AddResource(p, resource, 0.0, evaCapacity);
		}
	}


} // KERBALISM
