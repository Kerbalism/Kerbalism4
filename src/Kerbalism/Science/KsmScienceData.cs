using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	public interface IKsmScienceDataStorage
	{
		//double SizeAvailable { get; }
		//double SizeCapacity { get; }
		//double DataSize { get; }
	}

	public abstract class KsmScienceData
	{
		public const string VALUENAME_STOCKID = "stockId";

		/// <summary>subject</summary>
		public SubjectData SubjectData { get; protected set; }

		/// <summary>data size in Mb. For samples, this is also a representation of the sample volume </summary>
		public double Size { get; protected set; }

		/// <summary>Force the stock crediting formula to be applied on recovery. will be true if the file was created by the hijacker. </summary>
		public bool UseStockCrediting { get; protected set; }

		/// <summary>randomized result text</summary>
		public string ResultText { get; protected set; }

		public abstract bool IsDeleted { get; }

		public double lastSizeDelta;
		public double dataRate;

		protected KsmScienceData(SubjectData subjectData, bool generateResultText = true, string resultText = null, bool useStockCrediting = false)
		{
			SubjectData = subjectData;
			UseStockCrediting = useStockCrediting;

			if (generateResultText)
				ResultText = ResearchAndDevelopment.GetResults(SubjectData.StockSubjectId);
			else if (resultText == null)
				ResultText = string.Empty;
			else
				ResultText = resultText;
		}

		protected void AddSizeToData(double addedSize)
		{
			Size += addedSize;
			SubjectData.AddDataCollectedInFlight(addedSize);
		}

		/// <summary> Add the specified size in Mb. Doesn't check if the drive has enough capacity </summary>
		internal void AddSize(double addedSize)
		{
			lastSizeDelta = addedSize;
			AddSizeToData(addedSize);
			AddSizeToStorage(addedSize);
		}

		protected abstract void AddSizeToStorage(double addedSize);

		/// <summary> Remove the specified size in Mb. Will delete the file/sample if the size parameter is negative or if this result in an empty file. </summary>
		/// <returns> The actual data size that was removed </returns>
		public double TryRemoveSize(double sizeToRemove = -1.0)
		{
			if (sizeToRemove < 0.0 || sizeToRemove >= Size)
			{
				sizeToRemove = Size;
				RemoveSizeFromStorage(sizeToRemove, true);
			}
			else
			{
				RemoveSizeFromStorage(sizeToRemove, false);
			}

			Size -= sizeToRemove;
			SubjectData.RemoveDataCollectedInFlight(sizeToRemove);
			lastSizeDelta = -sizeToRemove;

			return sizeToRemove;
		}

		protected abstract void RemoveSizeFromStorage(double removedSize, bool delete);

		public abstract double AvailableSize();

		public static void Load<T>(ConfigNode node, T storage) where T : IKsmScienceDataStorage
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
			if (Lib.IsZeroOrNegativeOrNaN(size))
			{
				Lib.LogStack($"Can't load science data for {subjectData}, size of {size} is invalid", Lib.LogLevel.Error);
				return;
			}

			string resultText = Lib.ConfigValue(node, nameof(ResultText), string.Empty);
			bool useStockCrediting = Lib.ConfigValue(node, nameof(UseStockCrediting), false);

			if (storage is DriveHandler drive)
			{
				if (drive.filesDict.ContainsKey(subjectData))
					return;

				ScienceFile file = new ScienceFile(drive, subjectData, size, false, resultText, useStockCrediting, false); // can't check capacity as drive definition isn't yet loaded from OnLoad(), due to B9PS sheanigans
				file.transmit = Lib.ConfigValue(node, nameof(ScienceFile.transmit), true);
			}
			else if (storage is SampleStorageHandler sampleStorage)
			{
				if (sampleStorage.samplesDict.ContainsKey(subjectData))
					return;

				new ScienceSample(sampleStorage, subjectData, size, false, resultText, useStockCrediting, false); // can't check capacity as drive definition isn't yet loaded from OnLoad(), due to B9PS sheanigans
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
		public DriveHandler drive;

		public bool transmit;

		internal ScienceFile(
			DriveHandler drive,
			SubjectData subjectData,
			double size,
			bool generateResultText = true,
			string resultText = null,
			bool useStockCrediting = false,
			bool checkDriveCapacity = true)
			: base(subjectData, generateResultText, resultText, useStockCrediting)
		{
			this.drive = drive; 
			transmit = true;

			if (checkDriveCapacity)
				size = Math.Min(size, drive.AvailableSize());

			if (size > 0.0)
			{
				drive.filesDict.Add(subjectData, this);
				AddSizeToData(size);
				AddSizeToStorage(size);
			}
		}

		public override bool IsDeleted => drive == null;

		protected override void AddSizeToStorage(double addedSize)
		{
			drive.filesSize += addedSize;
		}

		protected override void RemoveSizeFromStorage(double removedSize, bool delete)
		{
			drive.filesSize -= removedSize;
			drive.filesSize = Lib.ClampToPositive(drive.filesSize);

			if (delete)
			{
				drive.filesDict.Remove(SubjectData);
				drive = null;
			}
		}

		public override double AvailableSize() => drive.AvailableSize();

		public override void Save(ConfigNode node)
		{
			base.Save(node);
			node.AddValue(nameof(transmit), transmit);
		}

		public override ScienceData ConvertToStockData()
		{
			return new ScienceData((float)Size, 1.0f, 1.0f, SubjectData.StockSubjectId, SubjectData.FullTitle);
		}
	}

	public class ScienceSample : KsmScienceData
	{
		public SampleStorageHandler sampleStorage;

		/// <summary>
		/// This shouldn't be used outside of the DriveHandler class. Use the DriveHandler.RecordSample() method instead.
		/// </summary>
		internal ScienceSample(
			SampleStorageHandler sampleStorage,
			SubjectData subjectData,
			double size,
			bool generateResultText = true,
			string resultText = null,
			bool useStockCrediting = false,
			bool checkCapacity = true)
			: base(subjectData, generateResultText, resultText, useStockCrediting)
		{
			this.sampleStorage = sampleStorage;

			if (checkCapacity)
			{
				size = Math.Min(size, sampleStorage.AvailableSize());
			}

			if (size > 0.0)
			{
				sampleStorage.samplesDict.Add(subjectData, this);
				AddSizeToData(size);
				sampleStorage.samplesSize += size;
			}
		}

		public override bool IsDeleted => sampleStorage == null;

		protected override void AddSizeToStorage(double addedSize)
		{
			sampleStorage.samplesSize += addedSize;
			if (!SubjectData.ExpInfo.SampleCollecting)
				sampleStorage.sampleMaterialSize = Lib.ClampToPositive(sampleStorage.sampleMaterialSize - addedSize);

			sampleStorage.UpdateInventoryMassOnSizeModified();
		}

		// TODO : implement deleting the stored part
		protected override void RemoveSizeFromStorage(double removedSize, bool delete)
		{
			sampleStorage.samplesSize -= removedSize;
			sampleStorage.samplesSize = Lib.ClampToPositive(sampleStorage.samplesSize);

			if (delete)
			{
				sampleStorage.samplesDict.Remove(SubjectData);
				sampleStorage = null;
			}
		}

		public override double AvailableSize() => sampleStorage.AvailableSize();

		public override ScienceData ConvertToStockData()
		{
			return new ScienceData((float)Size, 0f, 0f, SubjectData.StockSubjectId, SubjectData.FullTitle);
		}
	}
}
