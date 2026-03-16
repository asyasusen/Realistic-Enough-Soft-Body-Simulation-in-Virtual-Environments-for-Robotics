//NOTE
//Default Solver Iterations and change it from 6 to 30
//Default Solver Velocity Iterations and change it from 1 to 10


using UnityEngine;

public class BezierCableGenerator : MonoBehaviour
{
    [Header("Curve Settings")]
    [SerializeField] private Vector3[] controlPoints = new Vector3[4] {
        new Vector3(0, 0, 0),    
        new Vector3(3, 0, 0),    
        new Vector3(-3, -4, 0),  
        new Vector3(0, -4, 0)    
    };

    public Vector3[] ControlPoints 
    { 
        get { return controlPoints; } 
        set { controlPoints = value; } 
    }

    [Header("Cable Settings")]
    [SerializeField] private int segmentCount = 20;
    [SerializeField] private float cableRadius = 0.05f;
    [SerializeField] private float massPerSegment = 1.0f; 
    [SerializeField] private float stiffness = 100f; 
    [SerializeField] private float damping = 10f;

    [Header("Custom Physics")]
    [SerializeField] private Vector3 customGravity = new Vector3(0, -20f, 0); 

    [Header("Advanced Stability Settings")]
    [Tooltip("Acts as air resistance. Higher = falls slower, but stops flying away when swung.")]
    [SerializeField] private float linearDamping = 0.5f; 
    [Tooltip("Resistance to twisting and spinning.")]
    [SerializeField] private float angularDamping = 0.5f;
    [Tooltip("Internal friction in the joints. Higher = less wobbly, but stiffer to bend.")]
    [SerializeField] private float jointFriction = 1.0f;
    [Tooltip("Hard cap on how fast joints can push apart if they accidentally overlap.")]
    [SerializeField] private float maxDepenetrationVelocity = 1f;
    [Tooltip("Hard cap on rotational speed.")]
    [SerializeField] private float maxAngularVelocity = 15f;
    [Tooltip("Hard cap on movement speed. Lower this if the cable flies away too easily!")]
    [SerializeField] private float maxLinearVelocity = 20f;

    [Header("Visual Settings")]
    [SerializeField] private bool useLineRenderer = true;
    [SerializeField] private Material cableMaterial;

    private ArticulationBody[] generatedBodies;

    public Vector3 GetPoint(float t)
    {
        t = Mathf.Clamp01(t);
        float u = 1f - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        Vector3 p = uuu * controlPoints[0];
        p += 3f * uu * t * controlPoints[1];
        p += 3f * u * tt * controlPoints[2];
        p += ttt * controlPoints[3];
        
        return transform.TransformPoint(p); 
    }

    public Vector3 GetTangent(float t)
    {
        t = Mathf.Clamp01(t);
        float u = 1f - t;
        
        Vector3 tangent = 
            3f * u * u * (controlPoints[1] - controlPoints[0]) +
            6f * u * t * (controlPoints[2] - controlPoints[1]) +
            3f * t * t * (controlPoints[3] - controlPoints[2]);

        return transform.TransformDirection(tangent.normalized);
    }

    public void GenerateCable()
    {
        for (int i = transform.childCount - 1; i >= 0; i--) {
            DestroyImmediate(transform.GetChild(i).gameObject);
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
            DestroyImmediate(visual.GetComponent<Collider>()); 
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
            if (lr != null) DestroyImmediate(lr);
            
            SmoothCableVisuals visuals = gameObject.GetComponent<SmoothCableVisuals>();
            if (visuals != null) DestroyImmediate(visuals);

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