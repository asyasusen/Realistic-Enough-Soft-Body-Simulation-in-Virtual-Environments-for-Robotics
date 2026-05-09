using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SocketTrigger : MonoBehaviour
{
    [Tooltip("The exact name of the object that triggers success")]
    public string targetPlugName = "Rj45"; 

    [Tooltip("Reference to the Agent controlling the plug")]
    public PlugAgent plugAgent;

    private void OnTriggerEnter(Collider other)
    {
        if (other.attachedArticulationBody != null)
        {
            if (other.attachedArticulationBody.gameObject.name == targetPlugName)
            {
                Debug.Log("<color=green><b>SUCCESS: Plug successfully inserted into the socket!</b></color>");
                
                if (plugAgent != null)
                {
                    plugAgent.SuccessfullyPluggedIn();
                }
                else
                {
                    Debug.LogWarning("SocketTrigger triggered, but no PlugAgent is assigned!");
                }
            }
        }
    }
}
