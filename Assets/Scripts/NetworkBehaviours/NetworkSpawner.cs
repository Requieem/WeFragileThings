using System;
using Fusion;
using UnityEngine;
using Fusion.Sockets;
using System.Collections.Generic;
using Random = UnityEngine.Random;

public class NetworkSpawner : SimulationBehaviour, IPlayerJoined, IPlayerLeft
{
    [SerializeField] private NetworkObject m_objectPrefab; // The networked prefab to spawn
    [SerializeField] private bool m_spawnOnPlayerJoin = false; // Should spawn objects on player join?
    [SerializeField] private Transform[] m_spawnPoints; // Predefined spawn locations
    private readonly Dictionary<PlayerRef, NetworkObject> m_spawnedObjects = new Dictionary<PlayerRef, NetworkObject>();

    public void SpawnObject(Vector3 position, Quaternion rotation, PlayerRef player)
    {
        if (Runner == null || m_objectPrefab == null)
        {
            Debug.LogError("Missing NetworkRunner or Object Prefab!");
            return;
        }

        if (Runner.LocalPlayer != player)
        {
            Debug.LogError("Trying to spawn character from wrong client!");
            return;
        }

        // Spawn the object with optional ownership assignment
        NetworkObject spawnedObject = Runner.Spawn(m_objectPrefab, position, rotation, player);

        if (spawnedObject != null)
        {
            spawnedObject.AssignInputAuthority(player); // Assign authority to the player
            m_spawnedObjects[player] = spawnedObject;
        }
        else
        {
            Debug.LogError("Failed to spawn network object!");
        }
    }
    
    public void PlayerJoined(PlayerRef player)
    {
        if (player != Runner.LocalPlayer || !m_spawnOnPlayerJoin) return;

        Vector3 spawnPosition = GetSpawnPosition();
        SpawnObject(spawnPosition, Quaternion.identity, player);
    }

    public void PlayerLeft(PlayerRef player)
    {
        if (!Runner.IsSharedModeMasterClient ||
            !m_spawnedObjects.TryGetValue(player, out NetworkObject playerObject)) return;
        
        Runner.Despawn(playerObject);
        m_spawnedObjects.Remove(player);
        Debug.Log($"Despawned object for player {player.PlayerId}");
    }

    private Vector3 GetSpawnPosition()
    {
        if (m_spawnPoints != null && m_spawnPoints.Length > 0)
        {
            // Choose a random spawn point
            Transform spawnPoint = m_spawnPoints[Random.Range(0, m_spawnPoints.Length)];
            return spawnPoint.position;
        }
        else
        {
            // Fallback: Random position in default area
            return new Vector3(Random.Range(-5f, 5f), 1f, Random.Range(-5f, 5f));
        }
    }

    // Empty callbacks for now, but required for INetworkRunnerCallbacks
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
}