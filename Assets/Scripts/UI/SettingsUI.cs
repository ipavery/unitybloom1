using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class SettingsUI : MonoBehaviour
{
    public Button settingsButton;
    public RectTransform settingsPanel;

    void Start()
    {
        settingsPanel.gameObject.SetActive(false);
        settingsButton.onClick.AddListener(OnSettingsClicked);
    }

    void OnSettingsClicked()
    {
        if(settingsPanel.gameObject.activeSelf)
        {
            settingsPanel.gameObject.SetActive(false);
        }
        else
        {
            settingsPanel.gameObject.SetActive(true);
        }
    }
}