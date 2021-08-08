using System;
using System.Collections.Generic;
using UnityEngine;

namespace KERBALISM
{


	public static class Profile
	{
		public const string NODENAME_PROFILE = "KERBALISM_PROFILE";
		public const string NODENAME_RULE = "RULE";
		public const string NODENAME_PROCESS = "PROCESS";
		public const string NODENAME_SUPPLY = "SUPPLY";
		public const string NODENAME_VIRTUAL_RESOURCE = "VIRTUAL_RESOURCE";
		public const string NODENAME_RESOURCE_HVL = "RESOURCE_HVL";
		public const string NODENAME_COMFORT = "COMFORT";


		public static List<KerbalRuleDefinition> rules;
		public static List<SupplyDefinition> supplies;          // supplies in the profile
		public static List<Process> processes;        // processes in the profile

		// node parsing
		private static void Nodeparse(ConfigNode profile_node)
		{
			// parse resources radiation occlusion definitions
			Radiation.PopulateResourcesOcclusionLibrary(profile_node.GetNodes(NODENAME_RESOURCE_HVL));

			// parse all VirtualResourceDefinition
			foreach (ConfigNode virtualResNode in profile_node.GetNodes(NODENAME_VIRTUAL_RESOURCE))
			{
				try
				{
					VirtualResourceDefinition vResDef = new VirtualResourceDefinition(virtualResNode);
					if (!VirtualResourceDefinition.definitions.ContainsKey(vResDef.name))
					{
						VirtualResourceDefinition.definitions.Add(vResDef.name, vResDef);
					}
				}
				catch (Exception e)
				{
					Lib.Log("failed to load virtual resource\n" + e.ToString(), Lib.LogLevel.Warning);
				}
			}

			// parse all rules
			foreach (ConfigNode ruleNode in profile_node.GetNodes(NODENAME_RULE))
			{
				try
				{
					KerbalRuleDefinition ruleDefinition = new KerbalRuleDefinition(ruleNode);

					// ignore duplicates
					if (rules.Find(k => k.name == ruleDefinition.name) == null)
					{
						// add the rule
						rules.Add(ruleDefinition);
					}
				}
				catch (Exception e)
				{
					Lib.Log("failed to load rule\n" + e.ToString(), Lib.LogLevel.Warning);
				}
			}

			// parse all supplies
			foreach (ConfigNode supply_node in profile_node.GetNodes(NODENAME_SUPPLY))
			{
				try
				{
					// parse supply
					SupplyDefinition supply = new SupplyDefinition(supply_node);

					// ignore duplicates
					if (supplies.Find(k => k.name == supply.name) == null)
					{
						// add the supply
						supplies.Add(supply);
					}
				}
				catch (Exception e)
				{
					Lib.Log("failed to load supply\n" + e.ToString(), Lib.LogLevel.Warning);
				}
			}

			// parse all processes
			foreach (ConfigNode process_node in profile_node.GetNodes(NODENAME_PROCESS))
			{
				try
				{
					// parse process
					Process process = new Process(process_node);

					// ignore duplicates
					if (processes.Find(k => k.name == process.name) == null)
					{
						// add the process
						processes.Add(process);
					}
				}
				catch (Exception e)
				{
					Lib.Log("failed to load process\n" + e.ToString(), Lib.LogLevel.Warning);
				}
			}

			foreach (ConfigNode process_node in profile_node.GetNodes(NODENAME_PROCESS))
			{
				try
				{
					// parse process
					Process process = new Process(process_node);

					// ignore duplicates
					if (processes.Find(k => k.name == process.name) == null)
					{
						// add the process
						processes.Add(process);
					}
				}
				catch (Exception e)
				{
					Lib.Log("failed to load process\n" + e.ToString(), Lib.LogLevel.Warning);
				}
			}

			foreach (ConfigNode comfortNode in profile_node.GetNodes(NODENAME_COMFORT))
			{
				try
				{
					ComfortDefinition.Load(comfortNode);;
				}
				catch (Exception e)
				{
					Lib.Log("failed to load comfort\n" + e.ToString(), Lib.LogLevel.Warning);
				}
			}
		}

		public static void Parse()
		{
			// initialize data
			rules = new List<KerbalRuleDefinition>();
			supplies = new List<SupplyDefinition>();
			processes = new List<Process>();

			// for each profile config
			ConfigNode[] profileNodes = Lib.ParseConfigs(NODENAME_PROFILE);
			ConfigNode profileNode;
			if (profileNodes.Length == 1)
			{
				profileNode = profileNodes[0];
			}
			else
			{
				profileNode = new ConfigNode();

				if (profileNodes.Length == 0)
				{
					ErrorManager.AddError(true, $"No profile found.",
					"You likely have forgotten to install KerbalismConfig or an alternative config pack in GameData.");
				}
				else if (profileNodes.Length > 1)
				{
					ErrorManager.AddError(true, $"Muliple profiles found.",
					"You likely have duplicates of KerbalismConfig or of an alternative config pack in GameData.");
				}
			}

			// parse nodes
			Nodeparse(profileNode);

			// do systems-specific setup
			PostParseSetup();

			// log info
			Lib.Log($"{supplies.Count} {NODENAME_SUPPLY} definitions found :");
			foreach (SupplyDefinition supply in supplies)
				Lib.Log($"- {supply.name}");

			Lib.Log($"{rules.Count} {NODENAME_RULE} definitions found :");
			foreach (KerbalRuleDefinition rule in rules)
				Lib.Log($"- {rule.name}");

			Lib.Log($"{processes.Count} {NODENAME_PROCESS} definitions found :");
			foreach (Process process in processes)
				Lib.Log($"- {process.name}");

			Lib.Log($"{VirtualResourceDefinition.definitions.Count} {NODENAME_VIRTUAL_RESOURCE} definitions found :");
			foreach (VirtualResourceDefinition resDef in VirtualResourceDefinition.definitions.Values)
				Lib.Log($"- {resDef.name}");
		}

		private static void PostParseSetup()
		{
			VesselResHandler.SetupDefinitions();
		}

		public static void Execute(Vessel v, VesselData vd, VesselResHandler resources, double elapsed_s)
		{
			foreach (Process process in processes)
			{
				process.Execute(vd, elapsed_s);
			}
		}

		public static void SetupEva(Part p)
		{
			foreach (SupplyDefinition supply in supplies)
			{
				supply.SetupEva(p);
			}
		}
	}
} // KERBALISM
