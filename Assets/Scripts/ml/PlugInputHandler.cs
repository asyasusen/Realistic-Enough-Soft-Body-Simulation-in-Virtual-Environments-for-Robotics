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
    public float CurrentYaw { get; private set; } 
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
        CurrentYaw = -Input.GetAxis("Horizontal");        
        CurrentRoll = 0f;
        if (Input.GetKey(KeyCode.Q)) CurrentRoll = 1f;
        if (Input.GetKey(KeyCode.E)) CurrentRoll = -1f;

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

    public Vector3 CalculateHeuristicForce()
    {
        if (!IsDragging || plugBody == null) return Vector3.zero;

        Vector3 currentGrabWorldPos = plugBody.transform.TransformPoint(localGrabOffset);
        Vector3 displacement = targetPosition - currentGrabWorldPos;
        
        Vector3 worldForce = (displacement * pullForce) - (plugBody.linearVelocity * dragDamping);

        if (worldForce.magnitude > maxPullForce)
        {
            worldForce = worldForce.normalized * maxPullForce;
        }
        
        Vector3 normalizedWorldForce = worldForce / maxPullForce;
        
        return plugBody.transform.InverseTransformDirection(normalizedWorldForce);
    }
}