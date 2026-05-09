using UnityEngine;

public class BezierCableGenerator : MonoBehaviour
{
    [Header("Curve Settings")]
    [Tooltip("Add as many points as you want! The cable will smoothly connect all of them.")]
    [SerializeField] private Vector3[] controlPoints = new Vector3[6] {
        new Vector3(0, 0, 0),    
        new Vector3(2, -1, 0),    
        new Vector3(-2, -2, 1),  
        new Vector3(2, -3, -1),
        new Vector3(-1, -4, 0),
        new Vector3(0, -5, 0)
    };

    public Vector3[] ControlPoints 
    { 
        get { return controlPoints; } 
        set { controlPoints = value; } 
    }

    [Header("Cable Settings")]
    [SerializeField] private int segmentCount = 30;
    [SerializeField] private float cableRadius = 0.05f;
    [SerializeField] private float massPerSegment = 1.0f; 
    [SerializeField] private float stiffness = 100f; 
    [SerializeField] private float damping = 10f;

    [Header("Custom Physics")]
    [SerializeField] private Vector3 customGravity = new Vector3(0, -20f, 0); 

    [Header("Advanced Stability Settings")]
    [SerializeField] private float linearDamping = 0.5f; 
    [SerializeField] private float angularDamping = 0.5f;
    [SerializeField] private float jointFriction = 1.0f;
    [SerializeField] private float maxDepenetrationVelocity = 1f;
    [SerializeField] private float maxAngularVelocity = 15f;
    [SerializeField] private float maxLinearVelocity = 15f; 

    [Header("Visual Settings")]
    [SerializeField] private bool useLineRenderer = true;
    [Tooltip("Drag your Material here.")]
    [SerializeField] private Material cableMaterial;
    
    [Header("Connector Attachment")]
    [Tooltip("Drag your Plug Prefab here")]
    public GameObject plugPrefab;
    
    [Tooltip("Name assigned to the spawned plug (Must match SocketTrigger target)")]
    public string plugInstanceName = "Rj45";

    [Tooltip("Local offset for the plug (X=Left/Right, Y=Up/Down, Z=Forward/Backward)")]
    public Vector3 plugPositionOffset = new Vector3(0, 0, 0.05f);

    [Tooltip("Rotate the plug so its back faces the cable. Example: (0, 90, 0)")]
    public Vector3 plugRotationOffset = new Vector3(180, 0, 0);

    private ArticulationBody[] generatedBodies;

    public Vector3 GetPoint(float t)
    {
        if (controlPoints == null || controlPoints.Length < 2) return transform.position;
        
        t = Mathf.Clamp01(t);
        if (t == 1f) return transform.TransformPoint(controlPoints[controlPoints.Length - 1]);

        float p = t * (controlPoints.Length - 1);
        int i = Mathf.FloorToInt(p);
        float localT = p - i;

        Vector3 p0 = i == 0 ? controlPoints[0] - (controlPoints[1] - controlPoints[0]) : controlPoints[i - 1];
        Vector3 p1 = controlPoints[i];
        Vector3 p2 = controlPoints[i + 1];
        Vector3 p3 = i + 2 >= controlPoints.Length ? controlPoints[i + 1] + (controlPoints[i + 1] - controlPoints[i]) : controlPoints[i + 2];

        Vector3 localPos = GetCatmullRomPosition(localT, p0, p1, p2, p3);
        return transform.TransformPoint(localPos); 
    }

    private Vector3 GetCatmullRomPosition(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        Vector3 a = 2f * p1;
        Vector3 b = p2 - p0;
        Vector3 c = 2f * p0 - 5f * p1 + 4f * p2 - p3;
        Vector3 d = -p0 + 3f * p1 - 3f * p2 + p3;
        return 0.5f * (a + (b * t) + (c * t * t) + (d * t * t * t));
    }

    public Vector3 GetTangent(float t)
    {
        float tA = Mathf.Clamp01(t - 0.01f);
        float tB = Mathf.Clamp01(t + 0.01f);
        return (GetPoint(tB) - GetPoint(tA)).normalized;
    }

    public void GenerateCable()
    {
        for (int i = transform.childCount - 1; i >= 0; i--) {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isPlaying) {

                child.SetActive(false); 
                Destroy(child);
            } else {
                DestroyImmediate(child);
            }
        }

        generatedBodies = new ArticulationBody[segmentCount];
        ArticulationBody previousBody = null;
        float step = 1f / (segmentCount - 1);
        CapsuleCollider[] colliders = new CapsuleCollider[segmentCount];

        for (int i = 0; i < segmentCount; i++)
        {
            float t = i * step;
            Vector3 position = GetPoint(t);
            Vector3 tangent = GetTangent(t);
            
            float nextT = Mathf.Clamp01(t + step);
            float segmentLength = Vector3.Distance(GetPoint(t), GetPoint(nextT));
            if (i == segmentCount - 1) segmentLength = Vector3.Distance(GetPoint(t - step), GetPoint(t)); 

            GameObject segmentNode = new GameObject("Segment_" + i);
            segmentNode.transform.position = position;
            
            if (tangent != Vector3.zero) 
                segmentNode.transform.rotation = Quaternion.FromToRotation(Vector3.up, tangent);

            CapsuleCollider col = segmentNode.AddComponent<CapsuleCollider>();
            col.radius = cableRadius * 0.8f; 
            col.height = segmentLength; 
            col.direction = 1; 
            colliders[i] = col; 

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Collider visualCollider = visual.GetComponent<Collider>();
            if (Application.isPlaying) {
                visualCollider.enabled = false; 
                Destroy(visualCollider);       
            } else {
                DestroyImmediate(visualCollider);
            }
            visual.name = "Mesh";
            visual.transform.SetParent(segmentNode.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = new Vector3(cableRadius * 2f, segmentLength / 2f, cableRadius * 2f);

            ArticulationBody ab = segmentNode.AddComponent<ArticulationBody>();
            ab.mass = massPerSegment;
            ab.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            
            ab.useGravity = false; 
            ab.linearDamping = this.linearDamping;           
            ab.angularDamping = this.angularDamping;          
            ab.jointFriction = this.jointFriction;           
            ab.maxDepenetrationVelocity = this.maxDepenetrationVelocity;
            ab.maxAngularVelocity = this.maxAngularVelocity;     
            ab.maxLinearVelocity = this.maxLinearVelocity;      

            if (i == 0)
            {
                segmentNode.transform.SetParent(this.transform);
                ab.jointType = ArticulationJointType.FixedJoint;
                ab.immovable = true; 
            }
            else
            {
                segmentNode.transform.SetParent(previousBody.transform);
                ab.jointType = ArticulationJointType.SphericalJoint;

                ab.parentAnchorPosition = new Vector3(0, segmentLength / 2f, 0); 
                ab.anchorPosition = new Vector3(0, -segmentLength / 2f, 0);

                ArticulationDrive drive = new ArticulationDrive() {
                    stiffness = this.stiffness,
                    damping = this.damping,
                    forceLimit = 500f, 
                    target = 0f 
                };

                ab.xDrive = drive; ab.yDrive = drive; ab.zDrive = drive;
                ab.twistLock = ArticulationDofLock.FreeMotion;
                ab.swingYLock = ArticulationDofLock.FreeMotion;
                ab.swingZLock = ArticulationDofLock.FreeMotion;
            }

            previousBody = ab;
            generatedBodies[i] = ab;
        }

        int ignoreRadius = 2; 
        for (int i = 0; i < segmentCount; i++)
        {
            for (int j = i + 1; j < segmentCount; j++)
            {
                if (j - i <= ignoreRadius)
                {
                    Physics.IgnoreCollision(colliders[i], colliders[j], true);
                }
            }
        }

        if (useLineRenderer)
        {
            LineRenderer lr = gameObject.GetComponent<LineRenderer>();
            if (lr == null) lr = gameObject.AddComponent<LineRenderer>();
            
            lr.startWidth = cableRadius * 2f;
            lr.endWidth = cableRadius * 2f;
            
            if (cableMaterial != null) lr.sharedMaterial = cableMaterial;

            SmoothCableVisuals visuals = gameObject.GetComponent<SmoothCableVisuals>();
            if (visuals == null) visuals = gameObject.AddComponent<SmoothCableVisuals>();
            
            Transform[] nodes = new Transform[segmentCount];
            for(int i = 0; i < segmentCount; i++) 
            {
                nodes[i] = generatedBodies[i].transform;
                
                MeshRenderer blockyMesh = generatedBodies[i].transform.GetChild(0).GetComponent<MeshRenderer>();
                if (blockyMesh != null) blockyMesh.enabled = false;
            }
            visuals.cableNodes = nodes;
        }
        else
        {
            LineRenderer lr = gameObject.GetComponent<LineRenderer>();
            if (lr != null) 
            {
                if (Application.isPlaying) Destroy(lr);
                else DestroyImmediate(lr);
            }

            SmoothCableVisuals visuals = gameObject.GetComponent<SmoothCableVisuals>();
            if (visuals != null) 
            {
                if (Application.isPlaying) Destroy(visuals);
                else DestroyImmediate(visuals);
            }

            for(int i = 0; i < segmentCount; i++) 
            {
                MeshRenderer blockyMesh = generatedBodies[i].transform.GetChild(0).GetComponent<MeshRenderer>();
                if (blockyMesh != null) 
                {
                    blockyMesh.enabled = true;
                    if (cableMaterial != null) blockyMesh.sharedMaterial = cableMaterial;
                }
            }
        }
        if (plugPrefab != null)
        {
            GameObject head = Instantiate(plugPrefab);
            head.name = plugInstanceName;
            
            ArticulationBody lastSegment = generatedBodies[segmentCount - 1];
            
            Vector3 finalTangent = GetTangent(1f);
            
            // 1. Set the rotation first
            if (finalTangent != Vector3.zero) 
            {
                head.transform.rotation = Quaternion.LookRotation(finalTangent) * Quaternion.Euler(plugRotationOffset); 
            }

            // 2. Apply the offset relative to the plug's newly calculated rotation
            head.transform.position = lastSegment.transform.position + (head.transform.rotation * plugPositionOffset);

            head.transform.SetParent(lastSegment.transform, true);
            
            ArticulationBody headAB = head.GetComponent<ArticulationBody>();
            if (headAB == null) headAB = head.AddComponent<ArticulationBody>();
            
            headAB.jointType = ArticulationJointType.FixedJoint;
            

            headAB.mass = massPerSegment * 1.5f; 
            
            Collider[] headColliders = head.GetComponentsInChildren<Collider>();
            
            int segmentsToIgnore = Mathf.Min(3, segmentCount);
            for (int i = segmentCount - 1; i >= segmentCount - segmentsToIgnore; i--)
            {
                if (colliders[i] != null) 
                {
                    foreach (Collider hc in headColliders)
                    {
                        Physics.IgnoreCollision(hc, colliders[i], true);
                    }
                }
            }
            
      
        }
    }

    void FixedUpdate()
    {
        if (generatedBodies == null) return;

        foreach (ArticulationBody ab in generatedBodies)
        {
            if (ab != null && !ab.immovable)
            {
                ab.AddForce(customGravity, ForceMode.Acceleration);
            }
        }
    }
}