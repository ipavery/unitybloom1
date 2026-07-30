using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "SplinePickerData", menuName = "Scriptable Objects/SplinePickerData")]
public class SplinePickerData : ScriptableObject
{
    public bool isInputBlocked = InputBlocker.IsInputBlocked("Unblocked UI Layer");
    public GameObject lastHighlighted = null;
    public bool isSelecting = false;

    //control spheres
    public List<ControlPointGroup> controlSphereGroup = new();
    public Color unselectedOriginalColor = Color.white;
    public float controlPointScale;

    //gizmos
    public Color highlightColor = Color.yellow;
    public float gizmoEmissionIntensity = 0.6f; // Emission intensity for gizmos
    public enum GizmoType
    {
        None = -1,
        MoveX = 0, MoveY = 1, MoveZ = 2,
        PlanarX = 3, PlanarY = 4, PlanarZ = 5,
        RotateX = 6, RotateY = 7, RotateZ = 8,
        ScaleX = 9, ScaleY = 10, ScaleZ = 11 // <--- ADD THESE
    }
    public int activeGizmoAxis = (int)GizmoType.None; // 0=X, 1=Y, 2=Z. -1 means not currently dragging

    //Selection mode - complex with gizmos, or easy direct drag
    public enum InteractionMode { Classic, DirectDrag }
    public InteractionMode currentInteractionMode = InteractionMode.DirectDrag;

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
