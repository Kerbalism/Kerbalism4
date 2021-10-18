using KERBALISM.KsmGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	public class DataManager : KsmGuiVerticalLayout
	{
		private KsmGuiText summary;
		private KsmGuiIconButton deleteAll;
		private KsmGuiIconButton transferResetAll;
		private KsmGuiIconButton transmitDefault; // TODO : implement a persisted vessel-wide toggle
		private KsmGuiIconButton analyzeDefault; // TODO : implement a persisted vessel-wide toggle
		public DataManager(KsmGuiBase parent) : base(parent)
		{
			summary = new KsmGuiText(this);
			summary.SetLayoutElement(true, false, -1, 18);
		}

		private class DriveEntry : KsmGuiVerticalLayout
		{
			private DriveHandler drive;
			private List<DataEntry> dataEntries = new List<DataEntry>();
			private KsmGuiIconButton transferButton;
			private KsmGuiText driveSummary;

			public DriveEntry(DataManager parent, DriveHandler drive) : base(parent)
			{

			}

			private abstract class DataEntry : KsmGuiBase
			{
				protected DriveEntry driveEntry;
				protected KsmGuiImage dataIcon;
				protected KsmGuiText dataInfo;
				protected KsmGuiIconButton deleteButton;
				protected KsmGuiIconButton processToggle; // transmit (file) or process in a lab (sample)
				protected KsmGuiIconButton transferToggle;
				public bool transfer;

				public DataEntry(DriveEntry parent) : base(parent)
				{
					driveEntry = parent;
					dataIcon = new KsmGuiImage(this, null);
					dataIcon.StaticLayout(16, 16);
					dataInfo = new KsmGuiText(this);
					dataInfo.StaticLayout(150, 16, 16 + 5);
					deleteButton = new KsmGuiIconButton(this, Textures.export32, Transfer);
					deleteButton.StaticLayout(16, 16, 16 + 5 + 160 + 5);
					deleteButton.SetIconColor(Kolor.Red);
					processToggle = new KsmGuiIconButton(this, null, Process);
					processToggle.StaticLayout(16, 16, 16 + 5 + 160 + 5 + 16 + 5);
					transferToggle = new KsmGuiIconButton(this, Textures.delete32, Delete);
					transferToggle.StaticLayout(16, 16, 16 + 5 + 160 + 5 + 16 + 5 + 16 + 5);
				}

				private void Transfer()
				{
					transfer = !transfer;
				}

				protected abstract void Process();

				protected void ToggleProcessIcon(bool process)
				{
					processToggle.SetIconColor(process ? Kolor.Green : Kolor.LightGrey);
				}

				protected virtual void Delete()
				{
					driveEntry.dataEntries.Remove(this);
					Destroy();
				}
			}

			private class FileEntry : DataEntry
			{
				private DriveFile file;

				public FileEntry(DriveEntry parent, DriveFile file) : base(parent)
				{
					this.file = file;
					dataIcon.SetIconTexture(Textures.file32);
					processToggle.SetIconTexture(Textures.transmit32);
					ToggleProcessIcon(file.transmit);
				}

				protected override void Process()
				{
					file.transmit = !file.transmit;
					ToggleProcessIcon(file.transmit);
				}

				protected override void Delete()
				{
					driveEntry.drive.DeleteFile(file.subjectData);
					base.Delete();
				}
			}

			private class SampleEntry : DataEntry
			{
				private Sample sample;

				public SampleEntry(DriveEntry parent, Sample sample) : base(parent)
				{
					this.sample = sample;
					dataIcon.SetIconTexture(Textures.sample32);
					processToggle.SetIconTexture(Textures.lab32);
					ToggleProcessIcon(sample.analyze);
				}

				protected override void Process()
				{
					sample.analyze = !sample.analyze;
					ToggleProcessIcon(sample.analyze);
				}

				protected override void Delete()
				{
					driveEntry.drive.DeleteSample(sample.subjectData);
					base.Delete();
				}
			}
		}
	}
}
