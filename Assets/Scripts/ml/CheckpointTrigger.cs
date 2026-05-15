using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CheckpointTrigger : MonoBehaviour
{
    [Tooltip("The exact name of the object that triggers success")]
    public string targetPlugName = "Rj45"; 
    
    [Tooltip("Reference to the Agent controlling the plug")]
    public PlugAgent plugAgent;

    [Header("Checkpoint Settings")]
    [Tooltip("Order matters. Entrance = 0, Middle = 1, etc.")]
    public int checkpointID = 0;
    
    [Tooltip("How much reward to give when passing this checkpoint for the first time")]
    public float rewardAmount = 1.0f;

    private void OnTriggerEnter(Collider other)
    {
        if (other.attachedArticulationBody != null)
        {
            if (other.attachedArticulationBody.gameObject.name == targetPlugName)
            {
                if (plugAgent != null)
                {
                    // Pass the ID to the agent so it knows if it's new progress
                    plugAgent.ReachCheckpoint(checkpointID, rewardAmount);
                }
            }
        }
    }
}