using UnityEngine;
using UnityEngine.EventSystems;

public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [TextArea(2, 5)]
    [Tooltip("The text to display when hovering over this UI element.")]
    public string message;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (TooltipManager.Instance != null && !string.IsNullOrEmpty(message))
        {
            // Put it in the queue to wait for the delay
            TooltipManager.Instance.QueueTooltip(message); 
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (TooltipManager.Instance != null)
        {
            // Instantly cancel the timer or hide it if it's already visible
            TooltipManager.Instance.HideTooltip(); 
        }
    }
    
    private void OnDisable()
    {
        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip();
        }
    }
}