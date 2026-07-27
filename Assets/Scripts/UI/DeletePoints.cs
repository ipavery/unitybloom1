using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class DeletePoints : MonoBehaviour
{
    [SerializeField] private SplinePickerData SPD;

    [Header("Input")]
    [Tooltip("Optional: Bind a key like 'Delete' or 'Backspace'")]
    public Button deleteButton;

    void Start()
    {
        if (deleteButton != null)
            deleteButton.onClick.AddListener(OnDeletePerformed);
    }

    private void OnDeletePerformed()
    {
        DeleteSelected();
    }

    public void DeleteSelected()
    {
        if (SPD == null || SPD.controlSphereGroup == null) return;

        var selectedGroups = SPD.controlSphereGroup.Where(g => g.isSelected).ToList();
        if (selectedGroups.Count == 0) return;

        var pointsBySpline = selectedGroups
            .Where(g => g.sphereObject != null)
            .GroupBy(g => g.sphereObject.GetComponentInParent<BezierSpline>())
            .Where(group => group.Key != null);

        foreach (var splineGroup in pointsBySpline)
        {
            BezierSpline spline = splineGroup.Key;

            // A single curve is 4 points. Total curves = (Length - 1) / 3
            int totalCurves = (spline.points.Length - 1) / 3;
            if (totalCurves <= 1)
            {
                Debug.LogWarning($"Spline '{spline.name}' only has one curve. Cannot delete points.");
                continue;
            }

            // Find the extremes of the user's selection
            int minIdx = splineGroup.Min(cp => cp.index);
            int maxIdx = splineGroup.Max(cp => cp.index);
            int maxSplineIndex = spline.points.Length - 1;

            // Determine if the selection is anchored to the start or the end
            bool deleteStart = minIdx <= 2;
            bool deleteEnd = maxIdx >= maxSplineIndex - 2;

            if (deleteStart && deleteEnd)
            {
                Debug.LogWarning("Cannot delete from both ends simultaneously, or selection spans entire spline. Please select only one end.");
                continue; 
            }

            // --- GET ALL SPHERES BELONGING TO THIS SPLINE IN ORDER ---
            var splineSpheres = SPD.controlSphereGroup
                .Where(g => g.sphereObject != null && g.sphereObject.transform.IsChildOf(spline.transform))
                .OrderBy(g => g.index)
                .ToList();

            if (deleteStart)
            {
                UndoManager.Instance.RecordState(); // Record state before deletion for undo functionality
                
                // Calculate how many curves to delete based on the furthest selected point
                int curvesToDelete = (maxIdx == 0) ? 1 : (maxIdx + 2) / 3;
                
                // Prevent deleting the entire spline
                if (curvesToDelete >= totalCurves) 
                    curvesToDelete = totalCurves - 1;

                int pointsToRemove = curvesToDelete * 3;

                // 1. Update the math array
                Vector3[] newPoints = new Vector3[spline.points.Length - pointsToRemove];
                System.Array.Copy(spline.points, pointsToRemove, newPoints, 0, newPoints.Length);
                spline.ResetWithPoints(newPoints);

                // 2. Destroy the physical spheres and remove them from data
                for (int i = 0; i < pointsToRemove; i++)
                {
                    SPD.controlSphereGroup.Remove(splineSpheres[i]);
                    Destroy(splineSpheres[i].sphereObject);
                }

                // 3. Shift the indices of all remaining spheres down
                for (int i = pointsToRemove; i < splineSpheres.Count; i++)
                {
                    splineSpheres[i].index -= pointsToRemove;
                }

                SaveLoadUI.Instance.NotifyActionPerformed(); //autosave

                Debug.Log($"Deleted {curvesToDelete} START curve(s) of '{spline.name}'.");
            }
            else if (deleteEnd)
            {
                UndoManager.Instance.RecordState(); // Record state before deletion for undo functionality

                // Calculate how many curves to delete based on the furthest selected point from the end
                int distanceFromEnd = maxSplineIndex - minIdx;
                int curvesToDelete = (distanceFromEnd == 0) ? 1 : (distanceFromEnd + 2) / 3;

                // Prevent deleting the entire spline
                if (curvesToDelete >= totalCurves) 
                    curvesToDelete = totalCurves - 1;

                int pointsToRemove = curvesToDelete * 3;

                // 1. Update the math array
                Vector3[] newPoints = new Vector3[spline.points.Length - pointsToRemove];
                System.Array.Copy(spline.points, 0, newPoints, 0, newPoints.Length);
                spline.ResetWithPoints(newPoints);

                // 2. Destroy the physical spheres and remove them from data
                int count = splineSpheres.Count;
                for (int i = count - 1; i >= count - pointsToRemove; i--)
                {
                    SPD.controlSphereGroup.Remove(splineSpheres[i]);
                    Destroy(splineSpheres[i].sphereObject);
                }

                SaveLoadUI.Instance.NotifyActionPerformed(); //autosave

                Debug.Log($"Deleted {curvesToDelete} END curve(s) of '{spline.name}'.");
            }
            else
            {
                Debug.LogWarning("Selected points are in the middle of the spline. To avoid reshuffling errors, only segments connected to the ends can be deleted.");
                continue; 
            }
        }

        // Cleanup UI and Selection state
        EventHub.Publish(new ClearSelection(true));
        EventHub.Publish(new SplineSelectionChange(false)); //just false means to just deselect i hope
        SPD.lastHighlighted = null;
        SPD.activeGizmoAxis = (int)SplinePickerData.GizmoType.None;
    }
}