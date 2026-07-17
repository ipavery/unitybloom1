using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System;
using System.Linq;

[Serializable]
public class LerpLines
{
    public GameObject firstClip;      
    public GameObject secondClip; 
    public GameObject lerpLine;
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
    public GameObject cancelButtonObject;
    public ParticleManager particleManager;
    public RectTransform canvasrt;

    //scrolling variables
    public float scrollSpeed;
    private RawImage rawImage;
    private Rect uvRect;
    public Vector2 scrollBounds;  //scroller will start at y and decrease until x

    void OnEnable()
    {
        EventHub.Subscribe<ClipLerpClick>(OnClipLerpClick);
        EventHub.Subscribe<DeleteSpline>(OnSplineChange);
        EventHub.Subscribe<ReloadLerpUI>(OnReloadLerpUI);
    }

    void OnDisable()
    {
        EventHub.Unsubscribe<ClipLerpClick>(OnClipLerpClick);
        EventHub.Unsubscribe<DeleteSpline>(OnSplineChange);
        EventHub.Unsubscribe<ReloadLerpUI>(OnReloadLerpUI);
    }

    void OnReloadLerpUI(ReloadLerpUI e)
    {
        // foreach (var splineParticle in particleManager.splineParticleGroup)
        // {
        //     if (splineParticle.spline.lerpSpline != null && splineParticle.spline.lerpSpline != splineParticle.spline)
        //     {
        //         lerpLines.Add(new LerpLines({firstClip = splineParticle.spline}))
        //     }
        // }
        Debug.Log("maybe change this later");
    }

    void OnSplineChange(DeleteSpline e)
    {
        var oldLerpLines = new List<LerpLines>(lerpLines);
        foreach (var lerpLine in oldLerpLines)
        {
            if (lerpLine.firstClip.GetComponent<DraggableClip>().attachedSpline == e.spline || lerpLine.secondClip.GetComponent<DraggableClip>().attachedSpline == e.spline)
            {
                lerpLines.Remove(lerpLine);
                Destroy(lerpLine.lerpLine);
            }

        }
    }

    void OnClipLerpClick(ClipLerpClick e)
    {
        EventHub.Publish(new SplineSelectionChange(true, e.clipObj.GetComponent<DraggableClip>().attachedSpline));
        if (selectingLerp == false)
        {
            selectingLerp = true;
            var newLerpLine = Instantiate(lerpLinePrefab, gameObject.GetComponent<RectTransform>());
            rawImage = newLerpLine.GetComponent<RawImage>();
            rawImage.color = lerpLineColor;
            uvRect = rawImage.uvRect;
            uvRect.x = scrollBounds.y;
            newLerpGroup = new LerpLines { firstClip = e.clipObj, lerpLine = newLerpLine, nowSelecting = true };
            lerpLines.Add(newLerpGroup);
            cancelButtonObject.SetActive(true);
        }
        else
        {
            //check if selected clip is different to original
            if (newLerpGroup != null && newLerpGroup.firstClip != e.clipObj)
            {
                foreach (var lerpGroup in lerpLines)
                {
                    if (lerpGroup.firstClip == newLerpGroup.firstClip && lerpGroup.secondClip == e.clipObj)
                    {
                        lerpLines.Remove(lerpGroup);
                        Destroy(lerpGroup.lerpLine);
                        OnCancel();
                        break;
                    }
                    else if (lerpGroup.firstClip == newLerpGroup.firstClip && lerpGroup.secondClip != null)
                    {
                        lerpLines.Remove(lerpGroup);
                        Destroy(lerpGroup.lerpLine);
                        break;
                    }
                }
                if (newLerpGroup.firstClip.GetComponent<DraggableClip>().attachedSpline.points.Length > e.clipObj.GetComponent<DraggableClip>().attachedSpline.points.Length)
                {
                    Debug.Log("connect to smaller spline not allowed");
                    OnCancel();
                }
                //newLerpGroup.firstClip.GetComponent<DraggableClip>().attachedSpline.lerpSpline = e.clipObj.GetComponent<DraggableClip>().attachedSpline;

                    newLerpGroup.nowSelecting = false;
                newLerpGroup.secondClip = e.clipObj;
            }
            else if (newLerpGroup != null && newLerpGroup.firstClip == e.clipObj)
            {
                Debug.Log("same button clicked");
                OnCancel();
            }
            selectingLerp = false;
            cancelButtonObject.SetActive(false);
        }
        if (e.reset == true)
        {
            UpdateLerpSplines();
        }
    }

    void OnCancel()
    {
        selectingLerp = false;

        lerpLines.Remove(newLerpGroup);
        Destroy(newLerpGroup.lerpLine);
        cancelButtonObject.SetActive(false);
    }

    void UpdateLerpSplines()
    {
        foreach (var splineParticle in particleManager.splineParticleGroup)
        {
            splineParticle.spline.lerpSpline = splineParticle.spline;
        }
        foreach (var lerpGroup in lerpLines)
        {
            if (lerpGroup.firstClip != null && lerpGroup.secondClip != null)
            {
                lerpGroup.firstClip.GetComponent<DraggableClip>().attachedSpline.lerpSpline = lerpGroup.secondClip.GetComponent<DraggableClip>().attachedSpline;
            }
        }
        EventHub.Publish(new ReloadParticles(true));
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        lerpLines = new();
        cancelButtonObject.SetActive(false);
        cancelButtonObject.GetComponent<Button>().onClick.AddListener(OnCancel);
    }

    // Update is called once per frame
    void Update()
    {
        foreach (var lerpGroup in lerpLines)
        {
            if (lerpGroup.nowSelecting == false)
            {
                var target1 = lerpGroup.firstClip.transform.Find("LerpButton").position/ (Vector2)canvasrt.localScale;
                var target2 = lerpGroup.secondClip.transform.Find("LerpButton").position/ (Vector2)canvasrt.localScale;

                MakeLine(target1, target2, lerpGroup);

                ScrollRect(lerpGroup);
            }
            else
            {
                var target1 = lerpGroup.firstClip.transform.Find("LerpButton").position / (Vector2)canvasrt.localScale;
                var target2 = Mouse.current.position.ReadValue() / canvasrt.localScale;

                MakeLine(target1, target2, lerpGroup);

                ScrollRect(lerpGroup);
            }

        }
    }

    void ScrollRect(LerpLines lerpGroup)
    {
        rawImage = lerpGroup.lerpLine.GetComponent<RawImage>();
        uvRect = rawImage.uvRect;
        uvRect.x -= scrollSpeed * Time.deltaTime;
        if (uvRect.x < scrollBounds.x)
            uvRect.x += scrollBounds.y;  // Loop back smoothly
        rawImage.uvRect = uvRect;
    }

    void MakeLine(Vector2 target1, Vector2 target2, LerpLines lerpGroup)
    {
        Vector2 dir = target2 - target1;
        var dist = dir.magnitude;
        float angleTo = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        var lerpLineT = lerpGroup.lerpLine.transform;
        lerpLineT.localPosition = target1;
        lerpLineT.localScale = new Vector3(dist, lerpLineT.localScale.y, lerpLineT.localScale.z);
        lerpLineT.rotation = Quaternion.Euler(0f, 0f, angleTo);
    }
}