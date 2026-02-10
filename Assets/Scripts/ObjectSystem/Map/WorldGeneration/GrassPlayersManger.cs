using UnityEngine;

public class GrassPlayersManger : MonoBehaviour
{
    [Header("Player Interaction")]
    [SerializeField] private Transform player;
    [SerializeField] private Material grassMaterial;

    private Vector4 playerPosition;

    void FixedUpdate()
    {
        UpdatePlayerPositions();
    }

    void UpdatePlayerPositions()
    {
        if (player != null)
        {
            playerPosition = player.position;
        }
        else
        {
            playerPosition = new Vector4(0, -10000, 0, 0);
        }
        
        grassMaterial.SetVector("_PlayerPosition", playerPosition);
    }
}
