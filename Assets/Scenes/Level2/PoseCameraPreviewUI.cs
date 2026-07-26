using StarterAssets;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-800)]
[DisallowMultipleComponent]
public sealed class PoseCameraPreviewUI : MonoBehaviour
{
    private const KeyCode VisibilityToggleKey = KeyCode.V;
    private const KeyCode WindowLockToggleKey = KeyCode.K;
    private const KeyCode RestartLevelKey = KeyCode.R;
    private const float MissingFrameTimeoutSeconds = 1.5f;
    private const float DefaultPanelWidth = 336f;
    private const float MinimumPanelWidth = 240f;
    private const float MaximumScreenFraction = 0.6f;
    private const float PreviewPadding = 8f;
    private const float DefaultAspectRatio = 4f / 3f;
    private const string LayoutSavedKey = "Lumina.PosePreview.LayoutSaved";
    private const string LayoutPositionXKey = "Lumina.PosePreview.PositionX";
    private const string LayoutPositionYKey = "Lumina.PosePreview.PositionY";
    private const string LayoutWidthKey = "Lumina.PosePreview.Width";

    [SerializeField] private PoseCameraPreviewReceiver receiver;

    private GameObject generatedCanvas;
    private Canvas previewCanvas;
    private GameObject previewRoot;
    private RectTransform previewPanelRect;
    private Image previewBackground;
    private RawImage previewImage;
    private AspectRatioFitter previewAspectRatio;
    private GameObject resizeHandle;
    private Image resizeHandleImage;
    private StarterAssetsInputs playerInputs;

    private bool userVisible = true;
    private bool windowLocked = true;
    private bool resizeInteraction;
    private bool pointerPositionValid;
    private Vector2 previousPointerLocalPosition;
    private Vector2 resizeStartPointerLocalPosition;
    private float resizeStartWidth;
    private float currentAspectRatio = DefaultAspectRatio;

    private bool cursorStateCaptured;
    private bool previousPlayerCursorInputForLook;

    public bool IsWindowLocked => windowLocked;

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
        KeepCursorVisible();

        if (IsShiftHeld() && Input.GetKeyDown(VisibilityToggleKey))
        {
            userVisible = !userVisible;
        }

        if (IsShiftHeld() && Input.GetKeyDown(WindowLockToggleKey))
        {
            SetWindowLocked(!windowLocked);
        }

        if (IsShiftHeld() && Input.GetKeyDown(RestartLevelKey))
        {
            RestartCurrentLevel();
            return;
        }

        if (!windowLocked)
        {
            KeepCursorAvailableForWindowEditing();
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

        if (!windowLocked)
        {
            SetWindowLocked(true);
        }

        if (previewRoot != null)
        {
            previewRoot.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        RestoreCursorState();

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

    public void BeginPointerInteraction(
        Vector2 screenPosition,
        bool resizeWindow)
    {
        if (windowLocked ||
            previewPanelRect == null ||
            !TryGetPointerLocalPosition(screenPosition, out Vector2 localPosition))
        {
            return;
        }

        resizeInteraction = resizeWindow;
        pointerPositionValid = true;
        previousPointerLocalPosition = localPosition;
        resizeStartPointerLocalPosition = localPosition;
        resizeStartWidth = previewPanelRect.sizeDelta.x;
    }

    public void ContinuePointerInteraction(Vector2 screenPosition)
    {
        if (windowLocked ||
            !pointerPositionValid ||
            previewPanelRect == null ||
            !TryGetPointerLocalPosition(screenPosition, out Vector2 localPosition))
        {
            return;
        }

        if (resizeInteraction)
        {
            float horizontalDelta =
                localPosition.x - resizeStartPointerLocalPosition.x;
            SetPanelWidth(resizeStartWidth + horizontalDelta);
        }
        else
        {
            Vector2 pointerDelta =
                localPosition - previousPointerLocalPosition;
            previewPanelRect.anchoredPosition += pointerDelta;
            ClampWindowToCanvas();
        }

        previousPointerLocalPosition = localPosition;
    }

    public void EndPointerInteraction()
    {
        if (!pointerPositionValid)
        {
            return;
        }

        pointerPositionValid = false;
        SaveLayout();
    }

    private void HandleFrameUpdated(Texture2D texture)
    {
        EnsurePreviewUI();

        if (previewImage == null)
        {
            return;
        }

        previewImage.texture = texture;
        UpdatePreviewAspectRatio(texture);
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
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        generatedCanvas.transform.SetParent(transform, false);

        previewCanvas = generatedCanvas.GetComponent<Canvas>();
        previewCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        previewCanvas.overrideSorting = true;
        previewCanvas.sortingOrder = 80;

        CanvasScaler scaler = generatedCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        previewRoot = new GameObject(
            "PoseCameraPreview",
            typeof(RectTransform),
            typeof(Image),
            typeof(PoseCameraPreviewPointerHandler));
        previewRoot.transform.SetParent(generatedCanvas.transform, false);

        previewPanelRect = previewRoot.GetComponent<RectTransform>();
        previewPanelRect.anchorMin = Vector2.one;
        previewPanelRect.anchorMax = Vector2.one;
        previewPanelRect.pivot = Vector2.one;
        previewPanelRect.anchoredPosition = new Vector2(-18f, -18f);
        previewPanelRect.sizeDelta = new Vector2(
            DefaultPanelWidth,
            CalculatePanelHeight(DefaultPanelWidth));

        previewBackground = previewRoot.GetComponent<Image>();
        previewBackground.color = new Color(0.02f, 0.02f, 0.025f, 0.82f);

        PoseCameraPreviewPointerHandler dragHandler =
            previewRoot.GetComponent<PoseCameraPreviewPointerHandler>();
        dragHandler.Initialize(this, false);

        GameObject imageObject = new GameObject(
            "CameraImage",
            typeof(RectTransform),
            typeof(RawImage),
            typeof(AspectRatioFitter));
        imageObject.transform.SetParent(previewRoot.transform, false);

        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = new Vector2(PreviewPadding, PreviewPadding);
        imageRect.offsetMax = new Vector2(-PreviewPadding, -PreviewPadding);

        previewImage = imageObject.GetComponent<RawImage>();
        previewImage.color = Color.white;
        previewImage.raycastTarget = false;

        previewAspectRatio = imageObject.GetComponent<AspectRatioFitter>();
        previewAspectRatio.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        previewAspectRatio.aspectRatio = currentAspectRatio;

        CreateResizeHandle();
        LoadLayout();
        ApplyWindowLockVisualState();
        previewRoot.SetActive(false);
    }

    private void CreateResizeHandle()
    {
        resizeHandle = new GameObject(
            "ResizeHandle",
            typeof(RectTransform),
            typeof(Image),
            typeof(PoseCameraPreviewPointerHandler));
        resizeHandle.transform.SetParent(previewRoot.transform, false);

        RectTransform handleRect = resizeHandle.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(1f, 0f);
        handleRect.anchorMax = new Vector2(1f, 0f);
        handleRect.pivot = new Vector2(1f, 0f);
        handleRect.anchoredPosition = Vector2.zero;
        handleRect.sizeDelta = new Vector2(26f, 26f);

        resizeHandleImage = resizeHandle.GetComponent<Image>();
        resizeHandleImage.color = new Color(1f, 0.72f, 0.12f, 0.9f);

        PoseCameraPreviewPointerHandler resizeHandler =
            resizeHandle.GetComponent<PoseCameraPreviewPointerHandler>();
        resizeHandler.Initialize(this, true);
    }

    private void UpdatePreviewAspectRatio(Texture2D texture)
    {
        if (texture == null ||
            texture.width <= 0 ||
            texture.height <= 0)
        {
            return;
        }

        currentAspectRatio = (float)texture.width / texture.height;

        if (previewAspectRatio != null)
        {
            previewAspectRatio.aspectRatio = currentAspectRatio;
        }

        if (previewPanelRect != null)
        {
            SetPanelWidth(previewPanelRect.sizeDelta.x);
        }
    }

    private void SetPanelWidth(float requestedWidth)
    {
        if (previewPanelRect == null)
        {
            return;
        }

        float minimumWidth = MinimumPanelWidth;
        float maximumWidth = GetMaximumPanelWidth();
        float width = Mathf.Clamp(
            requestedWidth,
            Mathf.Min(minimumWidth, maximumWidth),
            maximumWidth);

        previewPanelRect.sizeDelta = new Vector2(
            width,
            CalculatePanelHeight(width));
        ClampWindowToCanvas();
    }

    private float CalculatePanelHeight(float panelWidth)
    {
        float imageWidth = Mathf.Max(1f, panelWidth - PreviewPadding * 2f);
        float imageHeight =
            imageWidth / Mathf.Max(0.01f, currentAspectRatio);
        return imageHeight + PreviewPadding * 2f;
    }

    private float GetMaximumPanelWidth()
    {
        RectTransform canvasRect = GetCanvasRect();
        if (canvasRect == null)
        {
            return DefaultPanelWidth;
        }

        float maximumWidthByScreen =
            canvasRect.rect.width * MaximumScreenFraction;
        float maximumHeight =
            canvasRect.rect.height * MaximumScreenFraction;
        float maximumImageHeight =
            Mathf.Max(1f, maximumHeight - PreviewPadding * 2f);
        float maximumWidthByHeight =
            maximumImageHeight * currentAspectRatio +
            PreviewPadding * 2f;

        return Mathf.Max(
            1f,
            Mathf.Min(maximumWidthByScreen, maximumWidthByHeight));
    }

    private void ClampWindowToCanvas()
    {
        RectTransform canvasRect = GetCanvasRect();
        if (canvasRect == null || previewPanelRect == null)
        {
            return;
        }

        Vector2 position = previewPanelRect.anchoredPosition;
        float panelWidth = previewPanelRect.sizeDelta.x;
        float panelHeight = previewPanelRect.sizeDelta.y;
        float canvasWidth = canvasRect.rect.width;
        float canvasHeight = canvasRect.rect.height;

        position.x = Mathf.Clamp(
            position.x,
            panelWidth - canvasWidth,
            0f);
        position.y = Mathf.Clamp(
            position.y,
            panelHeight - canvasHeight,
            0f);
        previewPanelRect.anchoredPosition = position;
    }

    private bool TryGetPointerLocalPosition(
        Vector2 screenPosition,
        out Vector2 localPosition)
    {
        localPosition = Vector2.zero;
        RectTransform canvasRect = GetCanvasRect();
        return canvasRect != null &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPosition,
                null,
                out localPosition);
    }

    private RectTransform GetCanvasRect()
    {
        return previewCanvas != null
            ? previewCanvas.transform as RectTransform
            : null;
    }

    private void SetWindowLocked(bool locked)
    {
        if (windowLocked == locked)
        {
            return;
        }

        windowLocked = locked;
        pointerPositionValid = false;
        ApplyWindowLockVisualState();

        if (windowLocked)
        {
            SaveLayout();
            RestoreCursorState();
        }
        else
        {
            CaptureCursorState();
            KeepCursorAvailableForWindowEditing();
        }
    }

    private void ApplyWindowLockVisualState()
    {
        bool interactionEnabled = !windowLocked;

        if (previewBackground != null)
        {
            previewBackground.raycastTarget = interactionEnabled;
        }

        if (resizeHandleImage != null)
        {
            resizeHandleImage.raycastTarget = interactionEnabled;
        }

        if (resizeHandle != null)
        {
            resizeHandle.SetActive(interactionEnabled);
        }
    }

    private void CaptureCursorState()
    {
        if (cursorStateCaptured)
        {
            return;
        }

        ResolvePlayerInputs();

        if (playerInputs != null)
        {
            previousPlayerCursorInputForLook =
                playerInputs.cursorInputForLook;
        }

        cursorStateCaptured = true;
    }

    private void KeepCursorAvailableForWindowEditing()
    {
        ResolvePlayerInputs();

        if (playerInputs != null)
        {
            playerInputs.cursorLocked = false;
            playerInputs.cursorInputForLook = false;
            playerInputs.LookInput(Vector2.zero);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void RestoreCursorState()
    {
        if (!cursorStateCaptured)
        {
            KeepCursorVisible();
            return;
        }

        if (playerInputs != null)
        {
            playerInputs.cursorLocked = false;
            playerInputs.cursorInputForLook =
                previousPlayerCursorInputForLook;
            playerInputs.LookInput(Vector2.zero);
        }

        cursorStateCaptured = false;
        KeepCursorVisible();
    }

    private void KeepCursorVisible()
    {
        ResolvePlayerInputs();

        if (playerInputs != null)
        {
            playerInputs.cursorLocked = false;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ResolvePlayerInputs()
    {
        if (playerInputs == null)
        {
            playerInputs = FindObjectOfType<StarterAssetsInputs>();
        }
    }

    private void ResolveReceiver()
    {
        if (receiver == null)
        {
            receiver = GetComponent<PoseCameraPreviewReceiver>();
        }
    }

    private void LoadLayout()
    {
        if (previewPanelRect == null ||
            PlayerPrefs.GetInt(LayoutSavedKey, 0) == 0)
        {
            return;
        }

        previewPanelRect.anchoredPosition = new Vector2(
            PlayerPrefs.GetFloat(
                LayoutPositionXKey,
                previewPanelRect.anchoredPosition.x),
            PlayerPrefs.GetFloat(
                LayoutPositionYKey,
                previewPanelRect.anchoredPosition.y));
        SetPanelWidth(PlayerPrefs.GetFloat(
            LayoutWidthKey,
            DefaultPanelWidth));
        ClampWindowToCanvas();
    }

    private void SaveLayout()
    {
        if (previewPanelRect == null)
        {
            return;
        }

        PlayerPrefs.SetInt(LayoutSavedKey, 1);
        PlayerPrefs.SetFloat(
            LayoutPositionXKey,
            previewPanelRect.anchoredPosition.x);
        PlayerPrefs.SetFloat(
            LayoutPositionYKey,
            previewPanelRect.anchoredPosition.y);
        PlayerPrefs.SetFloat(
            LayoutWidthKey,
            previewPanelRect.sizeDelta.x);
        PlayerPrefs.Save();
    }

    private void RestartCurrentLevel()
    {
        SaveLayout();

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.buildIndex < 0)
        {
            /* Debug.LogWarning(
                "[PoseCameraPreview] 当前场景不在 Build Settings 中，无法使用 Shift+R 重开。",
                this); */
            return;
        }

        SceneManager.LoadScene(activeScene.buildIndex);
    }

    private bool IsShiftHeld()
    {
        return Input.GetKey(KeyCode.LeftShift) ||
            Input.GetKey(KeyCode.RightShift);
    }
}
