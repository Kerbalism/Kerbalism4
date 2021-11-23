using System;
using System.Collections.Generic;
using static KERBALISM.DriveHandler;



namespace KERBALISM
{
	public class VesselComms : ICommonRecipeExecutedCallback
	{
		public struct TransmittedFileInfo
		{
			public SubjectData subject;
			public ScienceFile file;
			public double transmitRate;

			public TransmittedFileInfo(SubjectData subject, double transmitRate, ScienceFile file = null)
			{
				this.subject = subject;
				this.transmitRate = transmitRate;
				this.file = file;
			}
		}

		private VesselDataBase vd;
		private VesselResourceAbstract drivesCapacity;
		private VesselResourceAbstract transmitCapacity;

		private Recipe idleCommsRecipe;
		private RecipeInput IdleCommsECInput;

		private Recipe transmitRecipe;
		private RecipeInput transmitCapacityInput;
		private RecipeInput transmitECInput;

		private List<ScienceFile> filesToTransmit = new List<ScienceFile>();
		public List<TransmittedFileInfo> transmittedFiles = new List<TransmittedFileInfo>();

		public List<DriveHandler> drives = new List<DriveHandler>();
		public double filesSize;
		public double fileCapacity;

		public int DriveCapacityId => drivesCapacity.id;
		public int TransmitCapacityId => transmitCapacity.id;
		public double transmitECRatePerMb;
		public double TransmitDataRate => vd.ConnectionInfo.DataRate * (1.0 - transmitCapacity.Level);
		public double TransmitECRate => vd.ConnectionInfo.Ec * (1.0 - transmitCapacity.Level);

		public double totalDataTransmitted;
		public double totalScienceTransmitted;

		public void Init(VesselDataBase vd)
		{
			this.vd = vd;
			drivesCapacity = vd.ResHandler.AddNewAbstractResourceToHandler();
			transmitCapacity = vd.ResHandler.AddNewAbstractResourceToHandler();

			transmitRecipe = new Recipe("Stored data transmission", RecipeCategory.ScienceData);
			transmitRecipe.priority = 0;
			transmitCapacityInput = transmitRecipe.AddInput(transmitCapacity.id, 0.0);
			transmitECInput = transmitRecipe.AddInput(VesselResHandler.ElectricChargeId, 0.0);

			idleCommsRecipe = new Recipe("Telemetry", RecipeCategory.Comms);
			IdleCommsECInput = idleCommsRecipe.AddInput(VesselResHandler.ElectricChargeId, 0.0);
		}

		public void Update(double elapsedSec)
		{
			double transmitDataRate = vd.ConnectionInfo.DataRate;
			if (Lib.IsNegativeOrNaN(transmitDataRate))
				transmitDataRate = 0.0;

			double transmitDataSize = transmitDataRate * elapsedSec;
			transmitCapacity.SetAmountAndCapacity(transmitDataSize);

			transmitECRatePerMb = transmitDataSize == 0.0 ? 0.0 : vd.ConnectionInfo.Ec / transmitDataSize;

			drives.Clear();
			filesToTransmit.Clear();
			transmittedFiles.Clear();
			filesSize = 0.0;
			fileCapacity = 0.0;
			double filesToTransmitSize = 0.0;
			// TODO / REGRESSION : we used to prioritize transmission of files with the higher science points / Mb
			// value. It might be possible to reintroduce it through the priority system for "streamed" files and
			// by looping over all files here, instead of randomly selecting the first found ones.


			foreach (DriveHandler drive in GetAllDrives(vd))
			{
				drives.Add(drive);

				if (drive.definition.FilesCapacity < 0.0)
					fileCapacity = double.MaxValue;
				else
					fileCapacity += drive.definition.FilesCapacity;

				filesSize += drive.filesSize;

				if (filesToTransmitSize < transmitDataSize)
				{
					foreach (ScienceFile file in drive.Files)
					{
						if (file.transmit)
						{
							filesToTransmit.Add(file);
							filesToTransmitSize += file.Size;
						}
					}
				}
			}

			drivesCapacity.SetAmountAndCapacity(fileCapacity - filesSize);

			if (filesToTransmitSize > 0.0)
			{
				filesToTransmitSize = Math.Min(filesToTransmitSize, transmitDataSize);
				transmitCapacityInput.NominalRate = filesToTransmitSize / elapsedSec;
				transmitECInput.NominalRate = transmitECRatePerMb * filesToTransmitSize;
				transmitRecipe.RequestExecution(vd.ResHandler, this);
			}

			if (vd.ConnectionInfo.Linked)
			{
				IdleCommsECInput.NominalRate = vd.ConnectionInfo.EcIdle;
				idleCommsRecipe.RequestExecution(vd.ResHandler);
			}
		}

		public void TransmitScienceData(SubjectData subject, double dataSize, double elapsedSec, ScienceFile file = null)
		{
			transmittedFiles.Add(new TransmittedFileInfo(subject, dataSize / elapsedSec, file));
			double scienceValue = dataSize * subject.SciencePerMB;
			totalScienceTransmitted += subject.RetrieveScience(scienceValue, true, ((VesselData) vd).Vessel.protoVessel, file);
			totalDataTransmitted += dataSize;
		}

		bool IRecipeExecutedCallback.IsCallbackRegistered { get; set; }
		void ICommonRecipeExecutedCallback.OnRecipesExecuted(double elapsedSec)
		{
			double executedFactor = transmitRecipe.ExecutedFactor;
			if (executedFactor < 1e-14)
				return;

			double totalTransmittedSize = transmitCapacityInput.NominalRate * transmitRecipe.ExecutedFactor * elapsedSec;

			if (totalTransmittedSize == 0.0)
				return;

			foreach (ScienceFile scienceFile in filesToTransmit)
			{
				if (totalTransmittedSize <= 0.0)
					break;

				double transmittedSize = scienceFile.TryRemoveSize(totalTransmittedSize);
				TransmitScienceData(scienceFile.SubjectData, transmittedSize, elapsedSec, scienceFile);
				totalTransmittedSize -= transmittedSize;
			}
		}

		
	}
}
