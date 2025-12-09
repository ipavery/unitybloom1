using System.Collections.Generic;
using System.Drawing.Text;
using System.Linq;
using UnityEditor.Recorder;
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
    public Color unselectedOriginalColor = Color.white;

    List<Transform> GetDirectChildrenByName(Transform parent, string name)
    {
        List<Transform> output = new();
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name == name)
                output.Add(child);
        }
        
        return null;
    }
}
