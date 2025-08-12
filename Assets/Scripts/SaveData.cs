using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SplineData
{
    public string name;
    public float posX, posY, posZ;
    public List<float> points;
    public float s_life;
    public int splineSymmetry;
    public int frequency;
    public float lifetimeOffset;
    public float lerpLifetimeOffset;
    public int lerpTimes;
    public Color lerpColor1;
    public Color lerpColor2;
    public bool isActive;
    public string id; // <-- persistent spline GUID
    public string lerpSplineId; // <-- GUID of linked lerp spline
}

[System.Serializable]
public class SaveData
{
    public List<SplineData> splines = new();
}

[System.Serializable]
public class SaveMeta
{
    public string displayName;
    public string filename;
    public long timestamp; // ticks (DateTime.UtcNow.Ticks)
}