using UnityEngine;

public class GrassPlayersManger : MonoBehaviour
{
    [Header("Player Interaction")]
    [SerializeField] private Material grassMaterial;

    private static readonly int PlayerPositionId = Shader.PropertyToID("_PlayerPosition");

    private Transform player = null;
    private Vector3 playerPosition;
    private bool playerPositionApplied;
    private Vector3 appliedPosition;

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
        
        if (playerPositionApplied && playerPosition == appliedPosition) return;

        appliedPosition = playerPosition;
        playerPositionApplied = true;
        grassMaterial.SetVector(PlayerPositionId, playerPosition);
    }
}
