using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public static class InputBlocker
{
    /// <summary>
    /// Checks if input is blocked by any UI, excluding elements on a specific layer.
    /// This means if the user is hovering over any UI element that is not on the excluded layer, input will be considered blocked.
    /// <param name="excludedUILayer">Optional: the layer you want to ignore (e.g. "IgnoreUIBlock")</param>
    /// </summary>
    /// 
    public static bool inputBlockOverride = false;
    public static bool IsInputBlocked(string excludedUILayer = null)
    {
        if (inputBlockOverride == true)
            return true;
        if (EventSystem.current == null)
            return false;

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        List<RaycastResult> raycastResults = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, raycastResults);

        foreach (var result in raycastResults)
        {
            var layerName = LayerMask.LayerToName(result.gameObject.layer);
            if (excludedUILayer != null && layerName == excludedUILayer)
                continue;

            return true; // A UI element *not* on the excluded layer is under the pointer
        }

        return false; // Only excluded layer elements were hit, or nothing
    }
}