using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class SmoothCableVisuals : MonoBehaviour
{
    [HideInInspector] 
    public Transform[] cableNodes;

    private LineRenderer lineRenderer;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        
        lineRenderer.numCapVertices = 8;
        lineRenderer.numCornerVertices = 8;
    }

    void LateUpdate()
    {
        if (cableNodes == null || cableNodes.Length == 0) return;

        lineRenderer.positionCount = cableNodes.Length;
        for (int i = 0; i < cableNodes.Length; i++)
        {
            if (cableNodes[i] != null)
            {
                lineRenderer.SetPosition(i, cableNodes[i].position);
            }
        }
    }
}