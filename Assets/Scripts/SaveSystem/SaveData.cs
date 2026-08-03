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
    public float startTimeOffset;
    public int lerpTimes;
    public Color lerpColor1;
    public Color lerpColor2;
    public bool isActive;
    public int musicChannel;


    public float activationThreshhold;
    public string id; // <-- persistent spline GUID
    public string lerpSplineId; // <-- GUID of linked lerp spline

}

[System.Serializable]
public class SaveData
{
    public List<SplineData> splines = new();

    // Camera state
    public float camPosX = 0f;
    public float camPosY = 0f;
    public float camPosZ = -140f; // Set default Z position
    public float camRotX = 0f;
    public float camRotY = 0f;
    public float camRotZ = 0f;
}

[System.Serializable]
public class SaveMeta
{
    public string displayName;
    public string filename;
    public long timestamp; // ticks (DateTime.UtcNow.Ticks)
}