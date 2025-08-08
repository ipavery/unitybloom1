using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System;

[Serializable]
public class LerpLines
{
    public GameObject firstClip;              // or whatever your “line” type is
    public GameObject secondClip; // list of particles for this line
    public GameObject lerpLine; // Index of the control point in the spline
    public bool nowSelecting;
}

public class ClipLerper : MonoBehaviour
{
    public GameObject lerpLinePrefab;
    public Color lerpLineColor;
    private List<LerpLines> lerpLines;
    private LerpLines newLerpGroup;
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
            var newLerpLine = Instantiate(lerpLinePrefab, gameObject.GetComponent<RectTransform>());
            newLerpLine.GetComponent<Image>().color = lerpLineColor;
            newLerpGroup = new LerpLines { firstClip = e.clipObj, lerpLine = newLerpLine, nowSelecting = true };
            lerpLines.Add(newLerpGroup);

        }
        else
        {
            //check if selected clip is different to original
            if (newLerpGroup != null && newLerpGroup.firstClip != e.clipObj)
            {
                newLerpGroup.firstClip.GetComponent<DraggableClip>().attachedSpline.lerpSpline = e.clipObj.GetComponent<DraggableClip>().attachedSpline;
                newLerpGroup.nowSelecting = false;
                newLerpGroup.secondClip = e.clipObj;
            }
            selectingLerp = false;
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        lerpLines = new();
    }

    // Update is called once per frame
    void Update()
    {
        foreach (var lerpGroup in lerpLines)
        {
            if (lerpGroup.nowSelecting == false)
            {
                var target1 = lerpGroup.firstClip.transform.Find("LerpButton");
                var target2 = lerpGroup.secondClip.transform.Find("LerpButton");
                Vector2 dir = target2.position - target1.position;
                var dist = dir.magnitude;
                float angleTo = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                var lerpLineT = lerpGroup.lerpLine.transform;
                lerpLineT.localPosition = target1.position;
                lerpLineT.localScale = new Vector3(dist, lerpLineT.localScale.y, lerpLineT.localScale.z);
                lerpLineT.rotation = Quaternion.Euler(0f, 0f, angleTo);
            }
            else
            {
                var target1 = lerpGroup.firstClip.transform.Find("LerpButton");
                var target2 = Mouse.current.position.ReadValue();
                Vector2 dir = target2 - (Vector2)target1.position;
                var dist = dir.magnitude;
                float angleTo = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                var lerpLineT = lerpGroup.lerpLine.transform;
                lerpLineT.localPosition = target1.position;
                lerpLineT.localScale = new Vector3(dist, lerpLineT.localScale.y, lerpLineT.localScale.z);
                lerpLineT.rotation = Quaternion.Euler(0f, 0f, angleTo);
            }

        }
    }
}
