using UnityEngine;

public class PlugInputHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera mainCamera;

    [Header("Human Drag Physics")]
    public float pullForce = 250f;       
    public float dragDamping = 25f;      
    public float maxPullForce = 300f;    
    public float scrollSensitivity = 1.5f;

    public bool IsDragging { get; private set; }
    public bool IsSqueezing { get; private set; }
    public float CurrentPitch { get; private set; }
    public float CurrentRoll { get; private set; }

    private float cameraZDistance;
    private Vector3 localGrabOffset;
    private Vector3 targetPosition;
    
    private ArticulationBody plugBody;
    private ArticulationBody clipBody;

    public void Initialize(ArticulationBody plug, ArticulationBody clip)
    {
        plugBody = plug;
        clipBody = clip;
        IsDragging = false;
    }

    private void Update()
    {
        if (plugBody == null) return;

        IsSqueezing = Input.GetMouseButton(1);
        CurrentPitch = Input.GetAxis("Vertical");
        CurrentRoll = -Input.GetAxis("Horizontal");

        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                ArticulationBody hitBody = hit.collider.GetComponentInParent<ArticulationBody>();
                if (hitBody != null && (hitBody == plugBody || hitBody == clipBody || hitBody.transform.IsChildOf(plugBody.transform)))
                {
                    IsDragging = true;
                    cameraZDistance = mainCamera.WorldToScreenPoint(hit.point).z;
                    localGrabOffset = plugBody.transform.InverseTransformPoint(hit.point);
                }
            }
        }

        if (Input.GetMouseButton(0) && IsDragging)
        {
            float scroll = Input.mouseScrollDelta.y;
            cameraZDistance += scroll * scrollSensitivity;
            cameraZDistance = Mathf.Max(0.5f, cameraZDistance);

            Vector3 screenPosition = new Vector3(Input.mousePosition.x, Input.mousePosition.y, cameraZDistance);
            targetPosition = mainCamera.ScreenToWorldPoint(screenPosition);
        }

        if (Input.GetMouseButtonUp(0))
        {
            IsDragging = false;
        }
    }

    // Calculates the force vector strictly for the Agent's Heuristic callback
    public Vector3 CalculateHeuristicForce()
    {
        if (!IsDragging || plugBody == null) return Vector3.zero;

        Vector3 currentGrabWorldPos = plugBody.transform.TransformPoint(localGrabOffset);
        Vector3 displacement = targetPosition - currentGrabWorldPos;
        
        Vector3 rawForce = (displacement * pullForce) - (plugBody.linearVelocity * dragDamping);

        if (rawForce.magnitude > maxPullForce)
        {
            rawForce = rawForce.normalized * maxPullForce;
        }
        
        return rawForce / maxPullForce;
    }
}