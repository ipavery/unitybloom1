using System;
using UnityEngine;

[Serializable]
public class ControlPointGroup
{
    public GameObject sphereObject;              // or whatever your “line” type is
    public bool isSelected; // list of particles for this line
    public int index; // Index of the control point in the spline
}