using System;
using System.Collections;
using System.Collections.Generic;
using Smooth.Compare.Utilities;
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
		public KsmGuiLayoutOptimizer LayoutOptimizer { get; private set; }
		private KsmGuiTooltipBase tooltip;

		private Image colorComponent;

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
				LayoutOptimizer = parent.LayoutOptimizer;
				LayoutOptimizer.SetDirty();
				LayoutOptimizer.RebuildLayout();
				TopTransform.SetParentFixScale(parent.ParentTransformForChilds);
			}
			else
			{
				LayoutOptimizer = TopObject.AddComponent<KsmGuiLayoutOptimizer>();
			}

			TopObject.SetLayerRecursive(5);
		}

		public void SetParent(KsmGuiBase parent)
		{
			LayoutOptimizer = parent.LayoutOptimizer;
			LayoutOptimizer.SetDirty();
			LayoutOptimizer.RebuildLayout();
			TopTransform.SetParentFixScale(parent.ParentTransformForChilds);
		}

		public virtual string Name => GetType().Name;

		public virtual bool Enabled
		{
			get => TopObject.activeSelf;
			set
			{
				if (value == TopObject.activeSelf)
					return;

				TopObject.SetActive(value);

				// enabling/disabling an object almost always require a layout rebuild
				LayoutOptimizer.RebuildLayout();

				// if enabling and update frequency is more than every update, update immediately
				if (value && UpdateHandler != null)
				{
					UpdateHandler.UpdateASAP();
				}
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

		public void SetTooltip(string text, TextAlignmentOptions textAlignement = TextAlignmentOptions.Top, int maxWidth = 300)
		{
			if (ReferenceEquals(tooltip, null))
			{
				tooltip = TopObject.AddComponent<KsmGuiTooltipStaticText>();
				((KsmGuiTooltipStaticText)tooltip).Setup(text, textAlignement, maxWidth);
			}
			else if (tooltip is KsmGuiTooltipStaticText staticTextTooltip)
			{
				staticTextTooltip.SetText(text);
			}
			else
			{
				Lib.Log($"Can't set tooltip text : tooltip isn't defined or isn't a static text tooltip", Lib.LogLevel.Error);
			}
		}

		public void SetTooltip(Func<string> textFunc, TextAlignmentOptions textAlignement = TextAlignmentOptions.Top, int maxWidth = 300)
		{
			if (ReferenceEquals(tooltip, null))
				tooltip = TopObject.AddComponent<KsmGuiTooltipDynamicText>();

			((KsmGuiTooltipDynamicText)tooltip).Setup(textFunc, textAlignement, maxWidth);
		}

		public void SetTooltip(Func<KsmGuiBase> contentBuilder)
		{
			if (ReferenceEquals(tooltip, null))
				tooltip = TopObject.AddComponent<KsmGuiTooltipDynamicContent>();

			((KsmGuiTooltipDynamicContent)tooltip).Setup(contentBuilder);
		}

		public void SetTooltipEnabled(bool enabled)
		{
			if (!ReferenceEquals(tooltip, null))
			{
				tooltip.TooltipEnabled = enabled;
				if (!enabled && KsmGuiTooltipController.Instance.CurrentTooltip == tooltip)
				{
					KsmGuiTooltipController.Instance.HideTooltip();
				}
			}
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

		public enum HorizontalEdge { Left, Right }
		public enum VerticalEdge { Top, Bottom }

		public void StaticLayout(int width, int height, int horizontalOffset = 0, int verticalOffset = 0, HorizontalEdge horizontalEdge = HorizontalEdge.Left, VerticalEdge verticalEdge = VerticalEdge.Top)
		{
			TopTransform.anchorMin = new Vector2(horizontalEdge == HorizontalEdge.Left ? 0f : 1f, verticalEdge == VerticalEdge.Top ? 0f : 1f);
			TopTransform.anchorMax = TopTransform.anchorMin;

			TopTransform.sizeDelta = new Vector2(width, height);
			TopTransform.anchoredPosition = new Vector2(
				horizontalEdge == HorizontalEdge.Left ? horizontalOffset + width * TopTransform.pivot.x : horizontalOffset - width * (1f - TopTransform.pivot.x),
				verticalEdge == VerticalEdge.Top ? verticalOffset + height * TopTransform.pivot.y : verticalOffset - height * (1f - TopTransform.pivot.y));
		}

		/// <summary>
		/// Stretch the object transform to match its parent size and position. Only works if the parent has no layout component
		/// </summary>
		public void StaticLayoutStretchInParent()
		{
			TopTransform.anchorMin = Vector2.zero;
			TopTransform.anchorMax = Vector2.one;
			TopTransform.sizeDelta = Vector2.zero;
		}

		public void RebuildLayout() => LayoutOptimizer.RebuildLayout();



		public void MoveAsFirstChild()
		{
			TopTransform.SetAsFirstSibling();
		}

		public void MoveAsLastChild()
		{
			TopTransform.SetAsLastSibling();
		}

		public void MoveToSiblingIndex(int index)
		{
			TopTransform.SetSiblingIndex(index);
		}

		public bool MoveAfter(KsmGuiBase afterThis)
		{
			for (int i = 0; i < TopTransform.childCount; i++)
			{
				if (TopTransform.GetChild(i).transform == afterThis.TopTransform)
				{
					TopTransform.SetSiblingIndex(i + 1);
					return true;
				}
			}

			return false;
		}

		public void SetDestroyCallback(Action callback)
		{
			KsmGuiDestroyCallback destroyCallback = TopObject.AddComponent<KsmGuiDestroyCallback>();
			destroyCallback.SetCallback(callback);
		}

		public void Destroy()
		{
			TopObject.DestroyGameObject();
			RebuildLayout();
		}




		/// <summary>
		/// Add a Color component with the specified color to the top GameObject, or change the existing color of the component.
		/// The GameObject can't already have a graphic component (image, text...).
		/// </summary>
		/// <param name="color">If set to default, will add a black color with 20% transparency</param>
		public void SetColor(Color color)
		{
			Image image = null;

			foreach (Graphic graphicComponent in TopObject.GetComponents<Graphic>())
			{
				if (graphicComponent is Image)
				{
					colorComponent = (Image) graphicComponent;
					break;
				}
				else
				{
					Lib.LogDebugStack($"Can't set background color on {this}, it already has a graphic component", Lib.LogLevel.Warning);
					return;
				}

			}

			if (colorComponent == null)
			{
				colorComponent = TopObject.AddComponent<Image>();
			}

			colorComponent.color = color;
		}

		public void SetBoxColor() => SetColor(KsmGuiStyle.boxColor);
	}
}
