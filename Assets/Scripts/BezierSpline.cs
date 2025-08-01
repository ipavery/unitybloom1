using UnityEngine;
using System;

public class BezierSpline : MonoBehaviour
{

    public Vector3[] points;
    public float s_life; //Lifetime of the particle in seconds
    public int splineSymmetry;
    public int frequency;
    public float lifetimeOffset;
    public float lerpLifetimeOffset;
    public int lerpTimes;
    public Color lerpColor1;
    public Color lerpColor2;

    public GameObject splinePrefab;

    public BezierSpline lerpSpline;

    public int CurveCount
    {
        get
        {
            return (points.Length - 1) / 3;
        }
    }

    public Vector3 GetPoint(float t)
    {
        if (points.Length > 0)
        {
            int i;
            if (t >= 1f)
            {
                t = 1f;
                i = points.Length - 4;
            }
            else
            {
                t = Mathf.Clamp01(t) * CurveCount;
                i = (int)t;
                t -= i;
                i *= 3;
            }
            return transform.TransformPoint(Bezier.GetPoint(
                points[i], points[i + 1], points[i + 2], points[i + 3], t));
        }
        else
        {
            return transform.TransformPoint(Vector3.zero);
        }

    }

    public Vector3 GetLerpPoint(float t, float l)
    {
        if (lerpSpline == null)
        {
            return GetPoint(t);
        }
        int i;
        if (t >= 1f)
        {
            t = 1f;
            i = points.Length - 4;
        }
        else
        {
            t = Mathf.Clamp01(t) * CurveCount;
            i = (int)t;
            t -= i;
            i *= 3;
        }

        Vector3 p0 = Vector3.Lerp(transform.position+points[i], lerpSpline.points[i]+lerpSpline.transform.position, l);
        Vector3 p1 = Vector3.Lerp(transform.position+points[i + 1], lerpSpline.points[i+1]+lerpSpline.transform.position, l);
        Vector3 p2 = Vector3.Lerp(transform.position+points[i + 2], lerpSpline.points[i+2]+lerpSpline.transform.position, l);
        Vector3 p3 = Vector3.Lerp(transform.position+points[i + 3], lerpSpline.points[i+3]+lerpSpline.transform.position, l);
        return Bezier.GetPoint(
            p0, p1, p2, p3, t);
    }

    public Vector3 GetVelocity(float t)
    {
        if (points.Length > 0)
        {
            int i;
            if (t >= 1f)
            {
                t = 1f;
                i = points.Length - 4;
            }
            else
            {
                t = Mathf.Clamp01(t) * CurveCount;
                i = (int)t;
                t -= i;
                i *= 3;
            }
            return transform.TransformPoint(Bezier.GetFirstDerivative(
                points[i], points[i + 1], points[i + 2], points[i + 3], t)) - transform.position;
        }
        else
        {
            return transform.TransformPoint(Vector3.zero);
        }

    }

    public Vector3 GetDirection(float t)
    {
        return GetVelocity(t).normalized;
    }

    public void AddCurve()
    {
        Vector3 point = points[points.Length - 1];
        Array.Resize(ref points, points.Length + 3);
        point.x += 1f;
        points[points.Length - 3] = point;
        point.x += 1f;
        points[points.Length - 2] = point;
        point.x += 1f;
        points[points.Length - 1] = point;
    }

    public void RemoveCurve()
    {
        Array.Resize(ref points, points.Length - 3);
    }

    public void AddSpline(int index)
    {
        var newSpline = Instantiate(splinePrefab, Vector3.zero, Quaternion.identity);
        newSpline.name = "Spline " + index;
    }

    public void Reset()
    {
        points = new Vector3[] {
            new Vector3(1f, 0f, 0f),
            new Vector3(2f, 0f, 0f),
            new Vector3(3f, 0f, 0f),
            new Vector3(4f, 0f, 0f)
        };
    }
}
