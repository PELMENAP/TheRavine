using UnityEngine;

public class GrassPlayersManger : MonoBehaviour
{
    [Header("Player Interaction")]
    [SerializeField] private Material grassMaterial;

    private Transform player = null;
    private Vector3 playerPosition;

    private void FixedUpdate()
    {
        if (player == null) TryBindFirstPlayer();
        UpdatePlayerPositions();
    }

    private void TryBindFirstPlayer()
    {
        var players = ServiceLocator.Players.GetAllPlayers();
        if (players.Count == 0 || players[0].IsDisposed) return;

        TransformComponent transformComponent = players[0].GetEntityComponent<TransformComponent>();
        if (transformComponent == null) return;

        player = transformComponent.GetEntityTransform();
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
