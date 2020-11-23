using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KERBALISM.Modules
{
	public class TestComponent : MonoBehaviour
	{

	}

	public class NullCheckTestModule : PartModule
	{
		TestComponent test;

		[KSPEvent(name = "create", active = true, guiActive = true, guiActiveEditor = true)]
		public void CreateComponent()
		{
			Lib.LogDebug($"Creating...");
			test = part.gameObject.AddComponent<TestComponent>();
		}

		[KSPEvent(name = "destroy", active = true, guiActive = true, guiActiveEditor = true)]
		public void DestroyComponent()
		{
			Lib.LogDebug($"destroying...");
			Destroy(test);
		}

		[KSPEvent(name = "destroy and set ref to null", active = true, guiActive = true, guiActiveEditor = true)]
		public void DestroyComponentImmediate()
		{
			Lib.LogDebug($"destroying...");
			Destroy(test);
			test = null;
		}

		[KSPEvent(name = "test", active = true, guiActive = true, guiActiveEditor = true)]
		public void TestComponent()
		{
			Lib.LogDebug($"testing...");
			Check();
		}

		private void Check()
		{
			Lib.LogDebug($"test == null : {test == null}");
			Lib.LogDebug($"test.Equals(null) : {test.Equals(null)}");
			Lib.LogDebug($"object.ReferenceEquals(test, null) : {object.ReferenceEquals(test, null)}");
			Lib.LogDebug($"test isn't UnityEngine.Object : {!(test is UnityEngine.Object)}");
		}

		[KSPEvent(name = "TestPerf", active = true, guiActive = true, guiActiveEditor = true)]
		public void TestPerf()
		{
			Stopwatch watch = new Stopwatch();
			bool blah = false;

			watch.Start();
			for (int i = 0; i < 10000000; i++)
			{
				blah = test == null;
			}
			watch.Stop();
			Lib.LogDebug($"test == null : {blah} - {watch.ElapsedMilliseconds}ms");
			watch.Reset();

			watch.Start();
			for (int i = 0; i < 10000000; i++)
			{
				blah = test.Equals(null);
			}
			watch.Stop();
			Lib.LogDebug($"test.Equals(null) : {blah} - {watch.ElapsedMilliseconds}ms");
			watch.Reset();

			watch.Start();
			for (int i = 0; i < 10000000; i++)
			{
				blah = ReferenceEquals(test, null);
			}
			watch.Stop();
			Lib.LogDebug($"ReferenceEquals(test, null) : {blah} - {watch.ElapsedMilliseconds}ms");
			watch.Reset();

			watch.Start();
			for (int i = 0; i < 10000000; i++)
			{
				blah = !(test is UnityEngine.Object);
			}
			watch.Stop();
			Lib.LogDebug($"test isn't UnityEngine.Object : {blah} - {watch.ElapsedMilliseconds}ms");
			watch.Reset();
		}

	}
}
