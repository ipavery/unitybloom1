using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

[RequireComponent(typeof(RectTransform))]
public class MobileUILayout : MonoBehaviour
{
    [System.Serializable]
    public class LayoutSettings
    {
        [Tooltip("Standard anchored position (X, Y offset)")]
        public Vector2 anchoredPosition;
        
        [Tooltip("Controls Width/Height or stretch margins")]
        public Vector2 sizeDelta;
        
        [Tooltip("The lower-left anchor point (0 to 1)")]
        public Vector2 anchorMin;
        
        [Tooltip("The upper-right anchor point (0 to 1)")]
        public Vector2 anchorMax;
        
        [Tooltip("The pivot point (0 to 1)")]
        public Vector2 pivot;
    }

    public enum OverrideType
    {
        LayoutElementHeight,
        RectTransformSizeDelta
    }

    [System.Serializable]
    public class ChildLayoutOverride
    {
        [Tooltip("Drag the child GameObject or Component here.")]
        public GameObject targetObject;

        [Tooltip("Choose whether to modify the LayoutElement height or the RectTransform size delta.")]
        public OverrideType overrideType;

        [Header("Preferred Height Settings")]
        public float desktopPreferredHeight;
        public float mobilePreferredHeight;

        [Header("Size Delta Settings")]
        public Vector2 desktopSizeDelta;
        public Vector2 mobileSizeDelta;
    }

    [Header("Main Panel Layouts")]
    public LayoutSettings desktopLayout;
    public LayoutSettings mobileLayout;

    [Header("Child Element Overrides")]
    [Tooltip("Add child elements here to adjust their sizes/heights automatically.")]
    public ChildLayoutOverride[] childOverrides;

    [Header("Testing")]
    [Tooltip("Check this to force the mobile layout while testing in the Unity Editor.")]
    public bool forceMobileInEditor = false;

    [Header("Master Switch Events")]
    public UnityEvent onDesktopLayout;
    public UnityEvent onMobileLayout;

    public SplinePickerData SPD;

    private RectTransform rectTransform;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();

        bool isMobile = Application.isMobilePlatform;

#if UNITY_EDITOR
        if (forceMobileInEditor)
        {
            isMobile = true;
        }
#endif

        if (isMobile)
        {
            ApplyLayout(mobileLayout);
            ApplyChildOverrides(true);
            onMobileLayout?.Invoke();
            SPD.controlPointScale = 5;
        }
        else
        {
            ApplyLayout(desktopLayout);
            ApplyChildOverrides(false);
            onDesktopLayout?.Invoke();
        }
    }

    private void ApplyLayout(LayoutSettings layout)
    {
        rectTransform.anchorMin = layout.anchorMin;
        rectTransform.anchorMax = layout.anchorMax;
        rectTransform.pivot = layout.pivot;
        rectTransform.sizeDelta = layout.sizeDelta;
        rectTransform.anchoredPosition = layout.anchoredPosition;
    }

    private void ApplyChildOverrides(bool isMobile)
    {
        if (childOverrides == null) return;

        foreach (var item in childOverrides)
        {
            if (item.targetObject == null) continue;

            if (item.overrideType == OverrideType.LayoutElementHeight)
            {
                var layoutElement = item.targetObject.GetComponent<LayoutElement>();
                if (layoutElement != null)
                {
                    layoutElement.preferredHeight = isMobile ? item.mobilePreferredHeight : item.desktopPreferredHeight;
                }
                else
                {
                    Debug.LogWarning($"MobileUILayout: Target '{item.targetObject.name}' is set to Layout Element Height, but has no LayoutElement component!", this);
                }
            }
            else if (item.overrideType == OverrideType.RectTransformSizeDelta)
            {
                var childRect = item.targetObject.GetComponent<RectTransform>();
                if (childRect != null)
                {
                    childRect.sizeDelta = isMobile ? item.mobileSizeDelta : item.desktopSizeDelta;
                }
                else
                {
                    Debug.LogWarning($"MobileUILayout: Target '{item.targetObject.name}' is set to RectTransform Size Delta, but has no RectTransform!", this);
                }
            }
        }
    }

    [ContextMenu("Copy Current RectTransform to Desktop")]
    public void CopyToDesktop()
    {
        rectTransform = GetComponent<RectTransform>();
        desktopLayout.anchoredPosition = rectTransform.anchoredPosition;
        desktopLayout.sizeDelta = rectTransform.sizeDelta;
        desktopLayout.anchorMin = rectTransform.anchorMin;
        desktopLayout.anchorMax = rectTransform.anchorMax;
        desktopLayout.pivot = rectTransform.pivot;
    }
        [ContextMenu("Copy Current RectTransform to Mobile")]
    public void CopyToMobile()
    {
        rectTransform = GetComponent<RectTransform>();
        mobileLayout.anchoredPosition = rectTransform.anchoredPosition;
        mobileLayout.sizeDelta = rectTransform.sizeDelta;
        mobileLayout.anchorMin = rectTransform.anchorMin;
        mobileLayout.anchorMax = rectTransform.anchorMax;
        mobileLayout.pivot = rectTransform.pivot;
    }
}