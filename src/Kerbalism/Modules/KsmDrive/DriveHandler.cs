using System;
using System.Collections.Generic;

namespace KERBALISM
{
	public class DriveHandler :
		KsmModuleHandler<ModuleKsmDrive, DriveHandler, DriveDefinition>,
		IKsmScienceDataStorage,
		IActiveStoredHandler
	{
		public double filesSize;

		public bool computeFilesDataRates = false;

		public Dictionary<SubjectData, ScienceFile> filesDict = new Dictionary<SubjectData, ScienceFile>();
		public Dictionary<SubjectData, ScienceFile>.ValueCollection Files => filesDict.Values;

		public override void OnUpdate(double elapsedSec)
		{
			filesSize = 0.0;
			if (filesDict.Count > 0)
			{
				foreach (ScienceFile file in filesDict.Values)
				{
					if (computeFilesDataRates)
					{
						file.dataRate = file.lastSizeDelta / elapsedSec;
						file.lastSizeDelta = 0.0;
					}

					filesSize += file.Size;
				}
			}
		}

		public override void OnLoad(ConfigNode node)
		{
			filesDict.Clear();
			ConfigNode filesNode = node.GetNode("FILES");
			if (filesNode != null)
			{
				foreach (ConfigNode fileNode in filesNode.GetNodes())
				{
					KsmScienceData.Load(fileNode, this);
				}
			}
		}

		public override void OnSave(ConfigNode node)
		{
			if (filesDict.Count > 0)
			{
				ConfigNode filesNode = node.AddNode("FILES");
				foreach (ScienceFile file in filesDict.Values)
				{
					ConfigNode fileNode = filesNode.AddNode("FILE");
					file.Save(fileNode);
				}
			}
		}

		public bool IsActiveCargo => true;
		public StoredPartData StoredPart { get; set; }
		public void OnCargoStored()
		{
			
		}

		public void OnCargoUnstored()
		{
			// TODO : we probably need to notify running experiments that the data can't be stored here anymore ?
		}

		public override void OnFlightPartWillDie()
		{
			DeleteAllData();
		}

		public double AvailableSize()
		{
			return Math.Max(0.0, definition.FilesCapacity - filesSize);
		}

		public bool AvailableSize(double size)
		{
			return filesSize + size <= definition.FilesCapacity;
		}

		/// <summary>
		/// Add a file for the provided subject, or if a file for that subject exists already, increase its size <br/>
		/// Return the file that was created/incremented, or null if there isn't enough space on the drive.
		/// </summary>
		public ScienceFile RecordFile(SubjectData subjectData, double size, bool generateResultText = true, string resultText = null, bool useStockCrediting = false)
		{
			if (size <= 0.0 ||!AvailableSize(size))
				return null;

			// create new file or increase size of existing one
			if (filesDict.Count == 0 || !filesDict.TryGetValue(subjectData, out ScienceFile file))
				file = new ScienceFile(this, subjectData, size, generateResultText, resultText, useStockCrediting, false);
			else
				file.AddSize(size);

			return file;
		}

		/// <summary>
		/// Remove some data on the file for the provided subject, deleting the file when it is empty
		/// </summary>
		public void DeleteFile(SubjectData subjectData, double sizeToRemove = -1.0)
		{
			if (filesDict.TryGetValue(subjectData, out ScienceFile file))
				file.TryRemoveSize(sizeToRemove);
		}

		/// <summary>
		/// Delete all files/samples in the drive. Use with care, this should onyl be used if all files/samples
		/// are garanteed to be permanently removed from the game and aren't referenced by anything anymore.
		/// </summary>
		public void DeleteAllData()
		{
			foreach (ScienceFile file in filesDict.Values)
				file.SubjectData.RemoveDataCollectedInFlight(file.Size);

			filesDict.Clear();
			filesSize = 0.0;
		}

		/// <summary> Attempt to move a file to another drive </summary>
		public bool TryMoveFile(ScienceFile file, DriveHandler destination, bool allowPartial = true)
		{
			if (!filesDict.TryGetValue(file.SubjectData, out ScienceFile driveFile) || file != driveFile)
				return false;

			return TryMoveDriveFile(file, destination, allowPartial);
		}

		private bool TryMoveDriveFile(ScienceFile file, DriveHandler destination, bool allowPartial = true)
		{
			double transferSize = Math.Min(file.Size, destination.AvailableSize());
			if (transferSize == 0.0 || (!allowPartial && transferSize < file.Size))
				return false;

			if (destination.RecordFile(file.SubjectData, transferSize, false, file.ResultText, file.UseStockCrediting) == null)
				return false;

			file.TryRemoveSize(transferSize);
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
			double availableSize = destination.AvailableSize();
			double lastFileTransferSize = 0.0;
			foreach (ScienceFile file in filesDict.Values)
			{
				double transferSize = Math.Min(availableSize, file.Size);
				if (transferSize <= 0.0)
					break;

				if (destination.RecordFile(file.SubjectData, transferSize, false, file.ResultText, file.UseStockCrediting) == null)
					break;

				fileMoveBuffer.Add(file);
				availableSize = destination.AvailableSize();

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



		/// <summary> Get all drives on the vessel, including private drives </summary>
		public static IEnumerable<DriveHandler> GetAllDrives(VesselDataBase vd)
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
			foreach (DriveHandler drive in GetAllDrives(vesselData))
			{
				double driveSize = drive.AvailableSize();

				if (subject != null && driveSize > 0.0 && drive.filesDict.ContainsKey(subject))
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

		/// <summary>
		/// Attempt to move all samples and files from a drive to another another vessel <br/>
		/// If there isn't enough space on the destination vessel drives, the last transferred file/sample will be split. <br/>
		/// Returns true if all files and samples were transferred, false otherwise.
		/// </summary>
		public static bool MoveAllFromDriveToVessel(DriveHandler fromDrive, VesselDataBase toVessel)
		{
			if (fromDrive.VesselData == toVessel)
				return false;

			bool allMoved = true;
			foreach (DriveHandler toDrive in GetAllDrives(toVessel))
			{
				if (!fromDrive.TryMoveAllFiles(toDrive))
					allMoved = false;
			}

			return allMoved;
		}

		/// <summary>
		/// Attempt to move all samples and files in a vessel to another vessel drive <br/>
		/// If there isn't enough space on the destination drive, the last transferred file/sample will be split. <br/>
		/// Returns true if all files and samples were transferred, false otherwise.
		/// </summary>
		public static bool MoveAllFromVesselToDrive(VesselDataBase fromVessel, DriveHandler toDrive)
		{
			if (toDrive.VesselData == fromVessel)
				return false;

			bool allMoved = true;
			foreach (DriveHandler fromDrive in GetAllDrives(fromVessel))
			{
				if (!fromDrive.TryMoveAllFiles(toDrive))
					allMoved = false;
			}

			return allMoved;
		}

		/// <summary>
		/// Attempt to move all samples and files in a vessel to another vessel drives <br/>
		/// If there isn't enough space on the destination vessel drives, the last transferred file/sample will be split. <br/>
		/// Returns true if all files and samples were transferred, false otherwise.
		/// </summary>
		public static bool MoveAllFromVesselToVessel(VesselDataBase fromVessel, VesselDataBase toVessel)
		{
			IEnumerable<DriveHandler> toDrives = GetAllDrives(toVessel);

			bool allMoved = true;
			foreach (DriveHandler fromDrive in GetAllDrives(fromVessel))
			{
				foreach (DriveHandler toDrive in toDrives)
				{
					if (!fromDrive.TryMoveAllFiles(toDrive))
						allMoved = false;
				}

				if (!allMoved)
					break;
			}

			return allMoved;
		}
	}
}
