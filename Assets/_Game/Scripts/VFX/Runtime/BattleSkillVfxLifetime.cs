using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>공격 시작 VFX의 짧은 수명과 취소만 소유합니다. 풀 재사용 뒤 이전 소유자는 반환하지 못합니다.</summary>
[DisallowMultipleComponent]
public sealed class BattleSkillVfxLifetime : MonoBehaviour
{
    private uint _generation;
    private Coroutine _routine;
    private ParticleSystem[] _particles;
    private bool[] _wasPlaying;
    private bool _paused;

    public readonly struct Lease : IDisposable
    {
        private readonly BattleSkillVfxLifetime _owner;
        private readonly uint _generation;
        internal Lease(BattleSkillVfxLifetime owner, uint generation) { _owner = owner; _generation = generation; }
        public void Dispose() { if (_owner != null) _owner.Cancel(_generation); }
    }

    public static Lease Spawn(GameObject prefab, Vector3 position, float scale, float lifetime, Func<bool> isActive)
    {
        if (prefab == null || !Finite(scale) || scale <= 0f || !Finite(lifetime) || lifetime <= 0f) return default;
        GameObject instance = ObjectPoolManager.Instance != null
            ? ObjectPoolManager.Instance.Spawn(prefab, position, Quaternion.identity)
            : Instantiate(prefab, position, Quaternion.identity);
        if (instance == null) return default;
        instance.transform.localScale = prefab.transform.localScale * Mathf.Clamp(scale, .01f, 8f);
        CharacterVFX.ApplyRuntimeAudioNormalization(instance);
        var owner = instance.GetComponent<BattleSkillVfxLifetime>();
        if (owner == null) owner = instance.AddComponent<BattleSkillVfxLifetime>();
        return owner.Begin(Mathf.Clamp(lifetime, .05f, 10f), isActive);
    }

    private Lease Begin(float lifetime, Func<bool> isActive)
    {
        Invalidate();
        uint generation = _generation;
        int scene = SceneManager.GetActiveScene().handle;
        _particles = GetComponentsInChildren<ParticleSystem>(true);
        _wasPlaying = new bool[_particles.Length];
        _routine = StartCoroutine(Play(generation, scene, lifetime, isActive));
        return new Lease(this, generation);
    }

    private IEnumerator Play(uint generation, int scene, float lifetime, Func<bool> isActive)
    {
        yield return null; // 동기 반환/OnDisable이 이전 coroutine 핸들을 새 소유자에 남기지 않습니다.
        float elapsed = 0f;
        try
        {
            while (_generation == generation && gameObject.activeInHierarchy && elapsed < lifetime)
            {
                if (SceneManager.GetActiveScene().handle != scene || (isActive != null && !isActive())) yield break;
                SetPaused(BattleDelay.IsPaused);
                if (!_paused) elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        finally
        {
            if (this != null && _generation == generation)
            {
                _routine = null;
                ReturnToPool();
            }
        }
    }

    private void SetPaused(bool paused)
    {
        if (_paused == paused) return;
        _paused = paused;
        for (int i = 0; _particles != null && i < _particles.Length; i++)
        {
            ParticleSystem particle = _particles[i];
            if (particle == null) continue;
            if (paused)
            {
                _wasPlaying[i] = particle.isPlaying;
                if (_wasPlaying[i]) particle.Pause(false);
            }
            else if (_wasPlaying[i] && particle.isPaused) particle.Play(false);
        }
    }

    private void Cancel(uint generation)
    {
        if (_generation == generation && gameObject.activeInHierarchy) ReturnToPool();
    }

    private void ReturnToPool()
    {
        Invalidate();
        if (ObjectPoolManager.Instance != null) ObjectPoolManager.Instance.Despawn(gameObject);
        else Destroy(gameObject);
    }

    private void Invalidate()
    {
        unchecked { if (++_generation == 0) _generation++; }
        Coroutine routine = _routine;
        _routine = null;
        if (routine != null) StopCoroutine(routine);
        _paused = false;
    }

    private void OnDisable() => Invalidate();
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
