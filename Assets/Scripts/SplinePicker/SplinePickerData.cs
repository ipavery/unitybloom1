using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "SplinePickerData", menuName = "Scriptable Objects/SplinePickerData")]
public class SplinePickerData : ScriptableObject
{
    public bool isInputBlocked = InputBlocker.IsInputBlocked("Unblocked UI Layer");
    public GameObject lastHighlighted = null;
    public bool isSelecting = false;
    public List<ControlPointGroup> controlSphereGroup = new();
    public int activeGizmoAxis = -1; // 0=X, 1=Y, 2=Z. -1 means not currently dragging
    public Color highlightColor = Color.yellow;
    public float gizmoEmissionIntensity = 0.6f; // Emission intensity for gizmos
}
