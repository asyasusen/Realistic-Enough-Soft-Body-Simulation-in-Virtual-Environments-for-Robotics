using UnityEngine;

public class PlugActuator : MonoBehaviour
{
    [Header("Actuator Settings")]
    public float maxPullForce = 300f;    
    public float rotationSpeed = 150f;         
    public float rotationalPullForce = 5000f;  
    public float maxTorque = 5000f; 

    private ArticulationBody plugBody;
    private ArticulationBody clipBody;
    private float originalClipTarget;
    private Quaternion targetRotation;

    public void Initialize(ArticulationBody plug, ArticulationBody clip)
    {
        plugBody = plug;
        clipBody = clip;
        if (clipBody != null)
        {
            originalClipTarget = clipBody.xDrive.target;
        }
        
        plugBody.linearVelocity = Vector3.zero;
        plugBody.angularVelocity = Vector3.zero;
        targetRotation = plugBody.transform.rotation;
    }

    public void ApplyLinearForce(Vector3 localNormalizedForce)
    {
        if (plugBody == null) return;
        
        Vector3 worldForce = plugBody.transform.TransformDirection(localNormalizedForce);
        plugBody.AddForce(worldForce * maxPullForce, ForceMode.Acceleration);
    }

    public void SyncTargetRotation()
    {
        if (plugBody != null) targetRotation = plugBody.transform.rotation;
    }

    public void ApplySqueeze(bool squeeze)
    {
        if (clipBody == null) return;
        
        float squeezedTarget = clipBody.xDrive.upperLimit - 1f; 
        float desiredTarget = squeeze ? squeezedTarget : originalClipTarget;
        
        if (!Mathf.Approximately(clipBody.xDrive.target, desiredTarget))
        {
            ArticulationDrive drive = clipBody.xDrive;
            drive.target = desiredTarget;
            clipBody.xDrive = drive;
        }
    }
    public void ApplyRotation(float pitchInput, float yawInput, float rollInput)
    {
        if (plugBody == null) return;

        Quaternion pitchDelta = Quaternion.AngleAxis(pitchInput * rotationSpeed * Time.fixedDeltaTime, plugBody.transform.right);
        Quaternion yawDelta   = Quaternion.AngleAxis(yawInput * rotationSpeed * Time.fixedDeltaTime, plugBody.transform.up);
        Quaternion rollDelta  = Quaternion.AngleAxis(rollInput * rotationSpeed * Time.fixedDeltaTime, plugBody.transform.forward);
        
        targetRotation = pitchDelta * yawDelta * rollDelta * targetRotation;

        Quaternion deltaRot = targetRotation * Quaternion.Inverse(plugBody.transform.rotation);
        deltaRot.ToAngleAxis(out float angle, out Vector3 axis);

        if (!float.IsInfinity(axis.x) && !float.IsNaN(axis.x))
        {
            if (angle > 180f) angle -= 360f;
            angle = angle * Mathf.Deg2Rad; 

            Vector3 appliedTorque = axis.normalized * (angle * rotationalPullForce);
            if (appliedTorque.magnitude > maxTorque) appliedTorque = appliedTorque.normalized * maxTorque;

            plugBody.AddTorque(appliedTorque, ForceMode.Acceleration);
        }
    }
}