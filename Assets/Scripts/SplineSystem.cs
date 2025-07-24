using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SplineSystem : MonoBehaviour
{
    public BezierSpline[] splineList;
    public Transform SplineSystemTransform;
    
    void Awake() {
        splineList = new BezierSpline[SplineSystemTransform.transform.childCount];

        for (int i = 0; i < SplineSystemTransform.transform.childCount; i++)
        {
            // splineList[i] = SplineSystemTransform.GetChild(i);
            // Debug.Log(SplineSystemTransform.GetChild(i).gameObject.GetType());
        }
		
    }
}
