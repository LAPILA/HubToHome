using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>재사용 사슬 UI4개와 시작음1개만 소유합니다. 플래그·전투·BGM은 변경하지 않습니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup))]
public sealed class BattlePhaseOverlayPresentation : MonoBehaviour
{
    public const int CanvasOrder = -200;
    private static readonly int ScrollId = Shader.PropertyToID("_Scroll01");
    private static readonly int SpriteRectId = Shader.PropertyToID("_SpriteUvRect");
    private readonly BattlePhaseOverlayPlayback _clock = new();
    private readonly RawImage[] _ribbons = new RawImage[4];
    private BattlePhaseOverlayProfile _profile;
    private Canvas _canvas;
    private CanvasGroup _group;
    private FixedCanvasViewport _viewport;
    private UIViewportService _viewportService;
    private RectTransform _stage;
    private Material _material;
    private AudioSource _audioSource;
    private AudioClip _clip;
    private Coroutine _running;
    private bool _audioPaused;
    public bool IsShowing => _clock.IsActive;

    public bool Prepare(BattlePhaseOverlayProfile profile)
    {
        Hide();
        _profile = profile;
        if (profile == null || !profile.HasValidSettings) return false;
        AudioClip clip = profile.StartVolume > 0f ? profile.StartSfx : null;
        // 음원 importer의 background loading을 사용하며 완료를 기다리거나 worker에서 Unity API를 호출하지 않습니다.
        if (clip != null && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
        return true;
    }

    public bool Show()
    {
        Hide();
        if (!isActiveAndEnabled || _profile == null || !_profile.HasValidSettings) return false;
        Sprite sprite = _profile.ChainSprite;
        // 회전/타이트 패킹은 잘못된 UV를 추측하지 않고 명확히 시작을 거절합니다.
        if (sprite.packed && (sprite.packingMode == SpritePackingMode.Tight || sprite.packingRotation != SpritePackingRotation.None)) return false;
        Texture2D texture = sprite.texture;
        Rect textureRect = sprite.textureRect;
        if (texture == null || texture.width <= 0 || texture.height <= 0 || textureRect.width <= 0 || textureRect.height <= 0) return false;
        if (!EnsureView()) return false;

        Rect playfield = BattlePhaseOverlayPolicy.AreaFor(_profile.Placement);
        _stage.sizeDelta = playfield.size;
        _stage.anchoredPosition = playfield.center - new Vector2(320f, 240f);
        _material.SetVector(SpriteRectId, new Vector4(textureRect.x / texture.width, textureRect.y / texture.height,
            textureRect.width / texture.width, textureRect.height / texture.height));
        _material.SetFloat(ScrollId, 0f);
        float width = Mathf.Round(_profile.WidthPixels), length = Mathf.Round(_profile.LengthPixels);
        float period = BattlePhaseOverlayPolicy.RepeatPeriod(textureRect.height, _profile.SpriteScale);
        for (int i = 0; i < _ribbons.Length; i++)
        {
            RawImage ribbon = _ribbons[i];
            ribbon.texture = texture;
            ribbon.material = _material;
            ribbon.color = _profile.Tint;
            ribbon.uvRect = new Rect(0f, 0f, 1f, length / period);
            ribbon.rectTransform.sizeDelta = new Vector2(width, length);
            ribbon.rectTransform.anchoredPosition = BattlePhaseOverlayPolicy.CornerCenter(i, _profile.InsetPixels, playfield.size);
            ribbon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, BattlePhaseOverlayPolicy.CornerAngle(i, _profile.Placement));
            ribbon.rectTransform.localScale = BattlePhaseOverlayPolicy.CornerScale(_profile.GetCornerMirror(i));
        }
        _clip = _profile.StartVolume > 0f ? _profile.StartSfx : null;
        _canvas.enabled = true;
        _group.alpha = 1f;
        _stage.gameObject.SetActive(true);
        _viewport.enabled = true;
        _viewportService.RegisterFixedViewport(this);
        uint token = _clock.Begin(_profile.ScrollPixelsPerSecond, period, _profile.StartVolume, _profile.SoundFadeSeconds);
        Step(0f);
        _running = StartCoroutine(Play(token));
        return true;
    }

    public void Hide()
    {
        _clock.Hide(); // 먼저 세대를 무효화하여 이전 coroutine의 finally가 새 표시를 닫지 못하게 합니다.
        Coroutine running = _running;
        _running = null;
        if (running != null) StopCoroutine(running);
        if (_stage != null) _stage.gameObject.SetActive(false);
        if (_canvas != null) _canvas.enabled = false;
        if (_group != null) _group.alpha = 0f;
        if (_viewport != null) _viewport.enabled = false;
        if (_viewportService != null) _viewportService.Unregister(this);
        if (_audioSource != null)
        {
            _audioSource.Stop();
            _audioSource.clip = null;
            _audioSource.volume = 0f;
        }
        _clip = null;
        _audioPaused = false;
    }

    private IEnumerator Play(uint token)
    {
        yield return null; // StartCoroutine 반환 전에 _running을 덮어쓰지 않게 합니다.
        try
        {
            while (_clock.Owns(token) && _profile != null)
            {
                Step(Time.unscaledDeltaTime);
                yield return null;
            }
        }
        finally
        {
            if (_clock.Owns(token))
            {
                _running = null;
                Hide();
            }
        }
    }

    private void Step(float delta)
    {
        bool paused = BattleDelay.IsPaused || GameInput.IsBattleInputBlocked
            || (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameState.Cutscene);
        if (paused)
        {
            if (_audioSource != null && _audioSource.isPlaying)
            {
                _audioSource.Pause();
                _audioPaused = true;
            }
            _clock.Tick(delta, true, false);
            return;
        }
        if (_audioPaused && _audioSource != null)
        {
            _audioSource.UnPause();
            _audioPaused = false;
        }
        bool audioReady = _clock.NeedsSound && _clip != null && _clip.loadState == AudioDataLoadState.Loaded && EnsureAudioSource();
        BattlePhaseOverlayAudioCommand command = _clock.Tick(delta, false, audioReady);
        _material.SetFloat(ScrollId, _clock.Scroll01);
        if (_audioSource == null) return;
        if (command == BattlePhaseOverlayAudioCommand.Start)
        {
            _audioSource.clip = _clip;
            _audioSource.volume = _clock.Volume;
            _audioSource.Play();
        }
        else if (command == BattlePhaseOverlayAudioCommand.Stop)
        {
            _audioSource.Stop();
            _audioSource.clip = null;
        }
        _audioSource.volume = _clock.Volume;
    }

    private bool EnsureAudioSource()
    {
        if (_audioSource != null) return true;
        AudioSource template = AudioManager.Instance != null ? AudioManager.Instance.SfxSource : null;
        if (template == null || template.outputAudioMixerGroup == null) return false;
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.loop = false;
        _audioSource.spatialBlend = 0f;
        _audioSource.dopplerLevel = 0f;
        _audioSource.pitch = 1f;
        _audioSource.priority = 200; // 음악보다 먼저 가상화할 수 있는 낮은 우선순위의 장식음입니다.
        _audioSource.volume = 0f;
        _audioSource.outputAudioMixerGroup = template.outputAudioMixerGroup;
        return true;
    }

    private bool EnsureView()
    {
        if (_material == null)
        {
            Shader shader = Resources.Load<Shader>("HubToHome/BattlePhaseOverlay");
            if (shader == null) return false;
            _material = new Material(shader) { name = "Battle Phase Overlay (Owned)", hideFlags = HideFlags.DontSave };
        }
        if (_canvas == null) _canvas = GetComponent<Canvas>();
        if (!_canvas.isRootCanvas) return false;
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.overrideSorting = true;
        _canvas.sortingLayerID = 0;
        _canvas.sortingOrder = _profile.CanvasSortingOrder;
        _canvas.pixelPerfect = true;
        if (_group == null) _group = GetComponent<CanvasGroup>();
        _group.interactable = false;
        _group.blocksRaycasts = false;
        _viewportService = UIViewportService.GetOrCreate();
        _viewportService.RegisterFixedViewport(this);
        _viewport = GetComponent<FixedCanvasViewport>();
        if (_viewport == null || _viewport.ContentRect == null) return false;
        if (_stage == null)
        {
            _stage = new GameObject("Phase Corner Ribbons", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            _stage.SetParent(_viewport.ContentRect, false);
            _stage.anchorMin = _stage.anchorMax = _stage.pivot = new Vector2(.5f, .5f);
            for (int i = 0; i < _ribbons.Length; i++)
            {
                RawImage ribbon = new GameObject("Ribbon " + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
                ribbon.rectTransform.SetParent(_stage, false);
                ribbon.rectTransform.anchorMin = ribbon.rectTransform.anchorMax = ribbon.rectTransform.pivot = new Vector2(.5f, .5f);
                ribbon.raycastTarget = false;
                _ribbons[i] = ribbon;
            }
        }
        return true;
    }

    private void OnEnable() => SceneManager.activeSceneChanged += HandleSceneChanged;
    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= HandleSceneChanged;
        Hide();
    }
    private void HandleSceneChanged(Scene previous, Scene next) => Hide();
    private void OnDestroy()
    {
        Hide();
        if (_material == null) return;
        if (Application.isPlaying) Destroy(_material);
        else DestroyImmediate(_material);
    }
}
