using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Color = UnityEngine.Color;

namespace KERBALISM.KsmGui
{
	public class KsmGuiBase
	{
		public RectTransform TopTransform { get; private set; }
		public GameObject TopObject { get; private set; }
		public LayoutElement LayoutElement { get; private set; }
		public KsmGuiUpdateHandler UpdateHandler { get; private set; }
		private KsmGuiTooltipBase tooltip;
		private KsmGuiLayoutOptimizer layoutOptimizer;

		/// <summary>
		/// transform that will be used as parent for child KsmGui objects.
		/// override this if you have an internal object hierarchy where child
		/// objects must be parented to a specific transform (ex : scroll view)
		/// </summary>
		public virtual RectTransform ParentTransformForChilds => TopTransform;

		public KsmGuiBase(KsmGuiBase parent)
		{
			TopObject = new GameObject(Name);
			TopTransform = TopObject.AddComponent<RectTransform>();
			TopObject.AddComponent<CanvasRenderer>();

			if (parent != null)
			{
				layoutOptimizer = parent.layoutOptimizer;
				layoutOptimizer.SetDirty();
				layoutOptimizer.RebuildLayout();
				TopTransform.SetParentFixScale(parent.ParentTransformForChilds);
			}
			else
			{
				layoutOptimizer = TopObject.AddComponent<KsmGuiLayoutOptimizer>();
			}

			TopObject.SetLayerRecursive(5);
		}

		public virtual string Name => GetType().Name;

		public virtual bool Enabled
		{
			get => TopObject.activeSelf;
			set
			{
				TopObject.SetActive(value);

				// enabling/disabling an object almost always require a layout rebuild
				layoutOptimizer.RebuildLayout();

				// if enabling and update frequency is more than every update, update immediately
				if (value && UpdateHandler != null)
					UpdateHandler.UpdateASAP();
			}
		}

		/// <summary> callback that will be called on this object Update(). Won't be called if Enabled = false </summary>
		/// <param name="updateFrequency">seconds between updates, or set to 0f to update every frame</param>
		public void SetUpdateAction(Action action, float updateFrequency = 0.2f)
		{
			if (UpdateHandler == null)
				UpdateHandler = TopObject.AddComponent<KsmGuiUpdateHandler>();

			UpdateHandler.updateAction = action;
			UpdateHandler.updateFrequency = updateFrequency;
			UpdateHandler.UpdateASAP();
		}

		/// <summary> coroutine-like (IEnumerable) method that will be called repeatedly as long as Enabled = true </summary>
		public void SetUpdateCoroutine(KsmGuiUpdateCoroutine coroutineFactory)
		{
			if (UpdateHandler == null)
				UpdateHandler = TopObject.AddComponent<KsmGuiUpdateHandler>();

			UpdateHandler.coroutineFactory = coroutineFactory;
		}

		public void ForceExecuteCoroutine(bool fromStart = false)
		{
			if (UpdateHandler != null)
				UpdateHandler.ForceExecuteCoroutine(fromStart);
		}

		public void SetTooltipText(string text, TextAlignmentOptions textAlignement = TextAlignmentOptions.Top, float width = -1f, Func<KsmGuiBase> content = null)
		{
			if (text == null)
				return;

			if (ReferenceEquals(tooltip, null))
				tooltip = TopObject.AddComponent<KsmGuiTooltipStatic>();

			((KsmGuiTooltipStatic)tooltip).SetTooltipText(text, textAlignement, width, content);
		}

		public void SetTooltipText(Func<string> tooltipTextFunc, TextAlignmentOptions textAlignement = TextAlignmentOptions.Top, float width = -1f, Func<KsmGuiBase> content = null)
		{
			if (ReferenceEquals(tooltip, null))
				tooltip = TopObject.AddComponent<KsmGuiTooltipDynamic>();

			((KsmGuiTooltipDynamic)tooltip).SetTooltipText(tooltipTextFunc, textAlignement, width, content);
		}

		/// <summary> Add sizing constraints trough a LayoutElement component</summary>
		public void SetLayoutElement(bool flexibleWidth = false, bool flexibleHeight = false, int preferredWidth = -1, int preferredHeight = -1, int minWidth = -1, int minHeight = -1)
		{
			if (LayoutElement == null)
				LayoutElement = TopObject.AddComponent<LayoutElement>();

			LayoutElement.flexibleWidth = flexibleWidth ? 1f : -1f;
			LayoutElement.flexibleHeight = flexibleHeight ? 1f : -1f;
			LayoutElement.preferredWidth = preferredWidth;
			LayoutElement.preferredHeight = preferredHeight;
			LayoutElement.minWidth = minWidth;
			LayoutElement.minHeight = minHeight;
		}

		public void RebuildLayout() => layoutOptimizer.RebuildLayout();

		/// <summary>
		/// Stretch the object transform to match its parent size and position. Only works the parent has no layout component
		/// </summary>
		public void NoLayoutStretchInParent()
		{
			TopTransform.anchorMin = Vector2.zero;
			TopTransform.anchorMax = Vector2.one;
			TopTransform.sizeDelta = Vector2.zero;
		}

		public void MoveAsFirstChild()
		{
			TopTransform.SetAsFirstSibling();
		}

		public void MoveAsLastChild()
		{
			TopTransform.SetAsLastSibling();
		}



		/// <summary>
		/// Add a Color component with the specified color to the top GameObject, or change the existing color of the component.
		/// The GameObject can't already have a graphic component (image, text...).
		/// </summary>
		/// <param name="color">If set to default, will add a black color with 20% transparency</param>
		public void SetColor(Color color = default)
		{
			if (color == default)
			{
				color = KsmGuiStyle.boxColor;
			}

			Image image = null;

			foreach (Graphic graphicComponent in TopObject.GetComponents<Graphic>())
			{
				if (graphicComponent is Image)
				{
					image = (Image) graphicComponent;
					break;
				}
				else
				{
					Lib.LogDebugStack($"Can't set background color on {this}, it already has a graphic component", Lib.LogLevel.Warning);
					return;
				}

			}

			if (image == null)
			{
				image = TopObject.AddComponent<Image>();
			}

			image.color = color;
		}
	}
}
