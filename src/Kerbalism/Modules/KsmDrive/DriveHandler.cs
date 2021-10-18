using System;
using System.Collections.Generic;

namespace KERBALISM
{


	public class DriveHandler : KsmModuleHandler<ModuleKsmDrive, DriveHandler, DriveDefinition>//, IKsmModuleHandlerLateInit
	{
		public abstract class KsmScienceData
		{
			private const string VALUENAME_STOCKID = "stockId";

			public enum DataType { file, sample }

			/// <summary>subject</summary>
			public SubjectData SubjectData { get; protected set; }

			/// <summary>data size in Mb. For samples, this is also a representation of the sample volume </summary>
			public double Size { get; protected set; }

			/// <summary>Force the stock crediting formula to be applied on recovery. will be true if the file was created by the hijacker. </summary>
			public bool UseStockCrediting { get; protected set; }

			/// <summary>randomized result text</summary>
			public string ResultText { get; protected set; }

			/// <summary>the drive this is stored on </summary>
			public DriveHandler Drive { get; protected set; }

			public bool IsDeleted => Drive == null;

			protected KsmScienceData(DriveHandler drive, SubjectData subjectData, bool generateResultText = true, string resultText = null, bool useStockCrediting = false)
			{
				Drive = drive;
				SubjectData = subjectData;
				UseStockCrediting = useStockCrediting;

				if (generateResultText)
				{
					if (string.IsNullOrEmpty(resultText))
						ResultText = ResearchAndDevelopment.GetResults(SubjectData.StockSubjectId);
					else
						ResultText = resultText;
				}
				else
				{
					ResultText = string.Empty;
				}
			}

			/// <summary> Add the specified size in Mb </summary>
			/// <returns> The actual size that was added </returns>
			public double TryAddSize(double addedSize)
			{
				addedSize = Math.Min(addedSize, Drive.AvailableFileSize());

				if (addedSize <= 0.0)
					return 0.0;

				AddSizeNoCapacityCheck(addedSize);
				return addedSize;
			}

			/// <summary> Add the specified size in Mb. Doesn't check if the drive has enough capacity </summary>
			internal void AddSizeNoCapacityCheck(double addedSize)
			{
				Size += addedSize;
				Drive.filesSize += addedSize;
				SubjectData.AddDataCollectedInFlight(addedSize);
				OnAddSize(addedSize);
			}

			protected virtual void OnAddSize(double addedSize) { }

			/// <summary> Remove the specified size in Mb. Will delete the file/sample if the size parameter is negative or if this result in an empty file. </summary>
			/// <returns> The actual data size that was removed </returns>
			public double TryRemoveSize(double sizeToRemove = -1.0)
			{
				if (sizeToRemove < 0.0 || sizeToRemove >= Size)
				{
					sizeToRemove = Size;
					Size = 0.0;
					Drive.files.Remove(SubjectData);
					Drive.filesSize = Math.Max(0.0, Drive.filesSize - sizeToRemove);
					OnRemoveSize(sizeToRemove);
					Drive = null;
				}
				else
				{
					Drive.filesSize = Math.Max(0.0, Drive.filesSize - sizeToRemove);
					Size -= sizeToRemove;
				}

				SubjectData.RemoveDataCollectedInFlight(sizeToRemove);
				return sizeToRemove;
			}

			protected virtual void OnRemoveSize(double removedSize) { }

			public static void Load(ConfigNode node, DriveHandler drive, DataType dataType)
			{
				SubjectData subjectData;
				string stockSubjectId = Lib.ConfigValue(node, VALUENAME_STOCKID, string.Empty);
				// the stock subject id is stored only if this is an asteroid sample, or a non-standard subject id
				if (stockSubjectId != string.Empty)
					subjectData = ScienceDB.GetSubjectDataFromStockId(stockSubjectId);
				else
					subjectData = ScienceDB.GetSubjectData(node.name);

				if (subjectData == null)
					return;

				double size = Lib.ConfigValue(node, nameof(Size), 0.0);
				if (double.IsNaN(size) || size <= 0.0)
				{
					Lib.LogStack($"Can't load {dataType} for {subjectData}, size of {size} is invalid", Lib.LogLevel.Error);
					return;
				}

				string resultText = Lib.ConfigValue(node, nameof(ResultText), string.Empty);
				bool useStockCrediting = Lib.ConfigValue(node, nameof(UseStockCrediting), false);

				if (dataType == DataType.file)
				{
					if (drive.files.ContainsKey(subjectData))
						return;

					ScienceFile file = new ScienceFile(drive, subjectData, size, false, resultText, useStockCrediting, false); // can't check capacity as drive definition isn't yet loaded from OnLoad(), due to B9PS sheanigans
					file.Transmit = Lib.ConfigValue(node, nameof(ScienceFile.Transmit), true);
				}
				else
				{
					if (drive.samples.ContainsKey(subjectData))
						return;

					ScienceSample sample = new ScienceSample(drive, subjectData, size, false, resultText, useStockCrediting, false); // can't check capacity as drive definition isn't yet loaded from OnLoad(), due to B9PS sheanigans
					sample.Analyze = Lib.ConfigValue(node, nameof(ScienceSample.Analyze), true);
				}
			}

			public virtual void Save(ConfigNode node)
			{
				node.name = SubjectData.Id;
				node.AddValue(nameof(Size), Size);
				node.AddValue(nameof(UseStockCrediting), UseStockCrediting);
				node.AddValue(nameof(ResultText), ResultText);

				if (SubjectData is UnknownSubjectData)
					node.AddValue(VALUENAME_STOCKID, SubjectData.StockSubjectId);
			}

			public abstract ScienceData ConvertToStockData();
		}

		public class ScienceFile : KsmScienceData
		{
			public bool Transmit { get; set; }

			/// <summary>
			/// This shouldn't be used outside of the DriveHandler class. Use the DriveHandler.RecordFile() method instead.
			/// </summary>
			internal ScienceFile(
				DriveHandler drive,
				SubjectData subjectData,
				double size,
				bool generateResultText = true,
				string resultText = null,
				bool useStockCrediting = false,
				bool checkDriveCapacity = true)
				: base(drive, subjectData, generateResultText, resultText, useStockCrediting)
			{
				Transmit = true;

				if (checkDriveCapacity)
					size = Math.Min(size, drive.AvailableFileSize());

				if (size > 0.0)
				{
					drive.files.Add(subjectData, this);
					AddSizeNoCapacityCheck(size);
				}
			}

			public override void Save(ConfigNode node)
			{
				base.Save(node);
				node.AddValue(nameof(Transmit), Transmit);
			}

			public override ScienceData ConvertToStockData()
			{
				return new ScienceData((float)Size, 1.0f, 1.0f, SubjectData.StockSubjectId, SubjectData.FullTitle);
			}
		}

		public class ScienceSample : KsmScienceData
		{
			/// <summary> Sample mass in tons </summary>
			public double Mass { get; protected set; }

			/// <summary>flagged for analysis in a laboratory</summary>
			public bool Analyze { get; set; }

			/// <summary>
			/// This shouldn't be used outside of the DriveHandler class. Use the DriveHandler.RecordSample() method instead.
			/// </summary>
			internal ScienceSample(
				DriveHandler drive,
				SubjectData subjectData,
				double size,
				bool generateResultText = true,
				string resultText = null,
				bool useStockCrediting = false,
				bool checkDriveCapacity = true)
				: base(drive, subjectData, generateResultText, resultText, useStockCrediting)
			{
				Analyze = true;

				if (checkDriveCapacity)
				{
					size = Math.Min(size, drive.AvailableSampleSize(true));
				}

				if (size > 0.0)
				{
					drive.samples.Add(subjectData, this);
					AddSizeNoCapacityCheck(size);
				}
			}

			public override void Save(ConfigNode node)
			{
				base.Save(node);
				node.AddValue(nameof(Analyze), Analyze);
			}

			protected override void OnAddSize(double addedSize)
			{
				double addedMass = addedSize * SubjectData.ExpInfo.MassPerMB;
				Drive.samplesMass += addedMass;
				UpdateMass();
			}

			protected override void OnRemoveSize(double removedSize)
			{
				double removedMass = removedSize * SubjectData.ExpInfo.MassPerMB;
				Drive.samplesMass = Math.Max(0.0, Drive.samplesMass - removedMass);
				UpdateMass();
			}

			public void UpdateMass()
			{
				Mass = Size * SubjectData.ExpInfo.MassPerMB;
			}

			public override ScienceData ConvertToStockData()
			{
				return new ScienceData((float)Size, 0f, 0f, SubjectData.StockSubjectId, SubjectData.FullTitle);
			}
		}

		private bool isPrivate;
		private double filesSize;
		private double samplesSize;
		private double samplesMass;

		private Dictionary<SubjectData, ScienceFile> files = new Dictionary<SubjectData, ScienceFile>();
		private Dictionary<SubjectData, ScienceSample> samples = new Dictionary<SubjectData, ScienceSample>();

		public double FilesSize => filesSize;
		public double SamplesSize => samplesSize;
		public double SamplesMass => samplesMass;
		public bool IsPrivate => isPrivate;

		public Dictionary<SubjectData, ScienceFile>.ValueCollection Files => files.Values;
		public Dictionary<SubjectData, ScienceSample>.ValueCollection Samples => samples.Values;

		public Dictionary<SubjectData, ScienceFile> FileDictionary => files;
		public Dictionary<SubjectData, ScienceSample> SampleDictionary => samples;

		public override void OnUpdate(double elapsedSec)
		{
			filesSize = 0.0;
			if (files.Count > 0)
			{
				foreach (ScienceFile file in files.Values)
				{
					filesSize += file.Size;
				}
			}

			samplesSize = 0.0;
			samplesMass = 0.0;
			if (samples.Count > 0)
			{
				foreach (ScienceSample sample in samples.Values)
				{
					samplesSize += sample.Size;
					samplesMass += sample.Mass;
				}
			}
		}

		public override void OnLoad(ConfigNode node)
		{
			files.Clear();
			ConfigNode filesNode = node.GetNode("FILES");
			if (filesNode != null)
			{
				foreach (ConfigNode fileNode in filesNode.GetNodes())
				{
					KsmScienceData.Load(fileNode, this, KsmScienceData.DataType.file);
				}
			}

			samples.Clear();
			ConfigNode samplesNode = node.GetNode("SAMPLES");
			if (samplesNode != null)
			{
				foreach (ConfigNode sampleNode in samplesNode.GetNodes())
				{
					KsmScienceData.Load(sampleNode, this, KsmScienceData.DataType.sample);
				}
			}

			// parse capacities. be generous with default values for backwards
			// compatibility (drives had unlimited storage before this)
			//DataCapacity = Lib.ConfigValue(node, "dataCapacity", 100000.0);
			//SampleCapacity = Lib.ConfigValue(node, "sampleCapacity", 1000);
		}

		public override void OnSave(ConfigNode node)
		{
			if (files.Count > 0)
			{
				ConfigNode filesNode = node.AddNode("FILES");
				foreach (ScienceFile file in files.Values)
				{
					ConfigNode fileNode = new ConfigNode();
					file.Save(fileNode);
					filesNode.AddNode(fileNode);
				}
			}

			if (samples.Count > 0)
			{
				ConfigNode samplesNode = node.AddNode("SAMPLES");
				foreach (ScienceSample sample in samples.Values)
				{
					ConfigNode sampleNode = new ConfigNode();
					sample.Save(sampleNode);
					samplesNode.AddNode(sampleNode);
				}
			}

			//node.AddValue("dataCapacity", dataCapacity);
			//node.AddValue("sampleCapacity", sampleCapacity);
		}

		public override void OnFlightPartWillDie()
		{
			DeleteAllData();
		}

		public double AvailableFileSize()
		{
			if (definition.FilesCapacity < 0.0)
				return double.MaxValue;

			return Math.Max(0.0, definition.FilesCapacity - filesSize);
		}

		public double AvailableSampleSize(bool checkCount)
		{
			if (checkCount && definition.MaxSamples > 0 && samples.Count >= definition.MaxSamples)
				return 0.0;

			if (definition.SamplesCapacity < 0.0)
				return double.MaxValue;

			return Math.Max(0.0, definition.SamplesCapacity - samplesSize);
		}

		public int AvailableSampleCount()
		{
			if (definition.MaxSamples <= 0)
				return int.MaxValue;

			return samples.Count - definition.MaxSamples;
		}

		public bool CanStoreFile(double size)
		{
			if (definition.FilesCapacity < 0.0)
				return true;

			return filesSize + size < definition.FilesCapacity;
		}

		public bool CanStoreSample(double size)
		{
			if (samples.Count >= definition.MaxSamples)
				return false;

			if (definition.SamplesCapacity < 0.0)
				return true;

			return samplesSize + size < definition.SamplesCapacity;
		}

		/// <summary>
		/// Add a file for the provided subject, or if a file for that subject exists already, increase its size <br/>
		/// Return the file that was created/incremented, or null if there isn't enough space on the drive.
		/// </summary>
		public ScienceFile RecordFile(SubjectData subjectData, double size, bool generateResultText = true, string resultText = null, bool useStockCrediting = false)
		{
			if (size <= 0.0 ||!CanStoreFile(size))
				return null;

			// create new file or increase size of existing one
			if (files.Count == 0 || !files.TryGetValue(subjectData, out ScienceFile file))
				file = new ScienceFile(this, subjectData, size, generateResultText, resultText, useStockCrediting, false);
			else
				file.AddSizeNoCapacityCheck(size);

			return file;
		}

		/// <summary>
		/// Add a sample for the provided subject, or if a sample for that subject exists already, increase its size <br/>
		/// Return the sample that was created/incremented, or null if there isn't enough space on the drive.
		/// </summary>
		public ScienceSample RecordSample(SubjectData subjectData, double size, bool generateResultText = true, string resultText = null, bool useStockCrediting = false)
		{
			if (!CanStoreSample(size))
				return null;

			// create new sample or increase size of existing one
			if (samples.Count == 0 || !samples.TryGetValue(subjectData, out ScienceSample sample))
				sample = new ScienceSample(this, subjectData, size, generateResultText, resultText, useStockCrediting, false);
			else
				sample.AddSizeNoCapacityCheck(size);

			return sample;
		}

		/// <summary>
		/// Remove some data on the file for the provided subject, deleting the file when it is empty
		/// </summary>
		public void DeleteFile(SubjectData subjectData, double sizeToRemove = -1.0)
		{
			if (files.TryGetValue(subjectData, out ScienceFile file))
				file.TryRemoveSize(sizeToRemove);
		}

		/// <summary>
		/// Remove some data on the sample for the provided subject, deleting the sample when it is empty
		/// </summary>
		public void DeleteSample(SubjectData subjectData, double sizeToRemove = -1.0)
		{
			if (samples.TryGetValue(subjectData, out ScienceSample sample))
				sample.TryRemoveSize(sizeToRemove);
		}

		/// <summary>
		/// Delete all files/samples in the drive. Use with care, this should onyl be used if all files/samples
		/// are garanteed to be permanently removed from the game and aren't referenced by anything anymore.
		/// </summary>
		public void DeleteAllData()
		{
			foreach (ScienceFile file in files.Values)
				file.SubjectData.RemoveDataCollectedInFlight(file.Size);

			files.Clear();
			filesSize = 0.0;

			foreach (ScienceSample sample in samples.Values)
				sample.SubjectData.RemoveDataCollectedInFlight(sample.Size);

			samples.Clear();
			samplesSize = 0.0;
			samplesMass = 0.0;
		}

		/// <summary> Attempt to move a file to another drive </summary>
		public bool TryMoveFile(ScienceFile file, DriveHandler destination, bool allowPartial = true)
		{
			if (!files.TryGetValue(file.SubjectData, out ScienceFile driveFile) || file != driveFile)
				return false;

			return TryMoveDriveFile(file, destination, allowPartial);
		}

		private bool TryMoveDriveFile(ScienceFile file, DriveHandler destination, bool allowPartial = true)
		{
			double transferSize = Math.Min(file.Size, destination.AvailableFileSize());
			if (transferSize == 0.0 || (!allowPartial && transferSize < file.Size))
				return false;

			if (destination.RecordFile(file.SubjectData, transferSize, false, file.ResultText, file.UseStockCrediting) == null)
				return false;

			file.TryRemoveSize(transferSize);
			return true;
		}

		/// <summary> Attempt to move a sample to another drive </summary>
		public bool TryMoveSample(ScienceSample sample, DriveHandler destination, bool allowPartial = true)
		{
			if (!samples.TryGetValue(sample.SubjectData, out ScienceSample driveSample) || sample != driveSample)
				return false;

			return TryMoveDriveSample(sample, destination, allowPartial);
		}

		private bool TryMoveDriveSample(ScienceSample sample, DriveHandler destination, bool allowPartial = true)
		{
			double transferSize = Math.Min(sample.Size, destination.AvailableSampleSize(true));
			if (transferSize == 0.0 || (!allowPartial && transferSize < sample.Size))
				return false;

			if (destination.RecordSample(sample.SubjectData, transferSize, false, sample.ResultText, sample.UseStockCrediting) == null)
				return false;

			sample.TryRemoveSize(transferSize);
			return true;
		}

		private static List<ScienceFile> fileMoveBuffer = new List<ScienceFile>();

		/// <summary>
		/// Attempt to move all files to another drive. <br/>
		/// If there isn't enough space on the destination drive for all files, the last transferred file will be split. <br/>
		/// Returns true if all files were transferred, false otherwise.
		/// </summary>
		public bool TryMoveAllFiles(DriveHandler destination)
		{
			fileMoveBuffer.Clear();
			double availableSize = destination.AvailableFileSize();
			double lastFileTransferSize = 0.0;
			foreach (ScienceFile file in files.Values)
			{
				double transferSize = Math.Min(availableSize, file.Size);
				if (transferSize <= 0.0)
					break;

				if (destination.RecordFile(file.SubjectData, transferSize, false, file.ResultText, file.UseStockCrediting) == null)
					break;

				fileMoveBuffer.Add(file);
				availableSize = destination.AvailableFileSize();

				if (transferSize < file.Size)
				{
					lastFileTransferSize = transferSize;
					break;
				}
			}

			int lastFileIndex = fileMoveBuffer.Count - 1;
			for (int i = 0; i < fileMoveBuffer.Count; i++)
			{
				if (i == lastFileIndex)
					fileMoveBuffer[i].TryRemoveSize(lastFileTransferSize);
				else
					fileMoveBuffer[i].TryRemoveSize();
			}

			fileMoveBuffer.Clear();
			return lastFileTransferSize == 0.0;
		}

		private static List<ScienceSample> sampleMoveBuffer = new List<ScienceSample>();

		/// <summary>
		/// Attempt to move all samples to another drive. <br/>
		/// If there isn't enough space on the destination drive for all samples, the last transferred sample will be split. <br/>
		/// Returns true if all samples were transferred, false otherwise.
		/// </summary>
		public bool TryMoveAllSamples(DriveHandler destination)
		{
			sampleMoveBuffer.Clear();
			double availableSize = destination.AvailableSampleSize(true);
			double lastSampleTransferSize = 0.0;
			foreach (ScienceSample sample in samples.Values)
			{
				double transferSize = Math.Min(availableSize, sample.Size);
				if (transferSize <= 0.0)
					break;

				if (destination.RecordFile(sample.SubjectData, transferSize, false, sample.ResultText, sample.UseStockCrediting) == null)
					break;

				sampleMoveBuffer.Add(sample);
				availableSize = destination.AvailableSampleSize(true);

				if (transferSize < sample.Size)
				{
					lastSampleTransferSize = transferSize;
					break;
				}
			}

			int lastSampleIndex = sampleMoveBuffer.Count - 1;
			for (int i = 0; i < sampleMoveBuffer.Count; i++)
			{
				if (i == lastSampleIndex)
					sampleMoveBuffer[i].TryRemoveSize(lastSampleTransferSize);
				else
					sampleMoveBuffer[i].TryRemoveSize();
			}

			sampleMoveBuffer.Clear();
			return lastSampleTransferSize == 0.0;
		}

		/// <summary> Get all drives on the vessel, including private drives </summary>
		public static IEnumerable<DriveHandler> GetDrives(VesselDataBase vd)
		{
			return vd.Parts.AllModulesOfType<DriveHandler>();
		}

		/// <summary>
		/// Get the drive who already has that subject, or if not found the drive with the largest available space.
		/// Returns null if there no file capacity available on the vessel.
		/// <param name="subject">if null, the drive with the largest available space will be returned</param>
		/// <param name="availableSize">the available file size on the returned drive</param>
		public static DriveHandler FindBestDriveForFile(VesselDataBase vesselData, SubjectData subject, out double availableSize)
		{
			DriveHandler biggestDrive = null;
			availableSize = 0.0;
			foreach (DriveHandler drive in GetDrives(vesselData))
			{
				double driveSize = drive.AvailableFileSize();

				if (subject != null && driveSize > 0.0 && drive.files.ContainsKey(subject))
				{
					availableSize = driveSize;
					return drive;
				}

				if (driveSize > availableSize)
				{
					biggestDrive = drive;
					availableSize = driveSize;
				}
			}
			return biggestDrive;
		}

		/// <summary> Get the drive with the most available space. Returns null if there are no drives on the vessel.
		/// <param name="availableSize">the available sample size on the returned drive</param>
		public static DriveHandler FindBestDriveForSamples(VesselDataBase vesselData, out double availableSize)
		{
			DriveHandler biggestDrive = null;
			availableSize = 0.0;
			foreach (DriveHandler drive in GetDrives(vesselData))
			{
				double driveSize = drive.AvailableSampleSize(true);
				if (driveSize > availableSize)
				{
					biggestDrive = drive;
					availableSize = driveSize;
				}
			}
			return biggestDrive;
		}

		/// <summary>
		/// Attempt to move all samples and files from a drive to another another vessel <br/>
		/// If there isn't enough space on the destination vessel drives, the last transferred file/sample will be split. <br/>
		/// Returns true if all files and samples were transferred, false otherwise.
		/// </summary>
		public static bool MoveAllFromDriveToVessel(DriveHandler fromDrive, VesselDataBase toVessel, bool moveSamples = true, bool moveFiles = true)
		{
			if (fromDrive.VesselData == toVessel)
				return false;

			foreach (DriveHandler toDrive in GetDrives(toVessel))
			{
				if (toDrive.isPrivate)
					continue;

				if (moveFiles && fromDrive.TryMoveAllFiles(toDrive))
					moveFiles = false;

				if (moveSamples && fromDrive.TryMoveAllSamples(toDrive))
					moveSamples = false;
			}

			return !moveFiles && !moveSamples;
		}

		/// <summary>
		/// Attempt to move all samples and files in a vessel to another vessel drive <br/>
		/// If there isn't enough space on the destination drive, the last transferred file/sample will be split. <br/>
		/// Returns true if all files and samples were transferred, false otherwise.
		/// </summary>
		public static bool MoveAllFromVesselToDrive(VesselDataBase fromVessel, DriveHandler toDrive, bool moveSamples = true, bool moveFiles = true)
		{
			if (toDrive.VesselData == fromVessel)
				return false;

			foreach (DriveHandler fromDrive in GetDrives(fromVessel))
			{
				if (moveFiles && fromDrive.TryMoveAllFiles(toDrive))
					moveFiles = false;

				if (moveSamples && fromDrive.TryMoveAllSamples(toDrive))
					moveSamples = false;
			}

			return !moveFiles && !moveSamples;
		}

		/// <summary>
		/// Attempt to move all samples and files in a vessel to another vessel drives <br/>
		/// If there isn't enough space on the destination vessel drives, the last transferred file/sample will be split. <br/>
		/// Returns true if all files and samples were transferred, false otherwise.
		/// </summary>
		public static bool MoveAllFromVesselToVessel(VesselDataBase fromVessel, VesselDataBase toVessel, bool moveSamples = true, bool moveFiles = true)
		{
			IEnumerable<DriveHandler> toDrives = GetDrives(toVessel);

			foreach (DriveHandler fromDrive in GetDrives(fromVessel))
			{
				foreach (DriveHandler toDrive in toDrives)
				{
					if (toDrive.isPrivate)
						continue;

					if (moveFiles && fromDrive.TryMoveAllFiles(toDrive))
						moveFiles = false;

					if (moveSamples && fromDrive.TryMoveAllSamples(toDrive))
						moveSamples = false;
				}

				if (!moveFiles && !moveSamples)
					break;
			}

			return !moveFiles && !moveSamples;
		}
	}
}
