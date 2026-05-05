using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class VFXAutoDespawn : MonoBehaviour
{
    private ParticleSystem _mainPS;

    private void Awake()
    {
        _mainPS = GetComponent<ParticleSystem>();
    }

    private void OnEnable()
    {
        if (_mainPS != null)
        {
            _mainPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); 
            _mainPS.Play(true); 
        }
    }

    private void Update()
    {
        if (_mainPS != null && !_mainPS.IsAlive(true))
        {
            transform.SetParent(null); 

            if (ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.Despawn(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}