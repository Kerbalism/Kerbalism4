using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace KERBALISM.KsmGui
{
	public class KsmGuiUpdateCoroutine : IEnumerable
	{
		Func<IEnumerator> updateMethod;
		public KsmGuiUpdateCoroutine(Func<IEnumerator> updateMethod) => this.updateMethod = updateMethod;
		public IEnumerator GetEnumerator() => updateMethod();
	}

	public class KsmGuiUpdateHandler : MonoBehaviour
	{
		private float lastUpdate;
		public float updateFrequency = 0.2f;
		public Action updateAction;
		public KsmGuiUpdateCoroutine coroutineFactory;
		public IEnumerator currentCoroutine;

		public void UpdateASAP()
		{
			lastUpdate = float.MinValue;
		}

		void Start()
		{
			UpdateASAP();
		}

		void Update()
		{
			if (updateAction != null)
			{
				if (updateFrequency <= 0f || lastUpdate + updateFrequency < Time.unscaledTime)
				{
					lastUpdate = Time.unscaledTime;

					Profiler.BeginSample(updateAction.Target + "." + updateAction.Method.Name + "()");
					updateAction();
					Profiler.EndSample();
				}
			}

			if (coroutineFactory != null)
			{
				if (currentCoroutine == null || !currentCoroutine.MoveNext())
					currentCoroutine = coroutineFactory.GetEnumerator();
			}
		}

		public void ForceExecuteCoroutine(bool fromStart = false)
		{
			if (coroutineFactory == null)
				return;

			if (fromStart || currentCoroutine == null || !currentCoroutine.MoveNext())
				currentCoroutine = coroutineFactory.GetEnumerator();

			while (currentCoroutine.MoveNext()) { }
		}

	}
}
