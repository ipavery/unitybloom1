using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class EditorToolsUIMisc : MonoBehaviour
{
    public Button resetViewButton;
    Transform mainCam;
    Transform playerBody;
    public Transform defaultCamPos;
    [SerializeField] private MobileUILayout mobileUILayout;
    public RectTransform rootRectTransform;

    void OnEnable()
    {
        if (mobileUILayout != null)
        {
            mobileUILayout.onMobileLayout.AddListener(OnMobileLayout);
            mobileUILayout.onDesktopLayout.AddListener(OnDesktopLayout);
        }
    }

    void OnDisable()
    {
        if (mobileUILayout != null)
        {
            mobileUILayout.onMobileLayout.RemoveListener(OnMobileLayout);
            mobileUILayout.onDesktopLayout.RemoveListener(OnDesktopLayout);
        }
    }

    void OnMobileLayout()
    {
        // Adjust the root RectTransform for mobile layout
        float scaleFactor = 1.5f; // Example scale factor for mobile
        rootRectTransform.localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor); // Example scale for mobile
    }

    void OnDesktopLayout()
    {
        // Reset the root RectTransform for desktop layout
        rootRectTransform.localScale = Vector3.one; // Reset to original scale for desktop
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        resetViewButton.onClick.AddListener(OnResetViewClicked);
        mainCam = Camera.main.transform;
        playerBody = mainCam.parent;
    }

    void OnResetViewClicked()
    {
        // Snap the parent object to the saved location
        playerBody.SetPositionAndRotation(defaultCamPos.position, defaultCamPos.rotation);

        // Restore vertical look (pitch) locally to the camera
        mainCam.localRotation = Quaternion.Euler(0f, 0f, 0f);
    }
}
