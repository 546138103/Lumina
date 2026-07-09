using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-800)]
[DisallowMultipleComponent]
public sealed class PoseCameraPreviewUI : MonoBehaviour
{
    private const KeyCode VisibilityToggleKey = KeyCode.V;
    private const float MissingFrameTimeoutSeconds = 1.5f;
    private const float PreviewAspectRatio = 4f / 3f;

    [SerializeField] private PoseCameraPreviewReceiver receiver;

    private GameObject generatedCanvas;
    private GameObject previewRoot;
    private RawImage previewImage;
    private bool userVisible = true;

    private void Awake()
    {
        ResolveReceiver();
        EnsurePreviewUI();
    }

    private void OnEnable()
    {
        ResolveReceiver();

        if (receiver != null)
        {
            receiver.FrameUpdated += HandleFrameUpdated;
        }
    }

    private void Update()
    {
        if (IsShiftHeld() && Input.GetKeyDown(VisibilityToggleKey))
        {
            userVisible = !userVisible;
        }

        bool shouldShow =
            userVisible &&
            receiver != null &&
            receiver.HasRecentFrame(MissingFrameTimeoutSeconds);

        if (previewRoot != null && previewRoot.activeSelf != shouldShow)
        {
            previewRoot.SetActive(shouldShow);
        }
    }

    private void OnDisable()
    {
        if (receiver != null)
        {
            receiver.FrameUpdated -= HandleFrameUpdated;
        }

        if (previewRoot != null)
        {
            previewRoot.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (generatedCanvas != null)
        {
            Destroy(generatedCanvas);
            generatedCanvas = null;
        }
    }

    public void SetReceiver(PoseCameraPreviewReceiver previewReceiver)
    {
        if (receiver == previewReceiver)
        {
            return;
        }

        if (isActiveAndEnabled && receiver != null)
        {
            receiver.FrameUpdated -= HandleFrameUpdated;
        }

        receiver = previewReceiver;

        if (isActiveAndEnabled && receiver != null)
        {
            receiver.FrameUpdated += HandleFrameUpdated;
        }
    }

    private void HandleFrameUpdated(Texture2D texture)
    {
        EnsurePreviewUI();

        if (previewImage != null)
        {
            previewImage.texture = texture;
        }
    }

    private void EnsurePreviewUI()
    {
        if (generatedCanvas != null)
        {
            return;
        }

        generatedCanvas = new GameObject(
            "PoseCameraPreviewCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        generatedCanvas.transform.SetParent(transform, false);

        Canvas canvas = generatedCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 80;

        CanvasScaler scaler = generatedCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        previewRoot = new GameObject(
            "PoseCameraPreview",
            typeof(RectTransform),
            typeof(Image));
        previewRoot.transform.SetParent(generatedCanvas.transform, false);

        RectTransform panelRect = previewRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.one;
        panelRect.anchorMax = Vector2.one;
        panelRect.pivot = Vector2.one;
        panelRect.anchoredPosition = new Vector2(-18f, -18f);
        panelRect.sizeDelta = new Vector2(336f, 256f);

        Image background = previewRoot.GetComponent<Image>();
        background.color = new Color(0.02f, 0.02f, 0.025f, 0.82f);
        background.raycastTarget = false;

        GameObject imageObject = new GameObject(
            "CameraImage",
            typeof(RectTransform),
            typeof(RawImage),
            typeof(AspectRatioFitter));
        imageObject.transform.SetParent(previewRoot.transform, false);

        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = new Vector2(8f, 8f);
        imageRect.offsetMax = new Vector2(-8f, -8f);

        previewImage = imageObject.GetComponent<RawImage>();
        previewImage.color = Color.white;
        previewImage.raycastTarget = false;

        AspectRatioFitter aspectRatio = imageObject.GetComponent<AspectRatioFitter>();
        aspectRatio.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        aspectRatio.aspectRatio = PreviewAspectRatio;

        previewRoot.SetActive(false);
    }

    private void ResolveReceiver()
    {
        if (receiver == null)
        {
            receiver = GetComponent<PoseCameraPreviewReceiver>();
        }
    }

    private bool IsShiftHeld()
    {
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }
}
