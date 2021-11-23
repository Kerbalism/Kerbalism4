using System;
using System.Collections.Generic;
using static KERBALISM.DriveHandler;

namespace KERBALISM
{
	public class ModuleKsmDrive : KsmPartModule<ModuleKsmDrive, DriveHandler, DriveDefinition>,
		IScienceDataContainer
	{
		// IScienceDataContainer implementation
		// The sole purpose of this is to allow mods using custom logic to search
		// for a science container to store some science data to find one. Normal
		// stock experiments are handled through the stock science dialog hijacker.
		// We restrict the implementation to the bare minimum for that to work. While
		// we could technically implement everything, it's a bad idea, as we need to
		// convert our KsmScienceData objects to ScienceData objects :
		// - This can cause massive GC allocations if this is done every frame
		// - The conversion round-trip isn't lossless in all cases
		// And anyway, letting mods manipulate our data using the stock science system
		// assumptions is a bad idea.

		public void ReviewData()
		{
			//UI.Open((p) => p.Fileman(vessel));
		}

		public void ReviewDataItem(ScienceData data) => ReviewData();

		public void DumpData(ScienceData data) { }

		private static readonly ScienceData[] emptyStockDataArray = new ScienceData[0];
		public ScienceData[] GetData() => emptyStockDataArray;

		// Can't implement this without causing issues on EVA Kerbal boarding actions  
		public int GetScienceCount() => 0;

		public bool IsRerunnable() => false;

		public void ReturnData(ScienceData data)
		{
			SubjectData subjectData = ScienceDB.GetSubjectDataFromStockId(data.subjectID);
			if (subjectData == null)
				return;

			double size = data.dataAmount;
			DriveHandler drive = moduleHandler;
			IEnumerator<DriveHandler> vesselDrives = GetAllDrives(moduleHandler.VesselData).GetEnumerator();
			while (size > 0.0 && vesselDrives.MoveNext())
			{
				KsmScienceData convertedData = drive.RecordFile(subjectData, size, true, data.extraResultString, true);
				size -= convertedData.Size;
				drive = vesselDrives.Current;
			}
		}
	}
}
