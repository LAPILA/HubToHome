using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class VFXAutoDespawn : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float _alivePollInterval = 0.05f;

    private ParticleSystem _mainPS;
    private float _nextAlivePollTime;

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

        _nextAlivePollTime = Time.unscaledTime;
    }

    private void Update()
    {
        if (_mainPS == null || Time.unscaledTime < _nextAlivePollTime)
            return;

        _nextAlivePollTime = Time.unscaledTime + _alivePollInterval;
        if (_mainPS.IsAlive(true))
            return;

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
