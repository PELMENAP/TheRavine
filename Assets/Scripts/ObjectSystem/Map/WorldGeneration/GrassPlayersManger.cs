using UnityEngine;

public class GrassPlayersManger : MonoBehaviour
{
    [Header("Player Interaction")]
    [SerializeField] private Transform[] players;
    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private Material grassMaterial;

    private Vector4[] playerPositions;

    void Start()
    {
        playerPositions = new Vector4[maxPlayers];
    }

    void FixedUpdate()
    {
        UpdatePlayerPositions();
    }

    void UpdatePlayerPositions()
    {
        int count = Mathf.Min(players.Length, maxPlayers);
        
        for (int i = 0; i < count; i++)
        {
            if (players[i] != null)
            {
                playerPositions[i] = players[i].position;
            }
            else
            {
                playerPositions[i] = new Vector4(0, -10000, 0, 0);
            }
        }
        
        for (int i = count; i < maxPlayers; i++)
        {
            playerPositions[i] = new Vector4(0, -10000, 0, 0);
        }
        
        grassMaterial.SetInt("_PlayerCount", count);
        grassMaterial.SetVectorArray("_PlayerPositions", playerPositions);
    }
}
