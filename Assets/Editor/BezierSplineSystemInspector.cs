using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BezierSpline))]
[CanEditMultipleObjects]
public class BezierSplineInspector : Editor
{
	private int splineInstanceCount = 1;
    private const int lineSteps = 10;
    private const float directionScale = 0.5f;
	
    private BezierSpline spline;
    private string instanceName;
    private BezierSpline[] splineList;
    private Transform handleTransform;
    private Quaternion handleRotation;

    private void OnSceneGUI()
    {
        spline = target as BezierSpline;

        handleTransform = spline.transform;
        handleRotation = Tools.pivotRotation == PivotRotation.Local ?
            handleTransform.rotation : Quaternion.identity;

        if ((spline.points.Length - 1) % 3 == 0 && spline.points.Length >=4)
        {
            Vector3 p0 = ShowPoint(0);
            for (int i = 1; i < spline.points.Length; i += 3)
            {
                Vector3 p1 = ShowPoint(i);
                Vector3 p2 = ShowPoint(i + 1);
                Vector3 p3 = ShowPoint(i + 2);

                Handles.color = Color.gray;
                Handles.DrawLine(p0, p1);
                Handles.DrawLine(p2, p3);

                Handles.DrawBezier(p0, p3, p1, p2, Color.white, null, 2f);
                p0 = p3;
            }
            ShowDirections();
        }

    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        for (int i = 0; i < serializedObject.targetObjects.Length; i++)
        {
            spline = (BezierSpline)serializedObject.targetObjects[i];
            if (GUILayout.Button("Add Curve to " + serializedObject.targetObjects[i].name))
            {
                Undo.RecordObject(spline, "Add Curve");
                spline.AddCurve();
                EditorUtility.SetDirty(spline);
            }
            if (GUILayout.Button("Remove Curve from " + serializedObject.targetObjects[i].name))
            {
                Undo.RecordObject(spline, "Remove Curve");
                spline.RemoveCurve();
                EditorUtility.SetDirty(spline);
            }
        }
        if (GUILayout.Button("New Spline"))
        {
            Undo.RecordObject(spline, "New Spline");
            spline.AddSpline(splineInstanceCount);
            EditorUtility.SetDirty(spline);
			splineInstanceCount++;
        }
    }

    // public void Awake()
    // {
    //     splineList = serializedObject.targetObjects as BezierSpline[];
    //     // Debug.Log(target);
    //     // Debug.Log(serializedObject.targetObjects.Length);
    // }

    private const int stepsPerCurve = 10;

    private void ShowDirections()
    {
        Handles.color = Color.green;
        Vector3 point = spline.GetPoint(0f);
        Handles.DrawLine(point, point + spline.GetDirection(0f) * directionScale);
        int steps = stepsPerCurve * spline.CurveCount;
        for (int i = 1; i <= steps; i++)
        {
            point = spline.GetPoint(i / (float)steps);
            Handles.DrawLine(point, point + spline.GetDirection(i / (float)steps) * directionScale);
        }
    }

    private const float handleSize = 0.04f;
    private const float pickSize = 0.06f;

    private int selectedIndex = -1;

    private Vector3 ShowPoint(int index)
    {
        Vector3 point = handleTransform.TransformPoint(spline.points[index]);
        float size = HandleUtility.GetHandleSize(point)*2;
        Handles.color = Color.white;
        if (Handles.Button(point, handleRotation, size * handleSize, size * pickSize, Handles.DotHandleCap))
        {
            selectedIndex = index;
        }

        if (selectedIndex == index)
        {
            EditorGUI.BeginChangeCheck();
            point = Handles.DoPositionHandle(point, handleRotation);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(spline, "Move Point");
                EditorUtility.SetDirty(spline);
                spline.points[index] = handleTransform.InverseTransformPoint(point);
            }
        }
        return point;
    }
}