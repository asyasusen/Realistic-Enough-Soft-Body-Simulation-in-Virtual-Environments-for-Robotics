using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class PlugAgent : Agent
{
    [Header("Environment References")]
    [SerializeField] private BezierCableGenerator cableGenerator;
    [SerializeField] private Transform socketTransform;
    [SerializeField] private Camera mainCamera;

    [Header("Human Drag Physics")]
    public float pullForce = 500f;
    public float dragDamping = 50f;
    public float maxPullForce = 150f;
    public float scrollSensitivity = 1.5f;

    [Header("Rotation Physics")]
    public float rotationTorque = 500f; 
    public float angularDamping = 50f;
    public float maxTorque = 150f;
    
    private ArticulationBody plugBody;
    private ArticulationBody clipBody;
    private float originalClipTarget;

    private bool isDragging = false;
    private bool isSqueezing = false;
    private float currentPitch = 0f;
    private float currentRoll = 0f;
    private Vector3 targetPosition;
    private float cameraZDistance;

    public override void Initialize() { }

    public override void OnEpisodeBegin()
    {
        cableGenerator.GenerateCable();
        
        Transform plugTransform = null;
        

        Transform[] allChildren = cableGenerator.GetComponentsInChildren<Transform>(false);
        
        foreach (Transform child in allChildren)
        {
            if (child.name == cableGenerator.plugInstanceName)
            {
                plugTransform = child;
                break;
            }
        }

        if (plugTransform != null)
        {
            plugBody = plugTransform.GetComponent<ArticulationBody>();
            
            ArticulationBody[] bodies = plugTransform.GetComponentsInChildren<ArticulationBody>();
            foreach (var ab in bodies)
            {
                if (ab.jointType == ArticulationJointType.RevoluteJoint)
                {
                    clipBody = ab;
                    originalClipTarget = clipBody.xDrive.target;
                    break;
                }
            }
            
            plugBody.linearVelocity = Vector3.zero;
            plugBody.angularVelocity = Vector3.zero;
            isDragging = false; 
        }
        else
        {
            Debug.LogError($"Agent Reset Failed: Could not find generated plug named '{cableGenerator.plugInstanceName}'.");
        }
    }

    void Update()
    {
        if (plugBody == null) return;

        isSqueezing = Input.GetMouseButton(1);
        currentPitch = Input.GetAxis("Vertical");
        currentRoll = -Input.GetAxis("Horizontal");

        // 1. Raycast to see if we clicked the plug
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                ArticulationBody hitBody = hit.collider.GetComponentInParent<ArticulationBody>();
                
                if (hitBody != null)
                {
                    if (hitBody == plugBody || hitBody == clipBody || hitBody.transform.IsChildOf(plugBody.transform))
                    {
                        isDragging = true;
                        cameraZDistance = mainCamera.WorldToScreenPoint(plugBody.transform.position).z;
                        Debug.Log("<color=cyan><b>SUCCESS: Grabbed the RJ45 Plug!</b></color>");
                    }
                    else
                    {
                        Debug.Log($"<color=yellow>Missed! You clicked on: {hitBody.name} instead of the plug.</color>");
                    }
                }
                else
                {
                    Debug.Log($"Hit an object with no ArticulationBody: {hit.collider.name}");
                }
            }
        }

        // 2. Update target position while dragging
        if (Input.GetMouseButton(0) && isDragging)
        {
            float scroll = Input.mouseScrollDelta.y;
            cameraZDistance += scroll * scrollSensitivity;
            cameraZDistance = Mathf.Max(0.5f, cameraZDistance);

            Vector3 screenPosition = new Vector3(Input.mousePosition.x, Input.mousePosition.y, cameraZDistance);
            targetPosition = mainCamera.ScreenToWorldPoint(screenPosition);
        }

        // 3. Let go
        if (Input.GetMouseButtonUp(0))
        {
            if (isDragging) Debug.Log("Released the plug.");
            isDragging = false;
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (plugBody == null) 
        {
            for (int i = 0; i < 14; i++) sensor.AddObservation(0f);
            return;
        }

        sensor.AddObservation(plugBody.transform.position);
        sensor.AddObservation(socketTransform.position);
        sensor.AddObservation(socketTransform.position - plugBody.transform.position);
        sensor.AddObservation(plugBody.transform.rotation);

        bool clipSqueezed = clipBody != null && Mathf.Approximately(clipBody.xDrive.target, clipBody.xDrive.upperLimit);
        sensor.AddObservation(clipSqueezed ? 1.0f : 0.0f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (plugBody == null) return;

        // Linear Force
        Vector3 actionForce = new Vector3(
            actions.ContinuousActions[0],
            actions.ContinuousActions[1],
            actions.ContinuousActions[2]
        );
        plugBody.AddForce(actionForce * maxPullForce, ForceMode.Acceleration);

        // Rotational Torque
        float pitchInput = actions.ContinuousActions[3]; 
        float rollInput = actions.ContinuousActions[4];  

        Vector3 torqueDirection = (plugBody.transform.right * pitchInput) + (plugBody.transform.forward * rollInput);
        Vector3 appliedTorque = (torqueDirection * rotationTorque) - (plugBody.angularVelocity * angularDamping);
        
        if (appliedTorque.magnitude > maxTorque)
        {
            appliedTorque = appliedTorque.normalized * maxTorque;
        }

        plugBody.AddTorque(appliedTorque, ForceMode.Acceleration);

        // Squeeze
        int squeezeAction = actions.DiscreteActions[0];
        if (clipBody != null)
        {
            ArticulationDrive drive = clipBody.xDrive;
            drive.target = (squeezeAction == 1) ? drive.upperLimit : originalClipTarget;
            clipBody.xDrive = drive;
        }

        AddReward(-0.001f);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActionsOut = actionsOut.ContinuousActions;
        var discreteActionsOut = actionsOut.DiscreteActions;

        discreteActionsOut[0] = isSqueezing ? 1 : 0;

        Vector3 normalizedForce = Vector3.zero;

        if (isDragging)
        {
            Vector3 displacement = targetPosition - plugBody.transform.position;
            Vector3 rawForce = (displacement * pullForce) - (plugBody.linearVelocity * dragDamping);

            if (rawForce.magnitude > maxPullForce)
            {
                rawForce = rawForce.normalized * maxPullForce;
            }
            
            normalizedForce = rawForce / maxPullForce;
        }

        continuousActionsOut[0] = normalizedForce.x;
        continuousActionsOut[1] = normalizedForce.y;
        continuousActionsOut[2] = normalizedForce.z;
        continuousActionsOut[3] = currentPitch;
        continuousActionsOut[4] = currentRoll;
    }

    public void SuccessfullyPluggedIn()
    {
        AddReward(5.0f);
        EndEpisode();
    }
}