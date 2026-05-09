using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class PBDCable : MonoBehaviour
{
    [Header("Cable Settings")]
    public int particleCount = 20;
    public float segmentLength = 0.1f;
    public int solverIterations = 15; 
    public Vector3 gravity = new Vector3(0, -9.81f, 0);

    [Header("Physical Properties")]
    [Range(0f, 1f)]
    public float bendingStiffness = 0.5f; // 0 = string, 1 = solid metal rod

    [Header("Collision Settings")]
    public Transform collisionSphere; 
    public float sphereRadius = 0.5f;

    private Vector3[] positions;
    private Vector3[] previousPositions;
    private LineRenderer lineRenderer;

    void Start()
    {
        positions = new Vector3[particleCount];
        previousPositions = new Vector3[particleCount];
        
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = particleCount;
        lineRenderer.startWidth = 0.05f;
        lineRenderer.endWidth = 0.05f;

        for (int i = 0; i < particleCount; i++)
        {
            positions[i] = transform.position - new Vector3(0, i * segmentLength, 0);
            previousPositions[i] = positions[i];
        }
    }

    void FixedUpdate()
    {
        // 1. Predict new positions
        for (int i = 0; i < particleCount; i++)
        {
            if (i == 0) continue; 

            Vector3 velocity = positions[i] - previousPositions[i];
            previousPositions[i] = positions[i];
            
            positions[i] += velocity + gravity * (Time.fixedDeltaTime * Time.fixedDeltaTime);
        }

        // 2. Solve Constraints
        for (int iteration = 0; iteration < solverIterations; iteration++)
        {
            // A. Solve Stretch (Keep particles connected)
            for (int i = 0; i < particleCount - 1; i++)
            {
                Vector3 delta = positions[i + 1] - positions[i];
                float currentDistance = delta.magnitude;
                float error = currentDistance - segmentLength;

                if (currentDistance > 0.0001f) 
                {
                    Vector3 correction = delta.normalized * error * 0.5f; 
                    if (i != 0) positions[i] += correction; 
                    positions[i + 1] -= correction;
                }
            }

            // B. Solve Bending (Resist sharp angles)
            // We check the distance between particle [i] and [i+2]
            if (bendingStiffness > 0f)
            {
                for (int i = 0; i < particleCount - 2; i++)
                {
                    Vector3 delta = positions[i + 2] - positions[i];
                    float currentDistance = delta.magnitude;
                    float targetDistance = segmentLength * 2f; // Length if perfectly straight
                    float error = currentDistance - targetDistance;

                    if (currentDistance > 0.0001f)
                    {
                        // Apply correction, multiplied by our stiffness slider
                        Vector3 correction = delta.normalized * error * 0.5f * bendingStiffness;
                        if (i != 0) positions[i] += correction; 
                        positions[i + 2] -= correction;
                    }
                }
            }

            // C. Solve Collision (Keep out of the sphere)
            if (collisionSphere != null)
            {
                float trueCollisionRadius = sphereRadius + (lineRenderer.startWidth / 2f); 

                for (int i = 0; i < particleCount; i++)
                {
                    Vector3 toParticle = positions[i] - collisionSphere.position;
                    float distanceToCenter = toParticle.magnitude;

                    if (distanceToCenter < trueCollisionRadius && distanceToCenter > 0.0001f)
                    {
                        Vector3 pushOutDirection = toParticle.normalized;
                        positions[i] = collisionSphere.position + (pushOutDirection * trueCollisionRadius);
                    }
                }
            }
        }

        // 3. Update Visuals
        lineRenderer.SetPositions(positions);
    }
}