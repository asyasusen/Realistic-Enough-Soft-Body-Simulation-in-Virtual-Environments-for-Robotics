using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(BezierCableGenerator))]
public class BezierCableEditor : Editor
{
    private BezierCableGenerator generator;

    private void OnEnable()
    {
        generator = (BezierCableGenerator)target;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(10);
        if (GUILayout.Button("Generate Physics Cable", GUILayout.Height(40)))
        {
            generator.GenerateCable();
        }
    }

    private void OnSceneGUI()
    {
        if (generator.ControlPoints == null || generator.ControlPoints.Length < 2) return;

        int count = generator.ControlPoints.Length;

        // 1. Draw all handles and labels dynamically
        for (int i = 0; i < count; i++)
        {
            Vector3 worldPoint = generator.transform.TransformPoint(generator.ControlPoints[i]);
            
            Handles.Label(worldPoint + new Vector3(0.1f, 0.2f, 0), "P" + i);

            Handles.color = Color.yellow;
            Handles.SphereHandleCap(0, worldPoint, Quaternion.identity, 0.1f, EventType.Repaint);

            EditorGUI.BeginChangeCheck();
            Vector3 newWorldPoint = Handles.PositionHandle(worldPoint, Quaternion.identity);
            
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(generator, "Move Cable Control Point");
                generator.ControlPoints[i] = generator.transform.InverseTransformPoint(newWorldPoint);
            }
        }

        // 2. Draw the visual path connecting the dots (straight lines)
        Handles.color = Color.gray;
        for (int i = 0; i < count - 1; i++)
        {
            Handles.DrawDottedLine(
                generator.transform.TransformPoint(generator.ControlPoints[i]), 
                generator.transform.TransformPoint(generator.ControlPoints[i+1]), 
                4f);
        }

        // 3. Draw the actual smooth continuous curve
        Handles.color = Color.cyan;
        int resolution = 20 * count; // Calculate enough points to make the visual line look perfectly smooth
        Vector3[] smoothCurvePoints = new Vector3[resolution];
        
        for(int i = 0; i < resolution; i++)
        {
            float t = i / (float)(resolution - 1);
            smoothCurvePoints[i] = generator.GetPoint(t);
        }
        
        Handles.DrawPolyLine(smoothCurvePoints);
    }
}