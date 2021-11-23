using KERBALISM.KsmGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KERBALISM
{
	public class DataManager : KsmGuiVerticalLayout
	{

		private KsmGuiHeader summary;

		private KsmGuiText drivesSummary;
		private KsmGuiText samplesSummary;
		private KsmGuiText transmitSummary;

		private KsmGuiHeader transmitHeader;
		private KsmGuiBox transmitInfoBox;
		private KsmGuiText transmitInfoDataRate;
		private KsmGuiText transmitInfoECRate;
		private KsmGuiText transmitInfoTotalData;
		private KsmGuiVerticalLayout transmitEntriesLayout;
		private KsmGuiHeader drivesHeader;
		private KsmGuiVerticalLayout driveEntriesLayout;
		private KsmGuiHeader samplesHeader;
		private KsmGuiVerticalLayout samplesEntriesLayout;


		private VesselDataBase vd;
		private List<TransmitEntry> transmitEntries = new List<TransmitEntry>();
		private List<DriveEntry> drives = new List<DriveEntry>();
		private List<SampleStorageEntry> sampleStorages = new List<SampleStorageEntry>();


		private int totalFileCount;
		private double totalFileSize;
		private double totalFileCapacity;
		private double totalFileScience;
		private double totalSampleCount;
		private double totalSampleMass;
		private double totalSampleScience;

		public DataManager(KsmGuiBase parent) : base(parent)
		{
			summary = new KsmGuiHeader(this, "Science data manager");
			summary.SetLayoutElement(true, false, -1, 18);

			drivesSummary = new KsmGuiText(this);
			drivesSummary.SetLayoutElement(true, false, -1, 18);
			samplesSummary = new KsmGuiText(this);
			samplesSummary.SetLayoutElement(true, false, -1, 18);

			transmitHeader = new KsmGuiHeader(this, "Data transmission");
			transmitInfoBox = new KsmGuiBox(this, Kolor.Box);
			transmitInfoBox.SetLayoutElement(true, false, -1, 32);
			KsmGuiText transmitInfoDataRateTitle = new KsmGuiText(transmitInfoBox, "Data rate", TextAlignmentOptions.Top);
			transmitInfoDataRateTitle.StaticLayout(120, 16);
			KsmGuiText transmitInfoECRateTitle = new KsmGuiText(transmitInfoBox, "EC consumption", TextAlignmentOptions.Top);
			transmitInfoECRateTitle.StaticLayout(100, 16, 120);
			KsmGuiText transmitInfoTotalDataTitle = new KsmGuiText(transmitInfoBox, "Total transmitted", TextAlignmentOptions.Top);
			transmitInfoTotalDataTitle.StaticLayout(140, 16, 220);
			transmitInfoDataRate = new KsmGuiText(transmitInfoBox, null, TextAlignmentOptions.Top);
			transmitInfoDataRate.StaticLayout(120, 16, 0, 16);
			transmitInfoECRate = new KsmGuiText(transmitInfoBox, null, TextAlignmentOptions.Top);
			transmitInfoECRate.StaticLayout(100, 16, 120, 16);
			transmitInfoTotalData = new KsmGuiText(transmitInfoBox, null, TextAlignmentOptions.Top);
			transmitInfoTotalData.StaticLayout(140, 16, 220, 16);

			// TODO : add info for :
			// - data rate used/max
			// - ec consumed / max
			// - total data transmitted
			// - total science transmitted
			// (+ antenna list ?)
			transmitEntriesLayout = new KsmGuiVerticalLayout(this, 0, 0, 0, 0, 5);
			drivesHeader = new KsmGuiHeader(this, "drives");
			driveEntriesLayout = new KsmGuiVerticalLayout(this, 5);
			samplesHeader = new KsmGuiHeader(this, "samples");
			samplesEntriesLayout = new KsmGuiVerticalLayout(this, 5);
			SetUpdateAction(UpdateEntries);
		}

		public void SetVessel(VesselDataBase vd)
		{
			this.vd = vd;
		}

		public void UpdateEntries()
		{
			totalFileCount = 0;
			totalFileSize = 0.0;
			totalFileCapacity = 0.0;
			totalSampleCount = 0.0;
			totalFileScience = 0.0;
			totalSampleScience = 0.0;

			transmitInfoDataRate.Text = KsmString.Get.ReadableDataRateCompared(vd.vesselComms.TransmitDataRate, vd.ConnectionInfo.DataRate).GetStringAndRelease();
			transmitInfoECRate.Text = KsmString.Get.ReadableRate(vd.vesselComms.TransmitECRate, false, "EC").GetStringAndRelease();
			transmitInfoTotalData.Text = KsmString.Get.ReadableDataSize(vd.vesselComms.totalDataTransmitted).Add(" / ").ReadableScience(vd.vesselComms.totalScienceTransmitted).GetStringAndRelease();

			int index = 0;
			foreach (VesselComms.TransmittedFileInfo transmittedFile in vd.vesselComms.transmittedFiles)
			{
				TransmitEntry entry;
				if (index >= transmitEntries.Count)
				{
					entry = new TransmitEntry(this);
					transmitEntries.Add(entry);
				}
				else
				{
					entry = transmitEntries[index];
				}

				entry.Update(transmittedFile);
				index++;
			}

			while (index < transmitEntries.Count)
			{
				int last = transmitEntries.Count - 1;
				transmitEntries[last].Destroy();
				transmitEntries.RemoveAt(last);
			}

			transmitHeader.Enabled = index != 0;

			index = 0;
			foreach (DriveHandler driveHandler in DriveHandler.GetAllDrives(vd))
			{
				DriveEntry entry;
				if (index >= drives.Count)
				{
					entry = new DriveEntry(this, driveHandler);
					drives.Add(entry);
				}
				else
				{
					entry = drives[index];
					if (entry.drive != driveHandler)
					{
						entry.drive.computeFilesDataRates = false;
						entry.drive = driveHandler;
						entry.drive.computeFilesDataRates = true;
					}
				}

				entry.UpdateFiles();
				index++;
			}

			while (index < drives.Count)
			{
				int last = drives.Count - 1;
				drives[last].Destroy();
				drives.RemoveAt(last);
			}

			KsmString ks = KsmString.Get;
			ks.Format("Drives", KF.Bold);
			ks.Position(60);
			ks.Add(totalFileCount.ToString(), " ", totalFileCount < 2 ? "file" : "files");
			ks.Add(" - ");
			ks.ReadableDataSize(totalFileSize);
			ks.Add(" / ");
			ks.ReadableDataSize(totalFileCapacity);
			ks.Add(" - ");
			ks.ReadableScience(totalFileScience);
			drivesSummary.Text = ks.GetStringAndRelease();

			index = 0;
			foreach (SampleStorageHandler storageHandler in SampleStorageHandler.GetAllSampleStorages(vd))
			{
				if (index >= sampleStorages.Count)
					sampleStorages.Add(new SampleStorageEntry(samplesEntriesLayout, storageHandler));
				else
					sampleStorages[index].sampleStorage = storageHandler;

				index++;
			}

			while (index < sampleStorages.Count)
			{
				int last = sampleStorages.Count - 1;
				sampleStorages[last].Destroy();
				sampleStorages.RemoveAt(last);
			}
		}

		private class TransmitEntry : KsmGuiBase
		{
			private KsmGuiImage typeIcon;
			private KsmGuiText titleInfo;
			private KsmGuiText subjectInfo;
			private KsmGuiText transmitState;

			public TransmitEntry(DataManager parent) : base(parent.transmitEntriesLayout)
			{
				SetLayoutElement(true, false, -1, 30);
				typeIcon = new KsmGuiImage(this, null, 16, 16);
				typeIcon.StaticLayout(16, 16, 0, 5);

				titleInfo = new KsmGuiText(this, null, TextAlignmentOptions.TopLeft, false, TextOverflowModes.Ellipsis);
				titleInfo.TextComponent.fontStyle = FontStyles.Bold;
				titleInfo.StaticLayout(200, 16, 21, 1);

				subjectInfo = new KsmGuiText(this, null, TextAlignmentOptions.TopLeft, false, TextOverflowModes.Ellipsis);
				subjectInfo.TextComponent.fontSize = 10f;
				subjectInfo.TextComponent.fontStyle = FontStyles.UpperCase;
				subjectInfo.TextComponent.color = Kolor.LightGrey;
				subjectInfo.StaticLayout(335, 14, 21, 16);

				transmitState = new KsmGuiText(this);
				transmitState.StaticLayout(135, 18, 225);
			}

			public void Update(VesselComms.TransmittedFileInfo fileInfo)
			{
				bool isStream = fileInfo.file == null;
				typeIcon.SetIconTexture(isStream ? Textures.transmit32 : Textures.file32);
				titleInfo.Text = fileInfo.subject.ExperimentTitle;
				subjectInfo.Text = fileInfo.subject.SituationTitle;
				
				KsmString ks = KsmString.Get;
				ks.Format(KF.ReadableDataRate(fileInfo.transmitRate), KF.Bold);
				ks.Position(70);
				if (fileInfo.file == null)
				{
					ks.Add("Streamed");
				}
				else
				{
					ks.ReadableCountdown(fileInfo.file.Size / fileInfo.transmitRate);
				}

				transmitState.Text = ks.GetStringAndRelease();
			}
		}

		private class DriveEntry : KsmGuiVerticalLayout
		{
			private class FileEntry : KsmGuiBase
			{
				public ScienceFile file;
				public DriveEntry driveEntry;
				private KsmGuiIconButton dataSelect;
				private KsmGuiText titleInfo;
				private KsmGuiText subjectInfo;
				private KsmGuiText sizeInfo;
				private KsmGuiText rateInfo;
				private KsmGuiIconButton deleteButton;
				private KsmGuiIconButton transmitToggle;
				private KsmGuiBox mainTooltipHoverArea;
				public bool selected;

				public FileEntry(DriveEntry parent, ScienceFile file) : base(parent)
				{
					
					driveEntry = parent;
					SetLayoutElement(true, false, -1, 30);
					dataSelect = new KsmGuiIconButton(this, Textures.file32, Select);
					dataSelect.SetTooltip("Select");
					dataSelect.StaticLayout(16, 16, 0, 5);
					titleInfo = new KsmGuiText(this, null, TextAlignmentOptions.TopLeft, false, TextOverflowModes.Ellipsis);
					titleInfo.TextComponent.fontStyle = FontStyles.Bold;
					titleInfo.StaticLayout(180, 16, 21, 1); // 16 + 5
					subjectInfo = new KsmGuiText(this, null, TextAlignmentOptions.TopLeft, false, TextOverflowModes.Ellipsis);
					subjectInfo.TextComponent.fontSize = 10f;
					subjectInfo.TextComponent.fontStyle = FontStyles.UpperCase;
					subjectInfo.TextComponent.color = Kolor.LightGrey;
					subjectInfo.StaticLayout(180, 14, 21, 16);
					sizeInfo = new KsmGuiText(this, null, TextAlignmentOptions.TopLeft, false, TextOverflowModes.Truncate);
					sizeInfo.StaticLayout(115, 16, 205, 1); // 295 - 160 - 5
					rateInfo = new KsmGuiText(this, null);
					rateInfo.TextComponent.fontSize = 10f;
					rateInfo.TextComponent.fontStyle = FontStyles.Bold;
					rateInfo.StaticLayout(115, 14, 205, 16);
					transmitToggle = new KsmGuiIconButton(this, Textures.transmit32, ToggleTransmit);
					transmitToggle.StaticLayout(16, 16, 322, 5); // 301 + 16 + 5
					transmitToggle.SetTooltip("Transmit");
					deleteButton = new KsmGuiIconButton(this, Textures.delete32, Delete);
					deleteButton.StaticLayout(16, 16, 343, 5); // 322 + 16 + 5
					deleteButton.SetTooltip("Delete");
					deleteButton.SetIconColor(Kolor.Red);
					mainTooltipHoverArea = new KsmGuiBox(this);
					mainTooltipHoverArea.StaticLayout(295, 30, 20);
					mainTooltipHoverArea.SetTooltip(TooltipText, TextAlignmentOptions.TopLeft);

					SetFile(file);
				}

				public void SetFile(ScienceFile file)
				{
					this.file = file;
					ResetSelected();
				}

				public double Update()
				{
					transmitToggle.SetIconColor(file.transmit ? Kolor.Green : Kolor.LightGrey);

					double fileSize = file.Size;
					double scienceValue = Math.Min(fileSize * file.SubjectData.SciencePerMB, file.SubjectData.ScienceRemainingToRetrieve);

					titleInfo.Text = file.SubjectData.ExperimentTitle;
					subjectInfo.Text = file.SubjectData.SituationTitle;

					KsmString ks = KsmString.Get;

					ks.Format(KF.ReadableScience(scienceValue), KF.Bold);
					ks.Format(KF.ReadableDataSize(fileSize), KF.Position(55));
					sizeInfo.Text = ks.GetStringAndClear();

					ks.Format((fileSize / file.SubjectData.ExpInfo.DataSize).ToString("P1"), KF.Bold);
					if (file.dataRate != 0.0)
						ks.Format(file.dataRate > 0.0 ? "+" : "-", KF.ReadableDataRate(Math.Abs(file.dataRate)), KF.Color(file.dataRate > 0.0 ? Kolor.PosRate : Kolor.NegRate), KF.Position(55));
					rateInfo.Text = ks.GetStringAndRelease();

					return scienceValue;
				}

				public string TooltipText()
				{
					KsmString ks = KsmString.Get;
					double fileSize = file.Size;
					double scienceRemainingToRetrieve = file.SubjectData.ScienceRemainingToRetrieve;
					double scienceRetrievedInKSC = file.SubjectData.ScienceRetrievedInKSC;
					ks.Format(file.SubjectData.ExperimentTitle, KF.Bold, KF.Center).Break();
					ks.Format(file.SubjectData.SituationTitle, KF.Center).Break().Break();
					ks.Add("Size", ": ");
					ks.Bold().ReadableDataSize(fileSize).Add(" / ").ReadableDataSize(file.SubjectData.ExpInfo.DataSize).BoldReset();
					ks.Add(" (", (fileSize / file.SubjectData.ExpInfo.DataSize).ToString("P1"), ")");
					ks.Break();

					if (file.dataRate != 0)
					{
						ks.Add("Rate", ": ");
						bool isPosRate = file.dataRate > 0.0;
						double absRate = Math.Abs(file.dataRate);
						ks.Format(isPosRate ? "+" : " -", KF.ReadableDataRate(absRate), KF.Color(isPosRate ? Kolor.PosRate : Kolor.NegRate), KF.Bold);
						if (!isPosRate)
							ks.Add(" (").ReadableCountdown(fileSize / absRate).Add(")");
						ks.Break();
					}
					
					double scienceValue = Math.Min(fileSize * file.SubjectData.SciencePerMB, scienceRemainingToRetrieve);
					ks.Add("Science value", ": ");
					ks.Bold();
					ks.ReadableScience(scienceValue);
					if (scienceRemainingToRetrieve > 0.0)
						ks.Add(" / ").ReadableScience(scienceRemainingToRetrieve);
					ks.BoldReset();
					if (scienceRetrievedInKSC > 0.0)
						ks.Add(" (", "retrieved", ": ").Bold().ReadableScience(scienceRetrievedInKSC).BoldReset().Add(")");
					ks.Break();

					ks.Add("Retrieved", ": ");
					ks.Bold().Add(file.SubjectData.TimesCompleted.ToString(), " ", "times").BoldReset();
					ks.Add(" (", file.SubjectData.PercentRetrieved.ToString("P1"), ")");
					ks.Break();

					if (!string.IsNullOrEmpty(file.ResultText))
					{
						ks.Break();
						ks.Add(file.ResultText);
					}

					return ks.GetStringAndRelease();
				}

				public void UpdateSelectIcon()
				{
					dataSelect.SetIconColor(selected ? Kolor.Green : Kolor.White);
				}

				private void Select()
				{
					selected = !selected;
					UpdateSelectIcon();
				}

				public void ResetSelected()
				{
					selected = false;
					UpdateSelectIcon();
				}

				private void ToggleTransmit()
				{
					file.transmit = !file.transmit;
				}

				private void Delete()
				{
					file.TryRemoveSize();
					driveEntry.fileEntries.Remove(this);
					Destroy();
				}
			}

			private DataManager manager;
			public DriveHandler drive;
			private List<FileEntry> fileEntries = new List<FileEntry>();
			private KsmGuiHeader driveSummary;
			private KsmGuiText driveStats;

			public DriveEntry(DataManager manager, DriveHandler drive) : base(manager.driveEntriesLayout)
			{
				this.manager = manager;
				this.drive = drive;

				drive.computeFilesDataRates = true;
				SetDestroyCallback(() => this.drive.computeFilesDataRates = false);

				SetLayoutElement(true, true);
				driveSummary = new KsmGuiHeader(this, string.Empty);
				driveSummary.TextObject.TextComponent.enableWordWrapping = false;
				driveSummary.TextObject.TextComponent.overflowMode = TextOverflowModes.Ellipsis;
				driveSummary.TextObject.TextComponent.alignment = TextAlignmentOptions.Left;
				driveSummary.TextObject.SetLayoutElement(false, false, 160);
				driveStats = new KsmGuiText(driveSummary, null, TextAlignmentOptions.Center, false, TextOverflowModes.Truncate);
				driveStats.SetLayoutElement(true);
				driveSummary.AddButton(Textures.import32, ImportSelected, "Move selected files here");
				driveSummary.SetLayoutElement(true, false, -1, 18);
			}

			private void ImportSelected()
			{
				foreach (DriveEntry driveEntry in manager.drives)
				{
					foreach (FileEntry fileEntry in driveEntry.fileEntries)
					{
						if (fileEntry.selected)
						{
							fileEntry.ResetSelected();
							if (driveEntry.drive != drive)
								driveEntry.drive.TryMoveFile(fileEntry.file, drive);
						}
					}
				}
			}

			public void UpdateFiles()
			{
				driveSummary.Text = drive.partData.Title;

				KsmString ks = KsmString.Get;
				int fileCount = drive.filesDict.Count;
				if (fileCount > 0)
				{
					ks.Add(fileCount.ToString(), " ", fileCount == 1 ? "file" : "files", " : ");
					ks.ReadableDataSizeCompared(drive.filesSize, drive.definition.FilesCapacity);
				}
				else
				{
					ks.Add("Empty", ", ", "capacity", ": ");
					ks.ReadableDataSize(drive.definition.FilesCapacity);
				}

				driveStats.Text = ks.GetStringAndRelease();

				manager.totalFileCount += fileCount;
				manager.totalFileSize += drive.filesSize;
				manager.totalFileCapacity += drive.definition.FilesCapacity;

				int index = 0;
				foreach (ScienceFile file in drive.Files)
				{
					FileEntry entry;
					if (index >= fileEntries.Count)
					{
						entry = new FileEntry(this, file);
						fileEntries.Add(entry);
					}
					else
					{
						entry = fileEntries[index];
						if (entry.file != file)
						{
							entry.SetFile(file);
						}
					}

					manager.totalFileScience += entry.Update();

					index++;
				}

				while (index < fileEntries.Count)
				{
					int last = fileEntries.Count - 1;
					fileEntries[last].Destroy();
					fileEntries.RemoveAt(last);
				}
			}
		}

		private class SampleStorageEntry : KsmGuiVerticalLayout
		{
			public SampleStorageHandler sampleStorage;
			private List<SampleEntry> sampleEntries = new List<SampleEntry>();
			private KsmGuiText sampleStorageSummary;

			public SampleStorageEntry(KsmGuiBase parent, SampleStorageHandler sampleStorage) : base(parent)
			{
				this.sampleStorage = sampleStorage;
				SetLayoutElement(true, true);
				sampleStorageSummary = new KsmGuiText(this, sampleStorage.StoredPart.protoPart.partInfo.title);
				sampleStorageSummary.SetLayoutElement(true, false, -1, 18);
				SetUpdateAction(UpdateSamples);
			}

			private class SampleEntry : KsmGuiBase
			{
				public ScienceSample sample;
				public SampleStorageEntry sampleStorageEntry;
				private KsmGuiImage dataIcon;
				private KsmGuiText dataInfo;
				private KsmGuiIconButton deleteButton;

				public SampleEntry(SampleStorageEntry parent, ScienceSample sample) : base(parent)
				{
					this.sample = sample;
					sampleStorageEntry = parent;
					SetLayoutElement(true, false, -1, 18);
					dataIcon = new KsmGuiImage(this, Textures.sample32);
					dataIcon.StaticLayout(16, 16);
					dataInfo = new KsmGuiText(this, null, TextAlignmentOptions.TopLeft, false, TextOverflowModes.Truncate);
					dataInfo.StaticLayout(317, 16, 21);
					deleteButton = new KsmGuiIconButton(this, Textures.delete32, Delete);
					deleteButton.StaticLayout(16, 16, 343);
				}

				private void Delete()
				{
					sample.TryRemoveSize();
					sampleStorageEntry.sampleEntries.Remove(this);
					Destroy();
				}

				public void Update()
				{
					dataInfo.Text = sample.SubjectData.FullTitle + " " + (sample.Size * sample.SubjectData.SciencePerMB).ToString("F2");
				}
			}

			private void UpdateSamples()
			{
				sampleStorageSummary.Text = KsmString.Get.Add(sampleStorage.StoredPart.protoPart.partInfo.title, " - ", "inventory", ": ", sampleStorage.partData.Title).GetStringAndRelease();

				int index = 0;
				foreach (ScienceSample sample in sampleStorage.Samples)
				{
					SampleEntry entry;
					if (index >= sampleEntries.Count)
					{
						entry = new SampleEntry(this, sample);
						sampleEntries.Add(entry);
					}
					else
					{
						entry = sampleEntries[index];
						entry.sample = sample;
					}

					entry.Update();

					index++;
				}

				while (index < sampleEntries.Count)
				{
					int last = sampleEntries.Count - 1;
					sampleEntries[last].Destroy();
					sampleEntries.RemoveAt(last);
				}
			}
		}

	}
}
