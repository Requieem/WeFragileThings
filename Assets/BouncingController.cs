using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Addons.Physics;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.InputSystem;

public struct BouncingInput : INetworkInput
{
    public Vector2 Move;
}

public class BouncingController : NetworkBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private InputActionReference m_moveAction;
    [SerializeField] private Rigidbody m_rigidbody;
    [SerializeField] private float moveForce = 10f;
    [SerializeField] private float bounceForce = 5f;
    [SerializeField] private float bounceFrequency = 2f;
    [SerializeField] private float bounceAmplitude = 0.2f;
    
    private Vector2 _currentMoveInput;
    private float _time;
    private bool _isBouncing;

    public override void Spawned()
    {
        if (Object.HasInputAuthority)
        {
            m_moveAction.action.Enable();
            m_moveAction.action.performed += OnMove;
            m_moveAction.action.canceled += OnMove;
        }
        
        var runner = FindAnyObjectByType<NetworkRunner>();
        if (runner != null)
        {
            runner.AddCallbacks(this);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Object.HasInputAuthority)
        {
            m_moveAction.action.performed -= OnMove;
            m_moveAction.action.canceled -= OnMove;
        }
    }

    private void OnMove(InputAction.CallbackContext context)
    {
        _currentMoveInput = context.ReadValue<Vector2>();
        Debug.Log($"Move input: {_currentMoveInput}");
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || m_rigidbody == null) return;

        _time += Runner.DeltaTime * bounceFrequency;
        float scaleY = 1f + Mathf.Sin(_time) * bounceAmplitude;
        transform.localScale = new Vector3(1f, scaleY, 1f);

        bool wasBouncing = _isBouncing;
        _isBouncing = Mathf.Sin(_time) > 0.95f;

        if (_isBouncing && !wasBouncing)
        {
            m_rigidbody.AddForce(Vector3.up * bounceForce, ForceMode.Impulse);
        }

        if (GetInput(out BouncingInput input) && _isBouncing)
        {
            Vector3 forceDirection = new Vector3(input.Move.x, 0, input.Move.y);
            m_rigidbody.AddForce(forceDirection * moveForce, ForceMode.Acceleration);
        }
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        if (!Object.HasInputAuthority) return;

        BouncingInput bounceInput = new BouncingInput
        {
            Move = _currentMoveInput
        };

        input.Set(bounceInput);
    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player){}
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player){}
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player){}
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player){}
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason){}
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason){}
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token){}
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason){}
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message){}
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data){}
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress){}
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input){}
    public void OnConnectedToServer(NetworkRunner runner){}
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList){}
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data){}
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken){}
    public void OnSceneLoadDone(NetworkRunner runner){}
    public void OnSceneLoadStart(NetworkRunner runner){}
}