using System.Collections;

namespace KERBALISM
{
	public class VesselProcesses
	{
		private const string NODENAME_PROCESSES = "PROCESSES";

		private Process[] processes;

		IEnumerator GetEnumerator() => processes.GetEnumerator();

		public Process this[int index] => processes[index];

		public VesselProcesses()
		{
			processes = new Process[ProcessDefinition.definitions.Count];

			for (int i = 0; i < ProcessDefinition.definitions.Count; i++)
			{
				processes[i] = new Process(ProcessDefinition.definitions[i]);
			}
		}

		public void Load(ConfigNode vesselDataNode)
		{
			ConfigNode[] processNodes = vesselDataNode.GetNode(NODENAME_PROCESSES)?.GetNodes();
			if (processNodes == null)
				return;

			for (int i = 0; i < processNodes.Length; i++)
			{
				ConfigNode processNode = processNodes[i];
				string processName = processNode.name.NodeNameToKey();
				if (processName == processes[i].definition.name)
				{
					processes[i].Load(processNode);
				}
				else
				{
					foreach (Process process in processes)
					{
						if (processName == process.definition.name)
						{
							process.Load(processNode);
							break;
						}
					}
				}
			}
		}

		public void Save(ConfigNode vesselDataNode)
		{
			ConfigNode processesNode = vesselDataNode.AddNode(NODENAME_PROCESSES);

			foreach (Process process in processes)
			{
				ConfigNode processNode = processesNode.AddNode(process.definition.name.KeyToNodeName());
				process.Save(processNode);
			}
		}

		public void RegisterProcessController(ProcessControllerHandler processController)
		{
			processes[processController.definition.processDefinition.definitionIndex].controllers.Add(processController);
		}

		public void ResetBeforeModulesUpdate()
		{
			foreach (Process process in processes)
			{
				process.ResetBeforeModulesUpdate();
			}
		}

		public void Execute(VesselDataBase vd)
		{
			foreach (Process process in processes)
			{
				process.Execute(vd);
			}
		}
	}
}
