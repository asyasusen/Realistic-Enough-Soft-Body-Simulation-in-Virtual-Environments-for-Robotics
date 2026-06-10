using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

[RequireComponent(typeof(PlugActuator), typeof(PlugInputHandler))]
public class PlugAgent : Agent
{
    [Header("Environment References")]
    [SerializeField] private PlugEnvironmentManager envManager; 
    [SerializeField] private BezierCableGenerator cableGenerator;
    [SerializeField] private Transform socketTransform;
    [SerializeField] private Transform entranceTransform; 
    [SerializeField] private Transform targetHoleTransform;
    
    [Header("Agent Settings")]
    public int maxPatienceSteps = 2000;

    private PlugActuator actuator;
    private PlugInputHandler inputHandler;
    private ArticulationBody plugBody;
    private ArticulationBody clipBody;
    private Transform plugTipTransform;

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
        envManager.ResetEnvironment(); 
        
        FindAndSetupBodies();
        
        if (plugBody != null && clipBody != null)
        {
            actuator.Initialize(plugBody, clipBody);
            inputHandler.Initialize(plugBody, clipBody);
        }

        stepsSinceProgress = 0;
        highestCheckpointReached = -1;
        

        // Frame 1 bugfix
        if (entranceTransform != null)
        {
            Vector3 mathematicalRestPos = cableGenerator.GetPoint(1f);
            closestDistanceReached = Vector3.Distance(mathematicalRestPos, entranceTransform.position);
        }
    }

    private void FindAndSetupBodies()
    {
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
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (plugBody == null || plugTipTransform == null) 
        {
            for (int i = 0; i < 14; i++) sensor.AddObservation(0f);
            return;
        }

        sensor.AddObservation(targetHoleTransform.position - plugTipTransform.position);        
        Quaternion relativeRotation = Quaternion.Inverse(socketTransform.rotation) * plugBody.transform.rotation;
        sensor.AddObservation(relativeRotation);
        sensor.AddObservation(plugBody.linearVelocity);
        sensor.AddObservation(plugBody.angularVelocity);

        bool clipSqueezed = clipBody != null && Mathf.Approximately(clipBody.xDrive.target, clipBody.xDrive.upperLimit);
        sensor.AddObservation(clipSqueezed ? 1.0f : 0.0f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (plugBody == null) return;

        Vector3 actionForce = new Vector3(actions.ContinuousActions[0], actions.ContinuousActions[1], actions.ContinuousActions[2]);
        actuator.ApplyLinearForce(actionForce);

        bool isGripped = actionForce.magnitude > 0.01f || inputHandler.IsDragging;
        if (isGripped) actuator.ApplyRotation(actions.ContinuousActions[3], actions.ContinuousActions[4], actions.ContinuousActions[5]);
        else actuator.SyncTargetRotation();

        actuator.ApplySqueeze(actions.DiscreteActions[0] == 1);
        HandleRewards();
    }

private void HandleRewards()
    {
        float distanceToHole = 999f;
        if (entranceTransform != null) distanceToHole = Vector3.Distance(plugTipTransform.position, entranceTransform.position);

        if (entranceTransform != null && highestCheckpointReached < 0) 
        {           
            if (distanceToHole < closestDistanceReached)
            {
                AddReward((closestDistanceReached - distanceToHole) * 5.0f); 
                closestDistanceReached = distanceToHole; 
                stepsSinceProgress = 0; 
            }
            else stepsSinceProgress++;
        }
        else stepsSinceProgress++;

        // 2. ABSOLUTE PHYSICAL ALIGNMENT (The Fix)
        if (plugBody != null && socketTransform != null && plugTipTransform != null)
        {
            // Vector pointing FROM center TO tip (Forward)
            Vector3 plugForward = (plugTipTransform.position - plugBody.transform.position).normalized;
            // Vector pointing FROM entrance TO center (Into the socket)
            Vector3 holeForward = (targetHoleTransform.position - entranceTransform.position).normalized;
            
            // Vector pointing FROM center TO clip (Up)
            Vector3 plugUp = (clipBody.transform.position - plugBody.transform.position).normalized;
            Vector3 socketUp = socketTransform.up; 

            float forwardAlignment = Vector3.Dot(plugForward, holeForward);
            float upAlignment = Vector3.Dot(plugUp, socketUp);

            if (forwardAlignment > 0.8f && upAlignment > 0.8f)
            {
                AddReward(0.01f); 
            }
            else
            {
                AddReward((forwardAlignment + upAlignment - 2f) * 0.05f);
            }
        }

        if (stepsSinceProgress >= maxPatienceSteps)
        {
            AddReward(-1.0f); 
            EndEpisode();
            return; 
        }

        AddReward(-0.0005f);
    }

    public void ReachCheckpoint(int checkpointID, float reward)
    {
        if (checkpointID > highestCheckpointReached)
        {
            Vector3 plugForward = (plugTipTransform.position - plugBody.transform.position).normalized;
            Vector3 holeForward = (targetHoleTransform.position - entranceTransform.position).normalized;
            
            if (Vector3.Angle(plugForward, holeForward) < 45f) 
            {
                highestCheckpointReached = checkpointID;
                AddReward(reward);
                stepsSinceProgress = 0; 
            }
            else 
            {
                AddReward(-1.0f); 
                EndEpisode();
            }
        }
    }

    public void SuccessfullyPluggedIn()
    {
        Vector3 plugForward = (plugTipTransform.position - plugBody.transform.position).normalized;
        Vector3 holeForward = (targetHoleTransform.position - entranceTransform.position).normalized;
        Vector3 plugUp = (clipBody.transform.position - plugBody.transform.position).normalized;
        Vector3 socketUp = socketTransform.up; 

        float worstAngle = Mathf.Max(Vector3.Angle(plugForward, holeForward), Vector3.Angle(plugUp, socketUp));

        if (worstAngle < 20f) 
        {
            AddReward(10.0f);
            EndEpisode();
        }
        else 
        {
            AddReward(-2.0f); 
            EndEpisode();
        }
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
        continuousActionsOut[4] = inputHandler.CurrentYaw;
        continuousActionsOut[5] = inputHandler.CurrentRoll;
    }


}