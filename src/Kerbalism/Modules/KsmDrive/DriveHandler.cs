using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;

namespace KERBALISM
{
	// TODO : get ride of that mess. Ideally, data storage should be done through an interface
	// that is implemented both in the drive module and in the experiment module (for private drives), mapped to a common implementation
	// in a separate class, something like this :
	// ModuleKsmDrive : IDataStorage
	// ModuleKsmExperiment : IDataStorage
	// class DataStore -> actual logic implementation, all drives have one instance and experiments have one optionally,
	// implement the methods of IDataStorage


	public sealed class DriveHandler : KsmModuleHandler<ModuleKsmDrive, DriveHandler, DriveDefinition>, IKsmModuleHandlerLateInit
	{
		#region FIELDS

		public Dictionary<SubjectData, File> files = new Dictionary<SubjectData, File>();
		public Dictionary<SubjectData, Sample> samples = new Dictionary<SubjectData, Sample>();
		public Dictionary<string, bool> fileSendFlags = new Dictionary<string, bool>();
		public double dataCapacity;
		public int sampleCapacity;
		public bool isPrivate;

		#endregion

		#region LIFECYCLE

		/// <summary> Auto-Assign hard drive storage capacity based on the parts position in the tech tree and part cost </summary>
		public void OnLatePrefabInit(AvailablePart availablePart)
		{
			// don't touch drives assigned to an experiment
			if (!string.IsNullOrEmpty(modulePrefab.experiment_id))
				return;

			// no auto-assigning necessary
			if (loadedModule.sampleCapacity != ModuleKsmDrive.CAPACITY_AUTO && loadedModule.dataCapacity != ModuleKsmDrive.CAPACITY_AUTO)
				return;

			// get cumulative science cost for this part
			double maxScienceCost = 0;
			double tier = 1.0;
			double maxTier = 1.0;

			// find start node and max. science cost
			ProtoRDNode node = null;
			ProtoRDNode maxNode = null;

			foreach (var n in AssetBase.RnDTechTree.GetTreeNodes())
			{
				if (n.tech.scienceCost > maxScienceCost)
				{
					maxScienceCost = n.tech.scienceCost;
					maxNode = n;
				}
				if (availablePart.TechRequired == n.tech.techID)
					node = n;
			}

			if (node == null)
			{
				Lib.Log($"{availablePart.partPrefab.partInfo.name}: part not found in tech tree, skipping auto assignment", Lib.LogLevel.Warning);
				return;
			}

			// add up science cost from start node and all the parents
			// (we ignore teh requirement to unlock multiple nodes before this one)
			while (node.parents.Count > 0)
			{
				tier++;
				node = node.parents[0];
			}

			// determine max science cost and max tier
			while (maxNode.parents.Count > 0)
			{
				maxTier++;
				maxNode = maxNode.parents[0];
			}

			// see https://www.desmos.com/calculator/9oiyzsdxzv
			//
			// f = (tier / max. tier)^3
			// capacity = f * max. capacity
			// max. capacity factor 3GB (remember storages can be tweaked to 4x the base size, deep horizons had 8GB)
			// with the variation effects, this caps out at about 10GB.

			// add some part variance based on part cost
			var t = tier - 1;
			t += (availablePart.cost - 5000) / 10000;

			double f = Math.Pow(t / maxTier, 3);
			double dataCapacity = f * 3000;

			dataCapacity = (int)(dataCapacity * 4) / 4.0; // set to a multiple of 0.25
			if (dataCapacity > 2)
				dataCapacity = (int)(dataCapacity * 2) / 2; // set to a multiple of 0.5
			if (dataCapacity > 5)
				dataCapacity = (int)(dataCapacity); // set to a multiple of 1
			if (dataCapacity > 25)
				dataCapacity = (int)(dataCapacity / 5) * 5; // set to a multiple of 5
			if (dataCapacity > 250)
				dataCapacity = (int)(dataCapacity / 25) * 25; // set to a multiple of 25
			if (dataCapacity > 250)
				dataCapacity = (int)(dataCapacity / 50) * 50; // set to a multiple of 50
			if (dataCapacity > 1000)
				dataCapacity = (int)(dataCapacity / 250) * 250; // set to a multiple of 250

			dataCapacity = Math.Max(dataCapacity, 0.25); // 0.25 minimum

			double sampleCapacity = tier / maxTier * 8;
			sampleCapacity = Math.Max(sampleCapacity, 1); // 1 minimum

			if (modulePrefab.dataCapacity == ModuleKsmDrive.CAPACITY_AUTO)
			{
				modulePrefab.dataCapacity = dataCapacity;
				Lib.Log($"{availablePart.partPrefab.partInfo.name}: tier {tier}/{maxTier} part cost {availablePart.cost.ToString("F0")} data cap. {dataCapacity.ToString("F2")}", Lib.LogLevel.Message);
			}
			if (modulePrefab.sampleCapacity == ModuleKsmDrive.CAPACITY_AUTO)
			{
				modulePrefab.sampleCapacity = (int)Math.Round(sampleCapacity);
				Lib.Log($"{availablePart.partPrefab.partInfo.name}: tier {tier}/{maxTier} part cost {availablePart.cost.ToString("F0")} sample cap. {modulePrefab.sampleCapacity}", Lib.LogLevel.Message);
			}
		}

		public override void OnStart()
		{
			// modulePrefab will be null for transmit buffer drives
			if (modulePrefab != null)
			{
				dataCapacity = modulePrefab.effectiveDataCapacity;
				sampleCapacity = modulePrefab.effectiveSampleCapacity;
				isPrivate = !string.IsNullOrEmpty(modulePrefab.experiment_id);
			}
			else
			{
				dataCapacity = 0.0;
				sampleCapacity = 0;
				isPrivate = false;
			}

			if (isPrivate)
			{
				isPrivate = false;
				foreach (ModuleHandler handler in partData.modules)
				{
					// TODO : move experiment_id to the definition
					if (handler is ExperimentHandler experimentHandler && experimentHandler.definition.ExpInfo.ExperimentId == modulePrefab.experiment_id)
					{
						isPrivate = true;
						experimentHandler.PrivateDrive = this;
					}
				}
			}
		}

		public override void OnLoad(ConfigNode node)
		{
			// parse science  files
			files = new Dictionary<SubjectData, File>();
			ConfigNode filesNode = node.GetNode("FILES");
			if (filesNode != null)
			{
				foreach (var file_node in filesNode.GetNodes())
				{
					string subject_id = DB.FromSafeKey(file_node.name);
					File file = File.Load(subject_id, file_node);
					if (file != null)
					{
						if (files.ContainsKey(file.subjectData))
						{
							Lib.Log("discarding duplicate subject " + file.subjectData, Lib.LogLevel.Warning);
						}
						else
						{
							files.Add(file.subjectData, file);
							file.subjectData.AddDataCollectedInFlight(file.size);
						}
					}
				}
			}

			// parse science samples
			samples = new Dictionary<SubjectData, Sample>();
			ConfigNode samplesNode = node.GetNode("SAMPLES");
			if (samplesNode != null)
			{
				foreach (var sample_node in samplesNode.GetNodes())
				{
					string subject_id = DB.FromSafeKey(sample_node.name);
					Sample sample = Sample.Load(subject_id, sample_node);
					if (sample != null)
					{
						samples.Add(sample.subjectData, sample);
						sample.subjectData.AddDataCollectedInFlight(sample.size);
					}
				}
			}

			// parse capacities. be generous with default values for backwards
			// compatibility (drives had unlimited storage before this)
			dataCapacity = Lib.ConfigValue(node, "dataCapacity", 100000.0);
			sampleCapacity = Lib.ConfigValue(node, "sampleCapacity", 1000);

			fileSendFlags = new Dictionary<string, bool>();
			string fileNames = Lib.ConfigValue(node, "sendFileNames", string.Empty);
			foreach (string fileName in Lib.Tokenize(fileNames, ','))
			{
				Send(fileName, true);
			}
		}

		public override void OnSave(ConfigNode node)
		{
			// save science files
			bool hasFiles = false;
			ConfigNode filesNode = new ConfigNode("FILES");
			foreach (File file in files.Values)
			{
				file.Save(filesNode.AddNode(DB.ToSafeKey(file.subjectData.Id)));
				hasFiles = true;
			}

			if (hasFiles)
				node.AddNode(filesNode);

			// save science samples
			bool hasSamples = false;
			ConfigNode samplesNode = new ConfigNode("SAMPLES");
			foreach (Sample sample in samples.Values)
			{
				sample.Save(samplesNode.AddNode(DB.ToSafeKey(sample.subjectData.Id)));
				hasSamples = true;
			}

			if (hasSamples)
				node.AddNode(samplesNode);

			node.AddValue("dataCapacity", dataCapacity);
			node.AddValue("sampleCapacity", sampleCapacity);

			string fileNames = string.Empty;
			foreach (string subjectId in fileSendFlags.Keys)
			{
				if (fileNames.Length > 0) fileNames += ",";
				fileNames += subjectId;
			}
			node.AddValue("sendFileNames", fileNames);
		}

		public override void OnFlightPartWillDie()
		{
			DeleteAllData();
		}

		#endregion

		public static double StoreFile(VesselData vd, SubjectData subjectData, double size, bool include_private = false)
		{
			if (size <= 0.0)
				return 0.0;

			// store what we can
			TryStoreFile(vd.TransmitBuffer, subjectData, ref size);

			if (size <= 0.0)
				return size;

			foreach (var d in GetDrives(vd, include_private))
			{
				if (!TryStoreFile(d, subjectData, ref size))
					break;
			}

			return size;
		}

		private static bool TryStoreFile(DriveHandler drive, SubjectData subjectData, ref double size)
		{
			var available = drive.FileCapacityAvailable();
			var chunk = Math.Min(size, available);
			if (!drive.RecordFile(subjectData, chunk, true))
				return false;
			size -= chunk;

			if (size <= 0.0)
				return false;

			return true;
		}

		// add science data, creating new file or incrementing existing one
		public bool RecordFile(SubjectData subjectData, double amount, bool allowImmediateTransmission = true, bool useStockCrediting = false)
		{
			if (dataCapacity >= 0 && FilesSize() + amount > dataCapacity)
				return false;

			// create new data or get existing one
			File file;
			if (!files.TryGetValue(subjectData, out file))
			{
				file = new File(subjectData, 0.0, useStockCrediting);
				files.Add(subjectData, file);

				if (!allowImmediateTransmission) Send(subjectData.Id, false);
			}

			// increase amount of data stored in the file
			file.size += amount;

			// keep track of data collected
			subjectData.AddDataCollectedInFlight(amount);

			return true;
		}

		public void Send(string subjectId, bool send)
		{
			if (!fileSendFlags.ContainsKey(subjectId)) fileSendFlags.Add(subjectId, send);
			else fileSendFlags[subjectId] = send;
		}

		public bool GetFileSend(string subjectId)
		{
			if (!fileSendFlags.ContainsKey(subjectId)) return PreferencesScience.Instance.transmitScience;
			return fileSendFlags[subjectId];
		}

		// add science sample, creating new sample or incrementing existing one
		public bool RecordSample(SubjectData subjectData, double amount, double mass, bool useStockCrediting = false)
		{
			int currentSampleSlots = SamplesSize();
			if (sampleCapacity >= 0)
			{
				if (!samples.ContainsKey(subjectData) && currentSampleSlots >= sampleCapacity)
				{
					// can't take a new sample if we're already at capacity
					return false;
				}
			}

			Sample sample;
			if (samples.ContainsKey(subjectData) && sampleCapacity >= 0)
			{
				// test if adding the amount to the sample would exceed our capacity
				sample = samples[subjectData];

				int existingSampleSlots = Lib.SampleSizeToSlots(sample.size);
				int newSampleSlots = Lib.SampleSizeToSlots(sample.size + amount);
				if (currentSampleSlots - existingSampleSlots + newSampleSlots > sampleCapacity)
					return false;
			}

			// create new data or get existing one
			if (!samples.TryGetValue(subjectData, out sample))
			{
				sample = new Sample(subjectData, 0.0, useStockCrediting);
				sample.analyze = PreferencesScience.Instance.analyzeSamples;
				samples.Add(subjectData, sample);
			}

			// increase amount of data stored in the sample
			sample.size += amount;
			sample.mass += mass;

			// keep track of data collected
			subjectData.AddDataCollectedInFlight(amount);

			return true;
		}

		// remove science data, deleting the file when it is empty
		public void DeleteFile(SubjectData subjectData, double amount = 0.0)
		{
			// get data
			File file;
			if (files.TryGetValue(subjectData, out file))
			{
				// decrease amount of data stored in the file
				if (amount == 0.0)
					amount = file.size;
				else
					amount = Math.Min(amount, file.size);

				file.size -= amount;

				// keep track of data collected
				subjectData.RemoveDataCollectedInFlight(amount);

				// remove file if empty
				if (file.size <= 0.0) files.Remove(subjectData);
			}
		}

		// remove science sample, deleting the sample when it is empty
		public double DeleteSample(SubjectData subjectData, double amount = 0.0)
		{
			// get data
			Sample sample;
			if (samples.TryGetValue(subjectData, out sample))
			{
				// decrease amount of data stored in the sample
				if (amount == 0.0)
					amount = sample.size;
				else
					amount = Math.Min(amount, sample.size);

				double massDelta = sample.mass * amount / sample.size;
				sample.size -= amount;
				sample.mass -= massDelta;

				// keep track of data collected
				subjectData.RemoveDataCollectedInFlight(amount);

				// remove sample if empty
				if (sample.size <= 0.0) samples.Remove(subjectData);

				return massDelta;
			}
			return 0.0;
		}

		// set analyze flag for a sample
		public void Analyze(SubjectData subjectData, bool b)
		{
			Sample sample;
			if (samples.TryGetValue(subjectData, out sample))
			{
				sample.analyze = b;
			}
		}

		// move all data to another drive
		public bool Move(DriveHandler destination, bool moveSamples)
		{
			bool result = true;

			// copy files
			List<SubjectData> filesList = new List<SubjectData>();
			foreach (File file in files.Values)
			{
				double size = Math.Min(file.size, destination.FileCapacityAvailable());
				if (destination.RecordFile(file.subjectData, size, true, file.useStockCrediting))
				{
					file.size -= size;
					file.subjectData.RemoveDataCollectedInFlight(size);
					if (file.size < double.Epsilon)
					{
						filesList.Add(file.subjectData);
					}
					else
					{
						result = false;
						break;
					}
				}
				else
				{
					result = false;
					break;
				}
			}
			foreach (SubjectData id in filesList) files.Remove(id);

			if (!moveSamples) return result;

			// move samples
			List<SubjectData> samplesList = new List<SubjectData>();
			foreach (Sample sample in samples.Values)
			{
				double size = Math.Min(sample.size, destination.SampleCapacityAvailable(sample.subjectData));
				if (size < double.Epsilon)
				{
					result = false;
					break;
				}

				double mass = sample.mass * (sample.size / size);
				if (destination.RecordSample(sample.subjectData, size, mass, sample.useStockCrediting))
				{
					sample.size -= size;
					sample.subjectData.RemoveDataCollectedInFlight(size);
					sample.mass -= mass;

					if (sample.size < double.Epsilon)
					{
						samplesList.Add(sample.subjectData);
					}
					else
					{
						result = false;
						break;
					}
				}
				else
				{
					result = false;
					break;
				}
			}
			foreach (var id in samplesList) samples.Remove(id);

			return result; // true if everything was moved, false otherwise
		}

		public double FileCapacityAvailable()
		{
			if (dataCapacity < 0) return double.MaxValue;
			return Math.Max(dataCapacity - FilesSize(), 0.0); // clamp to 0 due to fp precision in FilesSize()
		}

		public double FilesSize()
		{
			double amount = 0.0;
			foreach (File file in files.Values)
			{
				amount += file.size;
			}
			return amount;
		}

		public double SampleCapacityAvailable(SubjectData subject = null)
		{
			if (sampleCapacity < 0) return double.MaxValue;

			double result = Lib.SlotsToSampleSize(sampleCapacity - SamplesSize());
			if (subject != null && samples.ContainsKey(subject))
			{
				int slotsForMyFile = Lib.SampleSizeToSlots(samples[subject].size);
				double amountLostToSlotting = Lib.SlotsToSampleSize(slotsForMyFile) - samples[subject].size;
				result += amountLostToSlotting;
			}
			return result;
		}

		public int SamplesSize()
		{
			int amount = 0;
			foreach (Sample sample in samples.Values)
			{
				amount += Lib.SampleSizeToSlots(sample.size);
			}
			return amount;
		}

		// return size of data stored in Mb (including samples)
		public string Size()
		{
			var f = FilesSize();
			var s = SamplesSize();
			var result = f > double.Epsilon ? Lib.HumanReadableDataSize(f) : "";
			if (result.Length > 0) result += " ";
			if (s > 0) result += Lib.HumanReadableSampleSize(s);
			return result;
		}

		public bool Empty()
		{
			return files.Count + samples.Count == 0;
		}

		// transfer data from a vessel to a drive
		public static bool Transfer(VesselData src, DriveHandler dst, bool samples)
		{
			double dataAmount = 0.0;
			int sampleSlots = 0;
			foreach (var drive in GetDrives(src, true))
			{
				dataAmount += drive.FilesSize();
				sampleSlots += drive.SamplesSize();
			}

			if (dataAmount < double.Epsilon && (sampleSlots == 0 || !samples))
				return true;

			// get drives
			var allSrc = GetDrives(src, true);

			bool allMoved = true;
			foreach (var a in allSrc)
			{
				if (a.Move(dst, samples))
				{
					allMoved = true;
					break;
				}
			}

			return allMoved;
		}

		// transfer data from a drive to a vessel
		public static bool Transfer(DriveHandler drive, VesselData dst, bool samples)
		{
			double dataAmount = drive.FilesSize();
			int sampleSlots = drive.SamplesSize();

			if (dataAmount < double.Epsilon && (sampleSlots == 0 || !samples))
				return true;

			// get drives
			var allDst = GetDrives(dst);

			bool allMoved = true;
			foreach (var b in allDst)
			{
				if (drive.Move(b, samples))
				{
					allMoved = true;
					break;
				}
			}

			return allMoved;
		}

		// transfer data between two vessels
		public static void Transfer(VesselData src, VesselData dst, bool samples)
		{
			double dataAmount = 0.0;
			int sampleSlots = 0;
			foreach (var drive in GetDrives(src, true))
			{
				dataAmount += drive.FilesSize();
				sampleSlots += drive.SamplesSize();
			}

			if (dataAmount < double.Epsilon && (sampleSlots == 0 || !samples))
				return;

			var allSrc = GetDrives(src, true);
			bool allMoved = false;
			foreach (var a in allSrc)
			{
				if (Transfer(a, dst, samples))
				{
					allMoved = true;
					break;
				}
			}

			// inform the user
			if (allMoved)
				Message.Post
				(
					Lib.HumanReadableDataSize(dataAmount) + " " + Local.Science_ofdatatransfer,
				 	Lib.BuildString(Local.Generic_FROM, " <b>", src.VesselName, "</b> ", Local.Generic_TO, " <b>", dst.VesselName, "</b>")
				);
			else
				Message.Post
				(
					Lib.Color(Lib.BuildString("WARNING: not evering copied"), Lib.Kolor.Red, true),
					Lib.BuildString(Local.Generic_FROM, " <b>", src.VesselName, "</b> ", Local.Generic_TO, " <b>", dst.VesselName, "</b>")
				);
		}

		/// <summary> delete all files/samples in the drive</summary>
		public void DeleteAllData()
		{
			foreach (File file in files.Values)
				file.subjectData.RemoveDataCollectedInFlight(file.size);

			foreach (Sample sample in samples.Values)
				sample.subjectData.RemoveDataCollectedInFlight(sample.size);

			files.Clear();
			samples.Clear();
		}

		/// <summary> delete all files/samples in the vessel drives</summary>
		public static void DeleteDrivesData(VesselDataBase vd)
		{
			foreach (DriveHandler driveData in vd.Parts.AllModulesOfType<DriveHandler>())
			{
				driveData.DeleteAllData();
			}
		}

		public static IEnumerable<DriveHandler> GetDrives (VesselDataBase vd, bool includePrivate = false)
		{
			if (!includePrivate)
			{
				return vd.Parts.AllModulesOfType<DriveHandler>(p => !p.isPrivate);
			}
			else
			{
				return vd.Parts.AllModulesOfType<DriveHandler>();
			}
		}

		public static void GetCapacity(VesselDataBase vesseldata, out double free_capacity, out double total_capacity)
		{
			free_capacity = 0;
			total_capacity = 0;
			if (Features.Science)
			{
				foreach (var drive in GetDrives(vesseldata))
				{
					if (drive.dataCapacity < 0 || free_capacity < 0)
					{
						free_capacity = -1;
					}
					else
					{
						free_capacity += drive.FileCapacityAvailable();
						total_capacity += drive.dataCapacity;
					}
				}

				if (free_capacity < 0)
				{
					free_capacity = double.MaxValue;
					total_capacity = double.MaxValue;
				}
			}
		}

		/// <summary> Get a drive for storing files. Will return null if there are no drives on the vessel </summary>
		public static DriveHandler FileDrive(VesselDataBase vesselData, double size = 0.0)
		{
			DriveHandler result = null;
			foreach (var drive in GetDrives(vesselData))
			{
				if (result == null)
				{
					result = drive;
					if (size > 0.0 && result.FileCapacityAvailable() >= size)
						return result;
					continue;
				}

				if (size > 0.0 && drive.FileCapacityAvailable() >= size)
				{
					return drive;
				}

				// if we're not looking for a minimum capacity, look for the biggest drive
				if (drive.dataCapacity > result.dataCapacity)
				{
					result = drive;
				}
			}
			return result;
		}

		/// <summary> Get a drive for storing samples. Will return null if there are no drives on the vessel </summary>
		public static DriveHandler SampleDrive(VesselDataBase vesselData, double size = 0, SubjectData subject = null)
		{
			DriveHandler result = null;
			foreach (var drive in GetDrives(vesselData))
			{
				if (result == null)
				{
					result = drive;
					continue;
				}

				double available = drive.SampleCapacityAvailable(subject);
				if (size > double.Epsilon && available < size)
					continue;
				if (available > result.SampleCapacityAvailable(subject))
					result = drive;
			}
			return result;
		}
	}


} // KERBALISM

