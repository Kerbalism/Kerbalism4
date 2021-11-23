using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KERBALISM.KsmGui
{
	public class KsmGuiBox : KsmGuiBase
	{
		public KsmGuiBox(KsmGuiBase parent, Kolor backgroundColor = null) : base(parent)
		{
			RawImage image = TopObject.AddComponent<RawImage>();

			if (backgroundColor == null)
				image.color = Color.clear;
			else
				image.color = KsmGuiStyle.boxColor;

		}
	}
}
