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
        if (generator.ControlPoints == null || generator.ControlPoints.Length < 4) return;

        for (int i = 0; i < 4; i++)
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

        Handles.color = Color.gray;
        Handles.DrawDottedLine(
            generator.transform.TransformPoint(generator.ControlPoints[0]), 
            generator.transform.TransformPoint(generator.ControlPoints[1]), 
            4f);
        Handles.DrawDottedLine(
            generator.transform.TransformPoint(generator.ControlPoints[2]), 
            generator.transform.TransformPoint(generator.ControlPoints[3]), 
            4f);

        Vector3 p0 = generator.transform.TransformPoint(generator.ControlPoints[0]);
        Vector3 p1 = generator.transform.TransformPoint(generator.ControlPoints[1]);
        Vector3 p2 = generator.transform.TransformPoint(generator.ControlPoints[2]);
        Vector3 p3 = generator.transform.TransformPoint(generator.ControlPoints[3]);
        
        Handles.DrawBezier(p0, p3, p1, p2, Color.cyan, null, 4f);
    }
}