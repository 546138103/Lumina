using UnityEngine;
using UnityEngine.EventSystems;

public sealed class PoseCameraPreviewPointerHandler :
    MonoBehaviour,
    IPointerDownHandler,
    IDragHandler,
    IEndDragHandler,
    IPointerUpHandler
{
    private PoseCameraPreviewUI owner;
    private bool resizeWindow;

    public void Initialize(
        PoseCameraPreviewUI previewOwner,
        bool isResizeHandle)
    {
        owner = previewOwner;
        resizeWindow = isResizeHandle;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        owner?.BeginPointerInteraction(
            eventData.position,
            resizeWindow);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        owner?.ContinuePointerInteraction(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        owner?.EndPointerInteraction();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        owner?.EndPointerInteraction();
    }
}
