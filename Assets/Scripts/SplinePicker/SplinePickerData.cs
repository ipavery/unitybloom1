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
    
}
