using UnityEngine;

[RequireComponent(typeof(Camera))]
public class MouseDragCable : MonoBehaviour
{
    [Header("Interaction Settings")]
    public float pullForce = 500f;
    public float dragDamping = 50f;
    
    [Tooltip("How fast the scroll wheel pushes/pulls the object in 3D space.")]
    public float scrollSensitivity = 1.5f;

    private ArticulationBody selectedBody;
    private float cameraZDistance;
    private Vector3 targetPosition;
    private Camera cam;

    // --- NEW: Squeeze Feature Variables ---
    private ArticulationBody squeezedClip;
    private float originalDriveTarget;

    void Start()
    {
        cam = GetComponent<Camera>();
    }

    void Update()
    {
        // --- LEFT CLICK: GRAB & DRAG ---
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                // Using GetComponentInParent safely grabs the main body even if you click a child collider
                selectedBody = hit.collider.GetComponentInParent<ArticulationBody>();
                
                if (selectedBody != null)
                {
                    cameraZDistance = cam.WorldToScreenPoint(selectedBody.transform.position).z;
                }
            }
        }

        if (Input.GetMouseButton(0) && selectedBody != null)
        {
            float scroll = Input.mouseScrollDelta.y;
            cameraZDistance += scroll * scrollSensitivity;
            cameraZDistance = Mathf.Max(0.5f, cameraZDistance);

            Vector3 screenPosition = new Vector3(Input.mousePosition.x, Input.mousePosition.y, cameraZDistance);
            targetPosition = cam.ScreenToWorldPoint(screenPosition);
        }

        if (Input.GetMouseButtonUp(0))
        {
            selectedBody = null;
        }

        // --- RIGHT CLICK: SQUEEZE THE CLIP ---
        if (Input.GetMouseButtonDown(1))
        {
            // 1. If we are currently holding the main plug with Left Click...
            if (selectedBody != null)
            {
                // Find the clip (the Revolute Joint attached to the plug)
                ArticulationBody[] bodies = selectedBody.GetComponentsInChildren<ArticulationBody>();
                foreach (ArticulationBody ab in bodies)
                {
                    if (ab.jointType == ArticulationJointType.RevoluteJoint)
                    {
                        SqueezeClip(ab);
                        break;
                    }
                }
            }
            // 2. If we aren't holding the plug, try to squeeze whatever we are directly aiming at
            else
            {
                Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    ArticulationBody hitBody = hit.collider.GetComponentInParent<ArticulationBody>();
                    if (hitBody != null && hitBody.jointType == ArticulationJointType.RevoluteJoint)
                    {
                        SqueezeClip(hitBody);
                    }
                }
            }
        }

        // --- RIGHT CLICK RELEASE: LET GO OF CLIP ---
        if (Input.GetMouseButtonUp(1))
        {
            if (squeezedClip != null)
            {
                ArticulationDrive drive = squeezedClip.xDrive;
                drive.target = originalDriveTarget; // The spring snaps it back up
                squeezedClip.xDrive = drive;
                squeezedClip = null;
            }
        }
    }

    void FixedUpdate()
    {
        if (selectedBody != null)
        {
            Vector3 displacement = targetPosition - selectedBody.transform.position;
            Vector3 force = (displacement * pullForce) - (selectedBody.linearVelocity * dragDamping);

            float maxPullForce = 150f; 
            if (force.magnitude > maxPullForce)
            {
                force = force.normalized * maxPullForce;
            }

            selectedBody.AddForce(force, ForceMode.Acceleration);
        }
    }

    // Helper function to handle the spring logic
    private void SqueezeClip(ArticulationBody clip)
    {
        squeezedClip = clip;
        ArticulationDrive drive = squeezedClip.xDrive;
        originalDriveTarget = drive.target;
        
        // Set the target to the Upper Limit (which acts as the fully compressed flat position)
        drive.target = drive.upperLimit; 
        squeezedClip.xDrive = drive;
    }
}