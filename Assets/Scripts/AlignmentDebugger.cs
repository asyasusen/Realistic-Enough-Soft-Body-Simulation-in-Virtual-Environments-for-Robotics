#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public class AlignmentDebugger : MonoBehaviour
{
    [Header("Drag your objects here")]
    public Transform plugTransform;   
    public Transform targetTransform; 

    [Header("Gizmo Settings")]
    public float arrowLength = 0.15f;

    private void OnDrawGizmos()
    {
        if (plugTransform == null || targetTransform == null) return;

        DrawAxes(targetTransform.position, targetTransform.rotation, Color.red, Color.green, Color.blue);


        DrawAxes(plugTransform.position, plugTransform.rotation, 
                 new Color(1f, 0.5f, 0.5f), new Color(0.5f, 1f, 0.5f), new Color(0.5f, 0.5f, 1f));

        float angle = Quaternion.Angle(plugTransform.rotation, targetTransform.rotation);
        
        #if UNITY_EDITOR
        GUIStyle style = new GUIStyle();
        style.normal.textColor = angle < 10f ? Color.green : Color.red; 
        style.fontSize = 14;
        style.fontStyle = FontStyle.Bold;

        Handles.Label(targetTransform.position + Vector3.up * 0.1f, 
            $"Agent Math: {angle.ToString("F1")}°\n(Agent wants 0°)", style);
        
        Handles.color = Color.yellow;
        Handles.DrawDottedLine(plugTransform.position, targetTransform.position, 2f);
        #endif
    }

    private void DrawAxes(Vector3 pos, Quaternion rot, Color x, Color y, Color z)
    {
        Gizmos.color = x;
        Gizmos.DrawRay(pos, rot * Vector3.right * arrowLength);
        
        Gizmos.color = y;
        Gizmos.DrawRay(pos, rot * Vector3.up * arrowLength);
        
        Gizmos.color = z;
        Gizmos.DrawRay(pos, rot * Vector3.forward * arrowLength);
    }
}