using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.InputSystem;

public struct MovementInput : INetworkInput
{
    public Vector2 Move;
    public Vector3 Eulers;
    public float Squeeze;
    public NetworkBool Jump;
    public NetworkBool Dash;
    public NetworkBool Slam;
}

public class RollingController : NetworkBehaviour, INetworkRunnerCallbacks
{
    [Header("Movement Parameters")]
    [SerializeField] private float m_moveForce = 10f;
    [SerializeField] private float m_lookSmoothing = 0.9f;
    
    [Header("Dash Parameters")]
    [SerializeField] private float m_maxDashForce = 50f;
    [SerializeField] private float m_maxDashTime = 2f;
    
    [Header("Jump Parameters")]
    [SerializeField] private float m_jumpForce = 10f;
    [SerializeField] private float m_maxJumpTime = 1f;
    [SerializeField] private float m_maxJumpSqueeze = 0.25f;
    [SerializeField] private float m_jumpCooldown = 0.15f;
    
    [Header("Slam Parameters")]
    [SerializeField] private float m_slamForce = 10f;
    [SerializeField] private float m_slamPrepTime = 0.5f;
    [SerializeField] private float m_slamRadius = 0.5f;
    [SerializeField] private float m_slamPushForce = 10f;
    [SerializeField] private float m_upwardsModifier = 1;
    
    [Header("Environment Checks")]
    [SerializeField] private float m_groundedThreshold = 0.15f;

    [Header("Components")]
    [SerializeField] private Rigidbody m_rigidbody;
    [SerializeField] private SphereCollider m_collider;
    [SerializeField] private Transform m_scaledTransform;
    
    [Header("Input Actions")]
    [SerializeField] private InputActionReference m_moveAction;
    [SerializeField] private InputActionReference m_jumpStartAction;
    [SerializeField] private InputActionReference m_jumpEndAction;
    [SerializeField] private InputActionReference m_slamAction;
    [SerializeField] private InputActionReference m_dashStartAction;
    [SerializeField] private InputActionReference m_dashEndAction;

    private bool m_jumped;                                          // Whether the player has jumped recently
    private bool m_jumpStarted;                                     // Whether the player has started jumping
    private float m_jumpTime;                                       // The current charge time for the jump
    private NetworkBool m_jump;                                     // Whether the player wants to jump
    
    private bool m_dashStarted;                                     // Whether the player has started dashing
    private float m_dashTime;                                       // The current charge time for the dash
    private NetworkBool m_dash;                                     // Whether the player wants to dash
    
    private bool m_isSlamFall;                                      // Whether the player is currently slamming downwards
    private bool m_slamming;                                        // Whether the player is currently executing a slam
    private NetworkBool m_slam;                                     // Whether the player wants to slam

    private Vector3 m_lastDirection;
    private Vector3 m_eulers;
    private float m_squeeze = 1f;                                   // The current squeeze factor for the player, this scales the player vertically
    private Vector2 m_currentMoveInput;                             // The current direction the player wants to move in
    private readonly RaycastHit[] m_hitResults = new RaycastHit[5]; // The results of the ground check
    private readonly Collider[] m_slamHits = new Collider[20];      // The results of the slam check

    #region NetworkBehaviour Callbacks

    public override void Spawned()
    {
        if (Object.HasInputAuthority)
        {
            m_moveAction.action.performed += OnMove;
            m_moveAction.action.canceled += OnMove;
            m_jumpStartAction.action.started += OnJumpStart;
            m_jumpEndAction.action.performed += OnJumpEnd;
            m_slamAction.action.performed += OnSlam;
            m_dashStartAction.action.started += OnDashStart;
            m_dashEndAction.action.performed += OnDashEnd;
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
            m_jumpStartAction.action.started -= OnJumpStart;
            m_jumpEndAction.action.performed -= OnJumpEnd;
            m_slamAction.action.performed -= OnSlam;
            m_dashStartAction.action.started -= OnDashStart;
            m_dashEndAction.action.performed -= OnDashEnd;
        }
    }
    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        if (!Object.HasInputAuthority) return;

        MovementInput movementInput = new MovementInput
        {
            Move = m_currentMoveInput,
            Jump = m_jump,
            Slam = m_slam,
            Dash = m_dash,
            Squeeze = m_squeeze,
            Eulers = m_eulers,
        };
        
        m_dash = false;
        m_slam = false;
        m_jump = false;
        input.Set(movementInput);
    }   
    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || m_rigidbody == null) return;

        if (GetInput(out MovementInput input))
        {
            transform.eulerAngles = input.Eulers;
            ScaleObject(input.Squeeze);
            Vector3 forceDirection = new Vector3(input.Move.x, 0, input.Move.y);
            if(m_lastDirection != forceDirection && forceDirection != Vector3.zero)
            {
                m_lastDirection = forceDirection;
            }
            m_rigidbody.AddForce(forceDirection * m_moveForce, ForceMode.Acceleration);
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(forceDirection), m_lookSmoothing);

            if (input.Dash)
            {
                m_rigidbody.AddForce(forceDirection * (m_maxDashForce * m_dashTime / m_maxDashTime), ForceMode.VelocityChange);
                m_dashTime = 0;
            }

            var aerialAction = input.Jump || input.Slam;
            
            if (aerialAction)
            {
                var grounded = IsGrounded();
                if (input.Jump && grounded)
                {
                    Debug.Log("Jumping");
                    m_rigidbody.AddForce(Vector3.up * (m_jumpForce * m_jumpTime / m_maxJumpTime), ForceMode.Impulse);
                    m_jumpTime = 0;
                }
                
                if (input.Slam && !grounded)
                {
                    Debug.Log("Slamming");
                    StartCoroutine(nameof(Slam));
                }
            }
            
        }
    }
    
    #endregion

    #region Move

    private void OnMove(InputAction.CallbackContext context)
    {
        if (m_slamming)
        {
            m_currentMoveInput = Vector2.zero;
            return;
        }
        
        m_currentMoveInput = context.ReadValue<Vector2>();
        var direction = new Vector3(m_currentMoveInput.x, 0, m_currentMoveInput.y);
        if (m_currentMoveInput != Vector2.zero && direction != m_lastDirection)
        {
            m_lastDirection = direction;
            StopCoroutine(nameof(RotateTowards));
            StartCoroutine(nameof(RotateTowards), direction);
        }
    }
    private IEnumerator RotateTowards(Vector3 target)
    {
        var initialRotation = transform.rotation;
        var time = 0f;
        while (time < m_lookSmoothing)
        {
            time += Time.deltaTime;
            transform.rotation = Quaternion.Lerp(initialRotation, Quaternion.LookRotation(target), time / m_lookSmoothing);
            m_eulers = transform.eulerAngles;
            yield return null;
        }
    }
    
    #endregion

    #region Jump

    private void OnJumpStart(InputAction.CallbackContext context)
    {
        if(!context.started || m_jumped || m_slamming || !IsGrounded()) return;
        Debug.Log("Jump started");
        StartCoroutine(nameof(ChargeJump));
    }
    private void OnJumpEnd(InputAction.CallbackContext context)
    {
        if(!context.performed || !IsGrounded()) return;
        Jump();
    }
    private IEnumerator ChargeJump()
    {
        m_jumpStarted = true;
        m_jumpTime = 0; // Reset jump time before charging
        m_jump = false;
        m_jumped = false;
        m_slam = false;
        m_slamming = false;

        while (m_jumpTime < m_maxJumpTime)
        {
            m_jumpTime += Time.deltaTime;

            // Compute the squeeze factor (world-space Y-axis scaling)
            m_squeeze = Mathf.Lerp(1, m_maxJumpSqueeze, m_jumpTime / m_maxJumpTime);

            // Debug log with current jump time and squeeze factor and collider radius
            Debug.Log($"Jump time: {m_jumpTime}");
            yield return null; // More efficient than WaitForSeconds(Runner.DeltaTime)
        }

        m_jumpTime = m_maxJumpTime;
        Jump();
    }
    private IEnumerator CoolJump()
    {
        var time = 0f;
        var initialScale = transform.localScale;
        
        while (time < m_jumpCooldown)
        {
            time += Time.deltaTime;
            // Compute the squeeze factor (world-space Y-axis scaling)
            m_squeeze = Mathf.Lerp(initialScale.y, 1, time / m_jumpCooldown);
            // Debug log with current jump time and squeeze factor and collider radius
            Debug.Log($"Jump time: {m_jumpTime}");
            yield return null; // More efficient than WaitForSeconds(Runner.DeltaTime)
        }
        
        m_jumped = false;
    }
    private void Jump()
    {
        if(!m_jumpStarted)
            m_jumpStarted = false;
        if (m_slamming || m_jump || m_jumped) return;
        StopCoroutine(nameof(ChargeJump));
        m_jumped = true;
        m_jump = true;
        m_slam = false;
        Debug.Log("Jump ended");
        StartCoroutine(nameof(CoolJump));
    }

    #endregion

    #region Dash

    private void OnDashStart(InputAction.CallbackContext context)
    {
        if(!context.started) return;
        Debug.Log("Dash started");
        StartCoroutine(nameof(ChargeDash));
    }
    private void OnDashEnd(InputAction.CallbackContext context)
    {
        if(!context.performed) return;
        Dash();
    }
    private void Dash()
    {
        if(!m_dashStarted) return;
        m_dashStarted = false;
        StopCoroutine(nameof(ChargeDash));
        m_dash = true;
        Debug.Log("Dash ended");
    }
    private IEnumerator ChargeDash()
    {
        m_dashStarted = true;
        m_dashTime = 0;
        while (m_dashTime < m_maxDashTime)
        {
            m_dashTime += Time.deltaTime;
            yield return null;
        }
        
        m_dashTime = m_maxDashTime;
        Dash();
    }

    #endregion

    #region Slam

    private void OnSlam(InputAction.CallbackContext context)
    {
        if(!context.performed || IsGrounded()) return;
        m_slam = true;
    }
    private IEnumerator Slam()
    {
        m_slamming = true;
        var time = 0f;
        var initialScale = m_squeeze;
        var initialVelocity = m_rigidbody.linearVelocity;
        while (time < m_slamPrepTime)
        {
            time += Time.deltaTime;
            var t = time / m_slamPrepTime;
            m_squeeze = Mathf.Lerp(initialScale, 1, t);
            m_rigidbody.linearVelocity = Vector3.Lerp(initialVelocity, Vector3.zero, t);
            yield return null;
        }
        
        m_rigidbody.AddForce(Vector3.down * m_slamForce, ForceMode.Impulse);
        m_isSlamFall = true;
        m_slamming = false;
    }

    #endregion

    #region Environment Checks

    private bool IsGrounded()
    {
        var grounded = false;
        var checkRadius = m_collider.radius;
            
        Debug.Log("Aerial action detected");
        var hitsCount = Physics.SphereCastNonAlloc(new Ray(transform.position + Vector3.up * checkRadius, Vector3.down), checkRadius, m_hitResults,
            m_groundedThreshold + checkRadius);
        Debug.Log($"Hits count: {hitsCount}");
        for (var i = 0; i < hitsCount; i++)
        {
            Debug.Log($"Hit {i}: {m_hitResults[i].collider}");
            if (m_hitResults[i].collider != null && m_hitResults[i].collider != m_collider)
            {
                grounded = true;
                break;
            }
        }
        
        return grounded;
    }

    #endregion

    #region Collider Callbacks
    
    private void OnCollisionEnter(Collision other)
    {
        if(!m_isSlamFall) return;
        var hits = Physics.OverlapSphereNonAlloc(transform.position, m_slamRadius, m_slamHits);
        for(var i = 0; i < hits; i++)
        {
            // ignore this object
            if(m_slamHits[i] == m_collider) continue;
            var hit = m_slamHits[i];
            var rollingController = hit.gameObject.GetComponent<RollingController>();
            if(rollingController != null)
            {
                rollingController.GetPushed_Rpc(m_slamPushForce);
            }
        }
    }

    #endregion

    #region Scale Utility
    
    void ScaleObject(float squeeze)
    {
        Transform t = transform;

        // Get world rotation matrix
        Matrix4x4 rotationMatrix = Matrix4x4.Rotate(t.rotation);

        // Transform the axis into local space
        Vector3 localAxis = t.InverseTransformDirection(Vector3.up);

        // Compute the outer product
        Matrix4x4 outer = OuterProduct(localAxis);

        // Scale the outer product matrix manually
        Matrix4x4 scaleMatrix = Matrix4x4.identity;
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                scaleMatrix[row, col] += (squeeze - 1) * outer[row, col];
            }
        }

        // Apply scale in local space
        Matrix4x4 finalTransform = rotationMatrix * scaleMatrix;

        // Extract new scale and apply it
        m_scaledTransform.localScale = ExtractScale(finalTransform);
    }
    Matrix4x4 OuterProduct(Vector3 v)
    {
        return new Matrix4x4(
            new Vector4(v.x * v.x, v.x * v.y, v.x * v.z, 0),
            new Vector4(v.y * v.x, v.y * v.y, v.y * v.z, 0),
            new Vector4(v.z * v.x, v.z * v.y, v.z * v.z, 0),
            new Vector4(0, 0, 0, 1)
        );
    }
    Vector3 ExtractScale(Matrix4x4 matrix)
    {
        return new Vector3(
            matrix.GetColumn(0).magnitude,
            matrix.GetColumn(1).magnitude,
            matrix.GetColumn(2).magnitude
        );
    }
    
    #endregion

    #region Remote Procedure Calls
    
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void GetPushed_Rpc(float force)
    {
        m_rigidbody.AddExplosionForce(force, transform.position, m_slamRadius, m_upwardsModifier, ForceMode.Impulse);
    }
    
    #endregion

    
    
    
    
    
    
    
    
    
    
    
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