using UnityEngine;

public class PlugEnvironmentManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BezierCableGenerator cableGenerator;
    [SerializeField] private Transform socketParent; 
    [SerializeField] private Transform socketCenterPoint; 

    [Header("Spawn Settings")]
    public Vector3 spawnExtents = new Vector3(2f, 5f, 5f);

    [Tooltip("How far away from the cable tip should the wall spawn? Increase this to make the agent stretch the cable further.")]
    public float distanceFromTip = 3.0f;

    [Tooltip("Because the child socket is rotated (90,0,90), its hole faces sideways. This rotates the parent to compensate.")]
    public float yRotationFix = -90f; 

    [Header("Curriculum / Difficulty")]
    [Range(0f, 1f)]
    public float randomnessFactor = 1.0f;

    private Vector3[] baseControlPoints;

    public void ResetEnvironment()
    {
        EnsureInitialized();
        cableGenerator.ResetCableState();
        RandomizeSocket();
    }

    private void EnsureInitialized()
    {
        if (baseControlPoints == null || baseControlPoints.Length == 0)
        {
            baseControlPoints = (Vector3[])cableGenerator.ControlPoints.Clone();
            cableGenerator.GenerateCable(); 
        }
    }

    private void RandomizeSocket()
    {
        Vector3 targetCenterWorld = cableGenerator.GetPoint(1f);
        Vector3 rootCenterWorld = cableGenerator.GetPoint(0f);


        Vector3 directionOutward = targetCenterWorld - rootCenterWorld;
        directionOutward.y = 0; 
        directionOutward.Normalize();

        Vector3 baseHolePos = targetCenterWorld + (directionOutward * distanceFromTip);


        Vector3 directionToRoot = rootCenterWorld - baseHolePos;
        directionToRoot.y = 0; 
        
        if (directionToRoot != Vector3.zero)
        {
            Quaternion lookRot = Quaternion.LookRotation(directionToRoot);
            float randomYaw = Random.Range(-30f, 30f) * randomnessFactor;
            
            socketParent.rotation = lookRot * Quaternion.Euler(0, yRotationFix + randomYaw, 0);
        }


        float rx = Random.Range(-spawnExtents.x, spawnExtents.x) * randomnessFactor;
        float ry = Random.Range(-spawnExtents.y, spawnExtents.y) * randomnessFactor;
        float rz = Random.Range(-spawnExtents.z, spawnExtents.z) * randomnessFactor;
        Vector3 worldOffset = transform.TransformDirection(new Vector3(rx, ry, rz));

        Vector3 desiredHolePos = baseHolePos + worldOffset;


        Vector3 pivotOffset = socketCenterPoint.position - socketParent.position;
        socketParent.position = desiredHolePos - pivotOffset;
    }
}