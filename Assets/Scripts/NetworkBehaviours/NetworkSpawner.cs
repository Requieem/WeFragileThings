using System;
using Fusion;
using UnityEngine;
using Fusion.Sockets;
using System.Collections.Generic;
using Random = UnityEngine.Random;

public class NetworkSpawner : SimulationBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkObject m_objectPrefab; // The networked prefab to spawn
    [SerializeField] private NetworkRunner m_runner; // Reference to the NetworkRunner
    [SerializeField] private bool m_spawnOnPlayerJoin = false; // Should spawn objects on player join?
    [SerializeField] private Transform[] m_spawnPoints; // Predefined spawn locations
    private readonly Dictionary<PlayerRef, NetworkObject> m_spawnedObjects = new Dictionary<PlayerRef, NetworkObject>();

    private void Awake()
    {
        if (m_runner == null)
        {
            m_runner = FindAnyObjectByType<NetworkRunner>(); // Auto-assign if not set
        }

        if (m_runner == null)
        {
            Debug.LogError("Missing NetworkRunner!");
            return;
        }

        // Register this class for network event callbacks
        m_runner.AddCallbacks(this);
    }

    private void OnDestroy()
    {
        if (m_runner != null)
        {
            m_runner.RemoveCallbacks(this); // Prevent memory leaks
        }
    }

    public void SpawnObject(Vector3 position, Quaternion rotation, PlayerRef? player = null)
    {
        if (m_runner == null || m_objectPrefab == null)
        {
            Debug.LogError("Missing NetworkRunner or Object Prefab!");
            return;
        }

        if (!m_runner.IsSharedModeMasterClient)
        {
            Debug.LogError("Trying to spawn in non-Shared mode!");
            return;
        }
        
        if (!player.HasValue)
        {
            Debug.LogError("Trying to spawn object without player reference in Shared mode!");
            return;
        }

        // Spawn the object with optional ownership assignment
        NetworkObject spawnedObject = m_runner.Spawn(m_objectPrefab, position, rotation, player);

        if (spawnedObject != null)
        {
            Debug.Log($"Spawned network object at {position} for player {player?.PlayerId}");

            // If the object was spawned for a player, track it
            if (player.HasValue)
            {
                m_spawnedObjects[player.Value] = spawnedObject;
            }
        }
        else
        {
            Debug.LogError("Failed to spawn network object!");
        }
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsSharedModeMasterClient)
            return;

        if (m_spawnOnPlayerJoin)
        {
            Vector3 spawnPosition = GetSpawnPosition();
            SpawnObject(spawnPosition, Quaternion.identity, player);
            Debug.Log($"Spawned object for player {player.PlayerId}");
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (m_spawnedObjects.TryGetValue(player, out NetworkObject playerObject))
        {
            runner.Despawn(playerObject);
            m_spawnedObjects.Remove(player);
            Debug.Log($"Despawned object for player {player.PlayerId}");
        }
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