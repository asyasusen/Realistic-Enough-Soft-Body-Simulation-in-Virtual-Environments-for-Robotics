using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

[RequireComponent(typeof(PlugActuator), typeof(PlugInputHandler))]
public class PlugAgent : Agent
{
    [Header("Environment References")]
    [SerializeField] private BezierCableGenerator cableGenerator;
    [SerializeField] private Transform socketTransform;
    [Tooltip("Place an empty GameObject just outside the socket hole and assign it here.")]
    [SerializeField] private Transform entranceTransform; 
    [Tooltip("Place an empty GameObject exactly at the center of the socket hole.")]
    [SerializeField] private Transform targetHoleTransform;
    
    [Header("Agent Settings")]
    public int maxPatienceSteps = 2000;

    // Component References
    private PlugActuator actuator;
    private PlugInputHandler inputHandler;
    private ArticulationBody plugBody;
    private ArticulationBody clipBody;
    private Transform plugTipTransform;

    // Reward State
    private int stepsSinceProgress;
    private float closestDistanceReached;
    private int highestCheckpointReached = -1; 

    public override void Initialize()
    {
        actuator = GetComponent<PlugActuator>();
        inputHandler = GetComponent<PlugInputHandler>();
    }

    public override void OnEpisodeBegin()
    {
        cableGenerator.GenerateCable();
        FindAndSetupBodies();
        
        if (plugBody != null && clipBody != null)
        {
            actuator.Initialize(plugBody, clipBody);
            inputHandler.Initialize(plugBody, clipBody);
        }

        stepsSinceProgress = 0;
        highestCheckpointReached = -1;
        
        if (entranceTransform != null && plugBody != null)
        {
            closestDistanceReached = Vector3.Distance(plugBody.transform.position, entranceTransform.position);
        }
    }

    private void FindAndSetupBodies()
    {
        // Zero-allocation fetch straight from the generator
        plugBody = cableGenerator.PlugBody;

        if (plugBody != null)
        {
            plugBody.ResetCenterOfMass(); 
            
            Collider[] internalColliders = plugBody.GetComponentsInChildren<Collider>();
            for (int i = 0; i < internalColliders.Length; i++)
            {
                for (int j = i + 1; j < internalColliders.Length; j++)
                {
                    Physics.IgnoreCollision(internalColliders[i], internalColliders[j], true);
                }
            }
            
            ArticulationBody[] bodies = plugBody.GetComponentsInChildren<ArticulationBody>();
            foreach (var ab in bodies)
            {
                if (ab.jointType == ArticulationJointType.RevoluteJoint)
                {
                    clipBody = ab;
                    break;
                }
            }
            Transform[] allChildren = plugBody.GetComponentsInChildren<Transform>();
            foreach (Transform child in allChildren)
            {
                if (child.name == "Tip") 
                {
                    plugTipTransform = child;
                    break;
                }
            }
            
            if (plugTipTransform == null) 
                Debug.LogError("Could not find a child named 'Tip' on the Plug Prefab!");
        }
        
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (plugBody == null || plugTipTransform == null) 
        {
            for (int i = 0; i < 14; i++) sensor.AddObservation(0f);
            return;
        }

        // 1. Relative Position (3 obs)
        sensor.AddObservation(targetHoleTransform.position - plugTipTransform.position);        
        // 2. Relative Rotation (4 obs) - THIS IS CRITICAL
        // It tells the agent how misaligned it is from the socket's ideal rotation
        Quaternion relativeRotation = Quaternion.Inverse(socketTransform.rotation) * plugBody.transform.rotation;
        sensor.AddObservation(relativeRotation);

        // 3. Velocities (6 obs)
        sensor.AddObservation(plugBody.linearVelocity);
        sensor.AddObservation(plugBody.angularVelocity);

        // 4. Clip State (1 obs)
        bool clipSqueezed = clipBody != null && Mathf.Approximately(clipBody.xDrive.target, clipBody.xDrive.upperLimit);
        sensor.AddObservation(clipSqueezed ? 1.0f : 0.0f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (plugBody == null) return;

        Vector3 actionForce = new Vector3(
            actions.ContinuousActions[0],
            actions.ContinuousActions[1],
            actions.ContinuousActions[2]
        );
        
        actuator.ApplyLinearForce(actionForce);

        bool isGripped = actionForce.magnitude > 0.01f || inputHandler.IsDragging;

        if (isGripped) actuator.ApplyRotation(actions.ContinuousActions[3], actions.ContinuousActions[4]);
        else actuator.SyncTargetRotation();

        actuator.ApplySqueeze(actions.DiscreteActions[0] == 1);

        HandleRewards();
    }

    private void HandleRewards()
    {
        // 1. Math-based approach (Only useful BEFORE entering the socket)
        if (entranceTransform != null && highestCheckpointReached < 0) 
        {
            float currentDistance = Vector3.Distance(plugTipTransform.position, entranceTransform.position);            
            if (currentDistance < closestDistanceReached)
            {
                float newProgress = closestDistanceReached - currentDistance;
                AddReward(newProgress * 1.0f); 
                closestDistanceReached = currentDistance; 
                stepsSinceProgress = 0; 
            }
            else
            {
                stepsSinceProgress++;
            }
        }
        else 
        {
            stepsSinceProgress++;
        }

        if (plugBody != null && socketTransform != null)
        {
            // Calculate the absolute degree difference between plug and socket rotation
            float alignmentAngle = Quaternion.Angle(plugBody.transform.rotation, socketTransform.rotation);

            if (alignmentAngle > 27f)
            {
                // Punish for being misaligned (scales slightly so being 90 deg off is worse than 30)
                float penalty = (alignmentAngle / 180f) * -0.01f; 
                AddReward(penalty);
            }
            else if (highestCheckpointReached >= 0)
            {
                // Bonus for maintaining good alignment once inside the tunnel
                AddReward(0.001f);
            }
        }

        // 2. Kill Switch (Patience)
        if (stepsSinceProgress >= maxPatienceSteps)
        {
            AddReward(-1.0f); 
            EndEpisode();
            return; 
        }

        // Base time penalty
        AddReward(-0.0005f);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActionsOut = actionsOut.ContinuousActions;
        var discreteActionsOut = actionsOut.DiscreteActions;

        discreteActionsOut[0] = inputHandler.IsSqueezing ? 1 : 0;

        Vector3 normalizedForce = inputHandler.CalculateHeuristicForce();

        continuousActionsOut[0] = normalizedForce.x;
        continuousActionsOut[1] = normalizedForce.y;
        continuousActionsOut[2] = normalizedForce.z;
        continuousActionsOut[3] = inputHandler.CurrentPitch;
        continuousActionsOut[4] = inputHandler.CurrentRoll;
    }
    public void ReachCheckpoint(int checkpointID, float reward)
    {
        // Only reward if it's a new, deeper checkpoint
        if (checkpointID > highestCheckpointReached)
        {
            highestCheckpointReached = checkpointID;
            AddReward(reward);
            
            // Critical: Reset the death timer since the agent made legitimate progress
            stepsSinceProgress = 0; 
            
            Debug.Log($"<color=cyan>Checkpoint {checkpointID} Reached! Reward given.</color>");
        }
    }

    public void SuccessfullyPluggedIn()
    {
        AddReward(10.0f);
        EndEpisode();
    }
}