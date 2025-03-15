using Fusion;
using UnityEngine;

public class ActivateOnAuthority : NetworkBehaviour
{
    [SerializeField] private NetworkObject m_associatedObject;
    [SerializeField] private GameObject m_objectToActivate;
    
    public override void Spawned()
    {
        m_objectToActivate.SetActive(m_associatedObject.HasInputAuthority);
    }
}
