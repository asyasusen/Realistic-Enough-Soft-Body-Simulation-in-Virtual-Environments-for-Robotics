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

    void Start()
    {
        cam = GetComponent<Camera>();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                selectedBody = hit.collider.GetComponent<ArticulationBody>();
                
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
}