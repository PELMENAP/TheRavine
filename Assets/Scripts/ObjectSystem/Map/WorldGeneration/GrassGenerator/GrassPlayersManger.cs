using UnityEngine;

public class GrassPlayersManger : MonoBehaviour
{
    [Header("Player Interaction")]
    [SerializeField] private Transform player;
    [SerializeField] private Material grassMaterial;

    private Vector3 playerPosition;

    private void FixedUpdate()
    {
        UpdatePlayerPositions();
    }

    private void UpdatePlayerPositions()
    {
        if (player != null)
        {
            playerPosition = player.position;
        }
        else
        {
            playerPosition = new Vector3(0, -10000, 0);
        }
        
        grassMaterial.SetVector("_PlayerPosition", playerPosition);
    }
}
