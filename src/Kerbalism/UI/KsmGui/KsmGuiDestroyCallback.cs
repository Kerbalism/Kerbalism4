using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KERBALISM.KsmGui
{
	public class KsmGuiDestroyCallback : MonoBehaviour
	{
		private Action callback;

		public void SetCallback(Action callback)
		{
			this.callback = callback;
		}

		private void OnDestroy()
		{
			callback();
		}
	}
}
