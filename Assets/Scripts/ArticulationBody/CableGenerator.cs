using UnityEngine;

public class CableGenerator : MonoBehaviour
{
    [Header("Cable Dimensions")]
    public int segmentCount = 15;
    public float segmentLength = 0.1f;
    public float cableThickness = 0.02f;

    [Header("Segment Mass & Friction")]
    public float massPerSegment = 0.5f;
    public float linearDamping = 0.05f;
    public float angularDamping = 0.05f;
    public float jointFriction = 0.05f;

    [Header("Joint Springs (Bending Resistance)")]
    public float stiffness = 10f; 
    public float damping = 5f;    
    public float forceLimit = 10000f;

    void Start()
    {
        GenerateCable();
    }

    void GenerateCable()
    {
        ArticulationBody previousBody = null;

        for (int i = 0; i < segmentCount; i++)
        {
            GameObject segment = new GameObject("Segment_" + i);
            segment.transform.position = transform.position - new Vector3(0, i * segmentLength, 0);
            
            CapsuleCollider col = segment.AddComponent<CapsuleCollider>();
            col.radius = cableThickness / 2f;
            col.height = segmentLength;

            GameObject graphic = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(graphic.GetComponent<Collider>());
            graphic.transform.SetParent(segment.transform);
            graphic.transform.localPosition = Vector3.zero;
            graphic.transform.localScale = new Vector3(cableThickness, segmentLength / 2f, cableThickness); 

            ArticulationBody ab = segment.AddComponent<ArticulationBody>();
            
            ab.mass = massPerSegment;
            ab.linearDamping = linearDamping;
            ab.angularDamping = angularDamping;
            ab.jointFriction = jointFriction;

            if (i == 0)
            {
                ab.immovable = true; 
                segment.transform.SetParent(this.transform);
            }
            else
            {
                segment.transform.SetParent(previousBody.transform);
                ab.jointType = ArticulationJointType.SphericalJoint;
                
                ab.anchorPosition = new Vector3(0, segmentLength / 2f, 0);
                ab.parentAnchorPosition = new Vector3(0, -segmentLength / 2f, 0);

                ArticulationDrive drive = new ArticulationDrive
                {
                    stiffness = this.stiffness,
                    damping = this.damping,
                    forceLimit = this.forceLimit
                };

                ab.xDrive = drive; ab.yDrive = drive; ab.zDrive = drive;

                ab.twistLock = ArticulationDofLock.FreeMotion;
                ab.swingYLock = ArticulationDofLock.FreeMotion;
                ab.swingZLock = ArticulationDofLock.FreeMotion;
            }

            previousBody = ab;
        }
    }
}