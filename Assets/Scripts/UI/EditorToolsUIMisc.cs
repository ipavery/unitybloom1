using UnityEngine;
using UnityEngine.UI;

public class EditorToolsUIMisc : MonoBehaviour
{
    public Button resetViewButton;
    Transform mainCam;
    Transform playerBody;

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
        playerBody.position = new Vector3(0,0, -30);
        
        // Restore horizontal look (yaw) to the parent
        playerBody.rotation = Quaternion.Euler(0f, 0f, 0f);
        
        // Restore vertical look (pitch) locally to the camera
        mainCam.localRotation = Quaternion.Euler(0f, 0f, 0f);
    }
}
