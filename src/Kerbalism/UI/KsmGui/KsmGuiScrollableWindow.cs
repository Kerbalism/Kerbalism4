using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KERBALISM.KsmGui
{
	/// <summary>
	/// A window
	/// - Whose max height is tweakable by the user by dragging its bottom
	/// - Whose current height is reduced to its content height when less than the max height
	/// - That put its content in a scroll view when its content height is higher than the max height
	/// - That limit its max height to the screen height
	/// - That delegate that height handling to its content scroll views if there are any, instead of its own scroll view
	/// </summary>
	public class KsmGuiScrollableWindow : KsmGuiBase
	{
		public override RectTransform ParentTransformForChilds => contentTransform;

		public bool destroyOnClose;
		public Action OnClose { get; set; }

		private float staticWidth;
		private float staticHeight;
		private float maxHeight;

		private GameObject windowObject;
		private RectTransform windowTransform;

		private KsmGuiInputLock inputLockManager;

		private ScrollRectNoDrag scrollRect;
		private GameObject scrollbar;

		private RectTransform viewportTransform;
		private RectTransform contentTransform;

		private bool scrollViewEnabled;

		public KsmGuiScrollableWindow(
			float opacity = 0.8f,
			int staticWidth = -1, int staticHeight = -1, int maxHeight = -1,
			TextAnchor screenAnchor = TextAnchor.MiddleCenter,
			TextAnchor windowPivot = TextAnchor.MiddleCenter,
			int posX = 0, int posY = 0
			)
		{
			// WINDOW OBJECT SETUP

			windowObject = new GameObject("KsmGuiScrollableWindow");
			windowTransform = windowObject.AddComponent<RectTransform>();
			windowObject.AddComponent<CanvasRenderer>();

			windowTransform.SetAnchorsAndPosition(screenAnchor, windowPivot, posX, posY);
			windowTransform.SetParentFixScale(KsmGuiMasterController.Instance.KsmGuiTransform);
			windowTransform.localScale = Vector3.one;

			inputLockManager = windowObject.AddComponent<KsmGuiInputLock>();
			inputLockManager.rectTransform = windowTransform;

			DragPanel dragPanel = windowObject.AddComponent<DragPanel>();
			dragPanel.edgeOffset = 0;

			Image img = windowObject.AddComponent<Image>();
			img.sprite = Textures.KsmGuiSpriteBackground;
			img.type = Image.Type.Sliced;
			img.color = new Color(1.0f, 1.0f, 1.0f, opacity);

			// SCROLLVIEW SETUP

			scrollRect = windowObject.AddComponent<ScrollRectNoDrag>();
			//scrollRect.dragHandler = dragPanel;
			scrollRect.horizontal = false;
			scrollRect.vertical = true;
			scrollRect.movementType = ScrollRect.MovementType.Elastic;
			scrollRect.elasticity = 0.1f;
			scrollRect.inertia = true;
			scrollRect.decelerationRate = 0.15f;
			scrollRect.scrollSensitivity = 10f;
			scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
			scrollRect.verticalScrollbarSpacing = 0f;

			// viewport object (child of top object)
			GameObject viewport = new GameObject("viewport");
			viewportTransform = viewport.AddComponent<RectTransform>();
			viewport.AddComponent<CanvasRenderer>();

			// Note : using a standard "Mask" has a bug where scrollrect content is visible
			// in other windows scollrects (like if all masks were "global" for all masked content)
			// see https://issuetracker.unity3d.com/issues/scroll-view-content-is-visible-outside-of-mask-when-there-is-another-masked-ui-element-in-the-same-canvas
			// using a RectMask2D fixes it, at the cost of the ability to use an image mask (but we don't care)
			RectMask2D rectMask = viewport.AddComponent<RectMask2D>();

			viewportTransform.SetParentFixScale(windowTransform);
			scrollRect.viewport = viewportTransform;

			// content object (child of viewport)
			GameObject contentObject = new GameObject("content");
			contentTransform = contentObject.AddComponent<RectTransform>();
			contentTransform.anchorMin = new Vector2(0f, 1f);
			contentTransform.anchorMax = new Vector2(1f, 1f);
			contentTransform.pivot = new Vector2(0f, 1f);
			contentTransform.anchoredPosition = new Vector2(0f, 0f);
			contentTransform.sizeDelta = new Vector2(0f, 0f);


			LayoutFitter fitter = windowObject.AddComponent<LayoutFitter>();
			fitter.window = this;

			ContentSizeFitter scrollViewFitter = contentObject.AddComponent<ContentSizeFitter>();
			scrollViewFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; // is unconstrained on a default scroll view. Will that work ??
			scrollViewFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

			VerticalLayoutGroup contentGroup = contentObject.AddComponent<VerticalLayoutGroup>();
			contentGroup.padding = new RectOffset(5, 5, 5, 5);
			contentGroup.spacing = 0;
			contentGroup.childAlignment = TextAnchor.UpperLeft;
			contentGroup.childControlHeight = true;
			contentGroup.childControlWidth = true;
			contentGroup.childForceExpandHeight = false;
			contentGroup.childForceExpandWidth = false;

			contentTransform.SetParentFixScale(viewportTransform);
			scrollRect.content = contentTransform;

			// scrollbar (child of top object)
			scrollbar = new GameObject("scrollbar");
			RectTransform scrollbarTransform = scrollbar.AddComponent<RectTransform>();
			scrollbarTransform.anchorMin = new Vector2(1f, 0f);
			scrollbarTransform.anchorMax = new Vector2(1f, 1f);
			scrollbarTransform.pivot = new Vector2(1f, 1f);
			scrollbarTransform.anchoredPosition = new Vector2(0f, 0f);
			scrollbarTransform.sizeDelta = new Vector2(10f, 0f); // scrollbar width
			scrollbar.AddComponent<CanvasRenderer>();

			Image scrollBarImage = scrollbar.AddComponent<Image>();
			scrollBarImage.color = Color.black;

			Scrollbar scrollbarComponent = scrollbar.AddComponent<Scrollbar>();
			scrollbarComponent.interactable = true;
			scrollbarComponent.transition = Selectable.Transition.ColorTint;
			scrollbarComponent.colors = new ColorBlock()
			{
				normalColor = Color.white,
				highlightedColor = Color.white,
				pressedColor = new Color(0.8f, 0.8f, 0.8f),
				disabledColor = new Color(0.8f, 0.8f, 0.8f, 0.5f),
				colorMultiplier = 1f,
				fadeDuration = 0.1f
			};
			scrollbarComponent.navigation = new Navigation() { mode = Navigation.Mode.None };
			scrollbarComponent.direction = Scrollbar.Direction.BottomToTop;
			scrollRect.verticalScrollbar = scrollbarComponent;

			scrollbarTransform.SetParentFixScale(windowTransform);

			// scrollbar sliding area
			GameObject slidingArea = new GameObject("slidingArea");
			RectTransform slidingAreaTransform = slidingArea.AddComponent<RectTransform>();
			slidingAreaTransform.anchorMin = new Vector2(0f, 0f);
			slidingAreaTransform.anchorMax = new Vector2(1f, 1f);
			slidingAreaTransform.pivot = new Vector2(0.5f, 0.5f);
			slidingAreaTransform.anchoredPosition = new Vector2(5f, 5f);
			slidingAreaTransform.sizeDelta = new Vector2(5f, 5f); // scrollbar width / 2
			slidingAreaTransform.SetParentFixScale(scrollbarTransform);

			// scrollbar handle
			GameObject scrollbarHandle = new GameObject("scrollbarHandle");
			RectTransform handleTransform = scrollbarHandle.AddComponent<RectTransform>();
			scrollbarHandle.AddComponent<CanvasRenderer>();
			handleTransform.anchorMin = new Vector2(0f, 0f);
			handleTransform.anchorMax = new Vector2(1f, 1f);
			handleTransform.pivot = new Vector2(0.5f, 0.5f);
			handleTransform.anchoredPosition = new Vector2(-5f, -5f); // relative to sliding area width
			handleTransform.sizeDelta = new Vector2(-6f, -6f); // relative to sliding area width
			scrollbarComponent.handleRect = handleTransform;

			Image handleImage = scrollbarHandle.AddComponent<Image>();
			handleImage.color = new Color(0.4f, 0.4f, 0.4f);
			handleTransform.SetParentFixScale(slidingAreaTransform);
			scrollbarComponent.targetGraphic = handleImage;

			// KSMGUIBASE SETUP
			WindowSetup(contentObject);
			windowObject.SetLayerRecursive(5);
			this.staticWidth = staticWidth;
			this.staticHeight = staticHeight;
			this.maxHeight = maxHeight;

			//EnableScrollView(false);
		}

		private void EnableScrollView(bool enabled)
		{
			scrollViewEnabled = enabled;
			scrollRect.vertical = enabled;
		}

		/// <summary>
		/// Set the window size. If the width/height is set to -1, the window size
		/// will match its content size as defined by the content layout components.
		/// </summary>
		public void SetStaticSize(int width = -1, int height = -1, int maxHeight = -1)
		{
			staticWidth = width;
			staticHeight = height;
			this.maxHeight = maxHeight;
			UpdateSize();
		}

		private void UpdateSize()
		{
			Vector2 sizeDelta = windowTransform.sizeDelta;

			if (staticWidth <= 0f)
			{
				sizeDelta.x = contentTransform.rect.size.x;
			}
			else
			{
				sizeDelta.x = staticWidth;
			}

			if (scrollViewEnabled)
			{
				sizeDelta.x += 15f;
			}

			if (staticHeight <= 0f)
			{
				if (contentTransform.rect.size.y > maxHeight)
				{
					sizeDelta.y = maxHeight;
				}
				else
				{
					sizeDelta.y = contentTransform.rect.size.y;
				}
			}
			else
			{
				sizeDelta.y = staticHeight;
			}

			if (sizeDelta != windowTransform.sizeDelta)
			{
				windowTransform.sizeDelta = sizeDelta;
			}

			if (contentTransform.rect.size.y > sizeDelta.y)
			{
				if (!scrollViewEnabled)
					EnableScrollView(true);
			}
			else
			{
				if (scrollViewEnabled)
					EnableScrollView(false);
			}
		}

		public virtual void OnSceneChange(GameScenes data) => Close();

		public void Close()
		{
			if (OnClose != null)
				OnClose();

			if (destroyOnClose)
				Object.Destroy(windowObject);
			else
				Enabled = false;
		}

		private void OnDestroy()
		{
			GameEvents.onGameSceneLoadRequested.Remove(OnSceneChange);
		}

		public bool IsHovering => inputLockManager.IsHovering;

		public void SetOnPointerEnterAction(Action action) => inputLockManager.onPointerEnterAction = action;

		public void SetOnPointerExitAction(Action action) => inputLockManager.onPointerExitAction = action;

		public void StartCoroutine(IEnumerator routine) => inputLockManager.StartCoroutine(routine);

		public override bool Enabled
		{
			get => windowObject.activeSelf;
			set
			{
				if (value == windowObject.activeSelf)
					return;

				windowObject.SetActive(value);

				// enabling/disabling an object almost always require a layout rebuild
				LayoutOptimizer.RebuildLayout();

				// if enabling and update frequency is more than every update, update immediately
				if (value && UpdateHandler != null)
				{
					UpdateHandler.UpdateASAP();
				}
			}
		}

		private class LayoutFitter : MonoBehaviour, ICanvasElement
		{
			public float height
			{
				get => window.staticHeight;
				set => window.staticHeight = value;
			}

			private void Awake()
			{
				CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild(this);
			}

			public KsmGuiScrollableWindow window;

			public void Rebuild(CanvasUpdate executing)
			{

			}

			public void LayoutComplete()
			{
				window.UpdateSize();
				CanvasUpdateRegistry.RegisterCanvasElementForGraphicRebuild(this);
			}

			public void GraphicUpdateComplete()
			{
				CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild(this);
			}

			public bool IsDestroyed()
			{
				return false;
			}
		}
	}

	public class ScrollRectNoDrag : ScrollRect
	{
		public MonoBehaviour dragHandler;


		public override void OnInitializePotentialDrag(PointerEventData eventData)
		{
			if (dragHandler is IInitializePotentialDragHandler handler)
			{
				handler.OnInitializePotentialDrag(eventData);
			}
		}

		public override void OnBeginDrag(PointerEventData eventData)
		{
			if (dragHandler is IBeginDragHandler handler)
			{
				handler.OnBeginDrag(eventData);
			}
		}

		public override void OnDrag(PointerEventData eventData)
		{
			if (dragHandler is IDragHandler handler)
			{
				handler.OnDrag(eventData);
			}
		}

		public override void OnEndDrag(PointerEventData eventData)
		{
			if (dragHandler is IEndDragHandler handler)
			{
				handler.OnEndDrag(eventData);
			}
		}
	}
}
