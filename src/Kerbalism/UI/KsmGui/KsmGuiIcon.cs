using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KERBALISM.KsmGui
{
	public class KsmGuiIcon : KsmGuiBase
	{
		public RawImage Image { get; private set; }
		public RectTransform IconTransform { get; private set; }

		public KsmGuiIcon(KsmGuiBase parent, Texture2D texture, int iconWidth = -1, int iconHeight = -1) : base(parent)
		{
			// we use a child gameobject because KsmGuiIcon can be used as a button (we need it as a child in this case)
			// we directly set its size trough anchors / sizeDelta instead of using layout components, this way it can be used
			// both standalone or as a button without having to mess with the layout component
			// We still set a min height/min size layout on the top object to make sure other objects in a group won't overlap

			GameObject icon = new GameObject("icon");
			IconTransform = icon.AddComponent<RectTransform>();
			icon.AddComponent<CanvasRenderer>();

			Image = icon.AddComponent<RawImage>();

			// make sure pivot is at the center
			IconTransform.pivot = new Vector2(0.5f, 0.5f);

			// anchor-pivot distance
			IconTransform.anchoredPosition = Vector2.zero;

			SetIconSize(iconWidth, iconHeight);
			SetIconTexture(texture);

			IconTransform.SetParentFixScale(TopTransform);
		}

		public void SetIconSize(int width = -1, int height = -1)
		{
			if (width <= 0 || height <= 0)
			{
				// set anchors to stretch in parent
				IconTransform.anchorMin = Vector2.zero;
				IconTransform.anchorMax = Vector2.one;
				IconTransform.sizeDelta = Vector2.zero;
			}
			else
			{
				// set anchors to middle-center
				IconTransform.anchorMin = new Vector2(0.5f, 0.5f);
				IconTransform.anchorMax = new Vector2(0.5f, 0.5f);
				IconTransform.sizeDelta = new Vector2(width, height);
			}
		}

		public void SetIconTexture(Texture2D texture)
		{
			Image.texture = texture;
		}

		public void SetIconColor(Color color)
		{
			Image.color = color;
		}

		[Obsolete("Use a Kolor instead")]
		public void SetIconColor(Lib.Kolor kolor)
		{
			Image.color = Lib.KolorToColor(kolor);
		}

		public void SetIconColor(Kolor kolor)
		{
			Image.color = kolor.color;
		}
	}
}
