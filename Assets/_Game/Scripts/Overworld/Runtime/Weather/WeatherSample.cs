using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Room-owned, sprite-replaceable 2D weather. It owns only its generated render objects/materials.
/// This component deliberately does not modify cameras, Room contracts, physics or save state.
/// </summary>
[DisallowMultipleComponent]
public sealed class WeatherSample : MonoBehaviour
{
    private const int RainBudget = 512;
    private const int FrameEmissionBudget = 64;
    private static readonly int HoleId = Shader.PropertyToID("_WeatherHole");
    private static readonly int HoleEnabledId = Shader.PropertyToID("_WeatherHoleEnabled");

    [Title("방 날씨 샘플", "방 Prefab 아래 배치합니다. 미연결 Sprite는 임시 도형으로 표시합니다.")]
    [LabelText("영역 중심 (로컬 XY)")]
    [SerializeField] private Vector2 _center;
    [LabelText("영역 크기"), MinValue(.1f)]
    [SerializeField] private Vector2 _size = new Vector2(24f, 14f);
    [LabelText("플레이어 (선택)")]
    [SerializeField] private Transform _target;

    [Title("안개")]
    [LabelText("안개 켜기")]
    [SerializeField] private bool _fogEnabled = true;
    [LabelText("안개 Sprite (교체용)")]
    [SerializeField] private Sprite _fogSprite;
    [LabelText("안개 색상")]
    [SerializeField] private Color _fogColor = new Color(.72f, .79f, .86f, .55f);
    [LabelText("플레이어 투명 반경"), MinValue(.01f)]
    [SerializeField] private float _holeRadius = .8f;
    [LabelText("투명 경계 폭"), MinValue(.01f)]
    [SerializeField] private float _holeFeather = .55f;
    [LabelText("안개 정렬 순서")]
    [SerializeField] private int _fogOrder = 20;
    [LabelText("안개 원본 재질 (공유 원본 수정 안 함)")]
    [SerializeField] private Material _fogMaterial;

    [Title("비")]
    [LabelText("비 켜기")]
    [SerializeField] private bool _rainEnabled = true;
    [LabelText("빗줄기 Sprite (교체용)")]
    [SerializeField] private Sprite _rainSprite;
    [LabelText("물튀김 Sprite (교체용)")]
    [SerializeField] private Sprite _splashSprite;
    [LabelText("초당 빗방울 수"), Range(0f, 500f)]
    [SerializeField] private float _rainRate = 90f;
    [LabelText("낙하 속도 (월드 XY)")]
    [SerializeField] private Vector2 _rainVelocity = new Vector2(-1.8f, -14f);
    [LabelText("낙하 시간"), Range(.05f, 2f)]
    [SerializeField] private float _rainLifetime = .55f;
    [LabelText("빗줄기 폭 / 길이"), MinValue(.001f)]
    [SerializeField] private Vector2 _rainSize = new Vector2(.035f, .35f);
    [LabelText("비 색상")]
    [SerializeField] private Color _rainColor = new Color(.68f, .82f, 1f, .68f);
    [LabelText("물튀김 너비"), MinValue(.001f)]
    [SerializeField] private float _splashSize = .2f;
    [LabelText("물튀김 유지 시간"), Range(.03f, .5f)]
    [SerializeField] private float _splashLifetime = .16f;
    [LabelText("비 정렬 순서 (Foreground)")]
    [SerializeField] private int _rainOrder = 10;
    [LabelText("물튀김 정렬 순서 (Shadow)")]
    [SerializeField] private int _splashOrder = 5;
    [LabelText("비 원본 재질 (공유 원본 수정 안 함)")]
    [SerializeField] private Material _particleMaterial;

    private Transform _resolvedTarget;
    private GameObject _visualRoot;
    private SpriteRenderer _fog;
    private ParticleSystem _rain;
    private ParticleSystem _splash;
    private ParticleSystemRenderer _rainRenderer;
    private ParticleSystemRenderer _splashRenderer;
    private Material _ownedFogMaterial;
    private Material _ownedRainMaterial;
    private Material _ownedSplashMaterial;
    private MaterialPropertyBlock _fogProperties;
    private Texture2D _previewTexture;
    private Texture2D _previewSplashTexture;
    private Sprite _previewSprite;
    private Sprite _previewSplashSprite;
    private float _emissionRemainder;
    private bool _configurationDirty = true;

    public Transform Target => _resolvedTarget;
    public Vector2 AreaSize => _size;
    public bool HasGeneratedVisuals => _visualRoot != null;

    /// <summary>Call when a temporary playable actor takes over. No global polling is performed.</summary>
    public void SetTarget(Transform target)
    {
        _target = target;
        _resolvedTarget = target;
        UpdateFogHole();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        ResolveTargetOnce();
        if (!EnsureVisuals()) return;
        _visualRoot.SetActive(true);
        ApplyConfiguration();
    }

    private void ResolveTargetOnce()
    {
        _resolvedTarget = _target;
        if (_resolvedTarget != null) return;
        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null && players[i].gameObject.scene == gameObject.scene)
            {
                _resolvedTarget = players[i].transform;
                return;
            }
        }
    }

    private bool EnsureVisuals()
    {
        if (_visualRoot != null) return true;
        if (_fogMaterial == null || _particleMaterial == null)
        {
            Debug.LogWarning("[WeatherSample] 샘플의 안개/비 원본 재질을 연결해 주세요.", this);
            return false;
        }

        CreatePreviewSprites();
        _ownedFogMaterial = new Material(_fogMaterial) { name = "Weather Fog (Owned)" };
        _ownedRainMaterial = new Material(_particleMaterial) { name = "Weather Rain (Owned)" };
        _ownedSplashMaterial = new Material(_particleMaterial) { name = "Weather Splash (Owned)" };
        _fogProperties = new MaterialPropertyBlock();
        _visualRoot = new GameObject("Weather Visuals (Runtime)");
        _visualRoot.transform.SetParent(transform, false);

        var fogObject = new GameObject("Fog");
        fogObject.transform.SetParent(_visualRoot.transform, false);
        _fog = fogObject.AddComponent<SpriteRenderer>();
        _fog.sharedMaterial = _ownedFogMaterial;
        _fog.sortingLayerID = SortingLayer.NameToID("Foreground");

        _rain = CreateParticles("Rain", out _rainRenderer);
        _splash = CreateParticles("Landing Splash", out _splashRenderer);
        _splash.transform.SetParent(_rain.transform, false);
        _rainRenderer.sharedMaterial = _ownedRainMaterial;
        _rainRenderer.sortingLayerID = SortingLayer.NameToID("Foreground");
        _splashRenderer.sharedMaterial = _ownedSplashMaterial;
        _splashRenderer.sortingLayerID = SortingLayer.NameToID("Shadow");

        var splashEmission = _splash.emission;
        splashEmission.enabled = true;
        splashEmission.rateOverTime = 0f;
        splashEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)1) });
        var subEmitters = _rain.subEmitters;
        subEmitters.enabled = true;
        subEmitters.AddSubEmitter(_splash, ParticleSystemSubEmitterType.Death, ParticleSystemSubEmitterProperties.InheritNothing);
        return true;
    }

    private ParticleSystem CreateParticles(string objectName, out ParticleSystemRenderer renderer)
    {
        var particleObject = new GameObject(objectName);
        particleObject.transform.SetParent(_visualRoot.transform, false);
        ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.startSpeed = 0f;
        main.gravityModifier = 0f;
        main.maxParticles = RainBudget;
        main.stopAction = ParticleSystemStopAction.None;
        var emission = particles.emission;
        emission.enabled = false;
        var shape = particles.shape;
        shape.enabled = false;
        var collision = particles.collision;
        collision.enabled = false;
        var noise = particles.noise;
        noise.enabled = false;
        renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.World;
        renderer.sortMode = ParticleSystemSortMode.None;
        return particles;
    }

    private void ApplyConfiguration()
    {
        if (_visualRoot == null) return;
        _configurationDirty = false;
        _emissionRemainder = 0f;
        _rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        Sprite fogSprite = _fogSprite != null ? _fogSprite : _previewSprite;
        _fog.sprite = fogSprite;
        Vector3 spriteSize = fogSprite.bounds.size;
        Vector3 scale = new Vector3(_size.x / Mathf.Max(.001f, spriteSize.x), _size.y / Mathf.Max(.001f, spriteSize.y), 1f);
        _fog.transform.localScale = scale;
        _fog.transform.localPosition = new Vector3(_center.x, _center.y, 0f) - Vector3.Scale(fogSprite.bounds.center, scale);
        // SpriteRenderer tint is per-instance data in current URP; use our owned material explicitly.
        _fog.color = Color.white;
        _ownedFogMaterial.SetColor("_Color", _fogColor);
        _fog.sortingOrder = _fogOrder;
        _fog.enabled = _fogEnabled;

        ConfigureParticleSprite(_rain, _ownedRainMaterial, _rainSprite != null ? _rainSprite : _previewSprite);
        ConfigureParticleSprite(_splash, _ownedSplashMaterial, _splashSprite != null ? _splashSprite : _previewSplashSprite);
        var rainMain = _rain.main;
        rainMain.loop = true;
        rainMain.startLifetime = _rainLifetime;
        rainMain.startColor = _rainColor;
        rainMain.startSize3D = true;
        rainMain.startSizeX = _rainSize.x;
        rainMain.startSizeY = _rainSize.y;
        rainMain.startSizeZ = 1f;
        rainMain.startRotation = Mathf.Atan2(-_rainVelocity.x, -_rainVelocity.y);
        var splashMain = _splash.main;
        splashMain.loop = false;
        splashMain.startLifetime = _splashLifetime;
        splashMain.startColor = _rainColor;
        splashMain.startSize3D = true;
        splashMain.startSizeX = _splashSize;
        splashMain.startSizeY = _splashSize * .5f;
        splashMain.startSizeZ = 1f;
        var splashSize = _splash.sizeOverLifetime;
        splashSize.enabled = true;
        splashSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, .35f, 1f, 1f));
        var splashColor = _splash.colorOverLifetime;
        splashColor.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        splashColor.color = fade;
        _rainRenderer.sortingOrder = _rainOrder;
        _splashRenderer.sortingOrder = _splashOrder;
        if (_rainEnabled) _rain.Play(false);
        UpdateFogHole();
    }

    private static void ConfigureParticleSprite(ParticleSystem particles, Material material, Sprite sprite)
    {
        material.mainTexture = sprite.texture;
        var sheet = particles.textureSheetAnimation;
        sheet.enabled = true;
        sheet.mode = ParticleSystemAnimationMode.Sprites;
        while (sheet.spriteCount > 0) sheet.RemoveSprite(0);
        sheet.AddSprite(sprite);
        sheet.frameOverTime = 0f;
        sheet.startFrame = 0f;
        sheet.cycleCount = 1;
    }

    private void Update()
    {
        if (_visualRoot == null) return;
        if (_configurationDirty) ApplyConfiguration();
        if (!_rainEnabled || Time.deltaTime <= 0f) return;
        int count = WeatherSampleMath.AccumulateEmission(ref _emissionRemainder, _rainRate, Time.deltaTime, FrameEmissionBudget);
        for (int i = 0; i < count; i++)
        {
            Vector2 localLanding = _center + new Vector2(Random.Range(-.5f, .5f) * _size.x, Random.Range(-.5f, .5f) * _size.y);
            Vector3 landing = transform.TransformPoint(new Vector3(localLanding.x, localLanding.y, 0f));
            Vector2 spawn = WeatherSampleMath.CalculateSpawn(new Vector2(landing.x, landing.y), _rainVelocity, _rainLifetime);
            var emission = new ParticleSystem.EmitParams
            {
                position = new Vector3(spawn.x, spawn.y, landing.z),
                velocity = new Vector3(_rainVelocity.x, _rainVelocity.y, 0f),
                startLifetime = _rainLifetime
            };
            _rain.Emit(emission, 1);
        }
    }

    private void LateUpdate()
    {
        UpdateFogHole();
    }

    private void UpdateFogHole()
    {
        if (_fog == null || _fogProperties == null) return;
        bool hasHole = false;
        Vector3 position = Vector3.zero;
        if (_resolvedTarget != null && _resolvedTarget.gameObject.activeInHierarchy)
        {
            position = _resolvedTarget.position;
            Vector3 local = transform.InverseTransformPoint(position);
            hasHole = WeatherSampleMath.Contains(new Vector2(local.x, local.y), _center, _size);
        }
        _fogProperties.SetVector(HoleId, new Vector4(position.x, position.y, _holeRadius, _holeFeather));
        _fogProperties.SetFloat(HoleEnabledId, hasHole ? 1f : 0f);
        _fog.SetPropertyBlock(_fogProperties);
    }

    private void OnDisable()
    {
        _resolvedTarget = null;
        _emissionRemainder = 0f;
        if (_rain != null) _rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (_visualRoot != null) _visualRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        Release(_visualRoot);
        Release(_ownedFogMaterial);
        Release(_ownedRainMaterial);
        Release(_ownedSplashMaterial);
        Release(_previewSprite);
        Release(_previewSplashSprite);
        Release(_previewTexture);
        Release(_previewSplashTexture);
    }

    private static void Release(Object owned)
    {
        if (owned == null) return;
        if (Application.isPlaying) Destroy(owned);
        else DestroyImmediate(owned);
    }

    private void CreatePreviewSprites()
    {
        _previewTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "Weather Preview White", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        _previewTexture.SetPixel(0, 0, Color.white);
        _previewTexture.Apply(false, true);
        _previewSprite = Sprite.Create(_previewTexture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1f, 0, SpriteMeshType.FullRect);
        _previewSprite.name = "Weather Preview (Replace Sprite)";
        _previewSplashTexture = new Texture2D(16, 16, TextureFormat.RGBA32, false) { name = "Weather Preview Ring", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color[256];
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                float distance = new Vector2(x - 7.5f, y - 7.5f).magnitude;
                pixels[y * 16 + x] = distance > 4.5f && distance < 6.5f ? Color.white : Color.clear;
            }
        }
        _previewSplashTexture.SetPixels(pixels);
        _previewSplashTexture.Apply(false, true);
        _previewSplashSprite = Sprite.Create(_previewSplashTexture, new Rect(0, 0, 16, 16), new Vector2(.5f, .5f), 16f, 0, SpriteMeshType.FullRect);
        _previewSplashSprite.name = "Weather Splash Preview (Replace Sprite)";
    }

    private void OnValidate()
    {
        _size.x = Mathf.Max(.1f, FiniteOr(_size.x, 24f));
        _size.y = Mathf.Max(.1f, FiniteOr(_size.y, 14f));
        _center.x = FiniteOr(_center.x, 0f);
        _center.y = FiniteOr(_center.y, 0f);
        _holeRadius = Mathf.Max(.01f, FiniteOr(_holeRadius, .8f));
        _holeFeather = Mathf.Max(.01f, FiniteOr(_holeFeather, .55f));
        _rainRate = Mathf.Clamp(FiniteOr(_rainRate, 90f), 0f, 500f);
        _rainLifetime = Mathf.Clamp(FiniteOr(_rainLifetime, .55f), .05f, 2f);
        _rainVelocity.x = FiniteOr(_rainVelocity.x, -1.8f);
        _rainVelocity.y = Mathf.Min(-.01f, FiniteOr(_rainVelocity.y, -14f));
        _rainSize.x = Mathf.Max(.001f, FiniteOr(_rainSize.x, .035f));
        _rainSize.y = Mathf.Max(.001f, FiniteOr(_rainSize.y, .35f));
        _splashSize = Mathf.Max(.001f, FiniteOr(_splashSize, .2f));
        _splashLifetime = Mathf.Clamp(FiniteOr(_splashLifetime, .16f), .03f, .5f);
        _configurationDirty = true;
    }

    private static float FiniteOr(float value, float fallback)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(.55f, .78f, 1f, .8f);
        Gizmos.DrawWireCube(new Vector3(_center.x, _center.y, 0f), new Vector3(_size.x, _size.y, 0f));
        Gizmos.matrix = Matrix4x4.identity;
        if (_resolvedTarget != null) Gizmos.DrawWireSphere(_resolvedTarget.position, _holeRadius);
    }
}
