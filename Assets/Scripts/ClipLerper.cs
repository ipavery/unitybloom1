using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ClipLerper : MonoBehaviour
{
    public LineRenderer lr;
    private bool selectingLerp = false;
    private GameObject firstSelectedClip;
    Camera cam;

    void OnEnable()
    {
        EventHub.Subscribe<ClipLerpClick>(OnClipLerpClick);
    }

    void OnDisable()
    {
        EventHub.Unsubscribe<ClipLerpClick>(OnClipLerpClick);
    }

    void OnClipLerpClick(ClipLerpClick e)
    {
        if (selectingLerp == false)
        {
            selectingLerp = true;
            lr.enabled = true;
            firstSelectedClip = e.clipObj;
        }
        else
        {
            //check if selected clip is different to original
            if (firstSelectedClip != e.clipObj)
            {
                firstSelectedClip.GetComponent<DraggableClip>().attachedSpline.lerpSpline = e.clipObj.GetComponent<DraggableClip>().attachedSpline;
            }
            selectingLerp = false;
            firstSelectedClip = null;
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        lr.enabled = false;
        cam = Camera.main;
        lr.positionCount = 2;
        lr.startWidth = lr.endWidth = 0.01f; // world units
    }

    // Update is called once per frame
    void Update()
    {
        if (selectingLerp)
        {
            Vector3 wa = cam.ScreenToWorldPoint(
            RectTransformUtility.WorldToScreenPoint(cam, firstSelectedClip.transform.Find("LerpButton").GetComponent<RectTransform>().position)
        );
            Vector3 wb = cam.ScreenToWorldPoint(
                Mouse.current.position.ReadValue()
            );
            wa.z = wb.z = 0f; // ensure both lie on the same plane

            lr.SetPosition(0, wa);
            lr.SetPosition(1, wb);
            Debug.Log(lr.positionCount);
        }
    }
}
