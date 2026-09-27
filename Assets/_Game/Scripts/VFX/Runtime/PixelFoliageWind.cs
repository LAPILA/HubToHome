using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Metadata for integer-texel UV wind. No per-frame CPU animation or generated renderers.</summary>
[ExecuteAlways, DisallowMultipleComponent]
[AddComponentMenu("Hub To Home/VFX/픽셀 풀 바람")]
public sealed class PixelFoliageWind : MonoBehaviour
{
    private enum WindPreset
    {
        [LabelText("선택 안 함")] None = 0,
        [LabelText("잔디 · 잔잔함")] GentleGrass = 1,
        [LabelText("잔디 · 기본 바람")] BreezyGrass = 2,
        [LabelText("잔디 · 강한 바람")] StrongGrass = 3,
        [LabelText("꽃 / 세워진 풀")] RootedFlower = 4,
        [LabelText("덤불 / 나뭇잎")] BushLeaves = 5,
        [LabelText("매달린 천 / 덩굴")] HangingCloth = 6,
        [LabelText("몽환 흔들림")] Dream = 7
    }

    private static readonly int RegionId = Shader.PropertyToID("_FoliageRegion");
    private static readonly int LayoutId = Shader.PropertyToID("_FoliageLayout");
    private static readonly int SwayId = Shader.PropertyToID("_SwayPixels");
    private static readonly int VerticalId = Shader.PropertyToID("_VerticalPixels");
    private static readonly int SpeedId = Shader.PropertyToID("_SwaySpeed");
    private static readonly int ScaleId = Shader.PropertyToID("_WindScale");
    private static readonly int RootId = Shader.PropertyToID("_RootPin");
    private static readonly int PinTopId = Shader.PropertyToID("_PinFromTop");
    private static readonly int EdgeId = Shader.PropertyToID("_EdgeLockPixels");

    [InfoBox("잔디 Tilemap 또는 일반 SpriteRenderer에 붙입니다. Pixel Foliage UV Lit 머티리얼과 함께 사용하며, 색이 아니라 그림의 픽셀 위치를 바꿉니다.")]
    [Title("시작값 불러오기")]
    [LabelText("불러올 프리셋"), SerializeField]
    [Tooltip("프리셋 적용 버튼을 눌러야 수치가 바뀝니다. 이후 자유롭게 조절할 수 있습니다.")]
    private WindPreset _preset;

    [Title("흔들림 조절")]
    [LabelText("좌우 흔들림 (픽셀)"), SerializeField, Range(0f, 8f), OnValueChanged(nameof(Refresh))]
    private float _swayPixels = 2f;
    [LabelText("상하 흔들림 (픽셀)"), SerializeField, Range(0f, 8f), OnValueChanged(nameof(Refresh))]
    private float _verticalPixels;
    [LabelText("초당 흔들림 주기"), SerializeField, Range(0f, 3f), OnValueChanged(nameof(Refresh))]
    private float _speed = 0.45f;
    [LabelText("구역별 바람 차이"), SerializeField, Range(0.05f, 4f), OnValueChanged(nameof(Refresh))]
    private float _windScale = 1.1f;
    [LabelText("고정 강도"), SerializeField, Range(0f, 1f), OnValueChanged(nameof(Refresh))]
    [Tooltip("0은 전체 흔들림, 1은 한쪽 끝 고정. 기본은 아래쪽, 매달린 천은 위쪽을 고정합니다.")]
    private float _rootPin;
    [LabelText("위쪽을 고정"), SerializeField, OnValueChanged(nameof(Refresh))]
    private bool _pinFromTop;
    [LabelText("타일 경계 고정 폭 (픽셀)"), SerializeField, Range(0f, 8f), OnValueChanged(nameof(Refresh))]
    private float _edgeLockPixels = 2f;
    [LabelText("흔들림 엇박자 (위상)"), SerializeField, Range(0f, 6.283185f), OnValueChanged(nameof(Refresh))]
    private float _phase;
    [ShowInInspector, ReadOnly, LabelText("연결 상태")]
    private string _status;

    private Renderer _renderer;
    private SpriteRenderer _spriteRenderer;
    private Tilemap _tilemap;
    private MaterialPropertyBlock _block;
    private Sprite[] _sprites;

    private void OnEnable()
    {
        _tilemap = GetComponent<Tilemap>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _renderer = _tilemap != null ? GetComponent<TilemapRenderer>() : _spriteRenderer;
        if (_spriteRenderer != null) _spriteRenderer.RegisterSpriteChangeCallback(OnSpriteChanged);
        if (_tilemap != null) Tilemap.tilemapTileChanged += OnTilesChanged;
#if UNITY_EDITOR
        UnityEditor.Undo.undoRedoPerformed += Refresh;
#endif
        Refresh();
    }

    private void OnDisable()
    {
        if (_spriteRenderer != null) _spriteRenderer.UnregisterSpriteChangeCallback(OnSpriteChanged);
        Tilemap.tilemapTileChanged -= OnTilesChanged;
#if UNITY_EDITOR
        UnityEditor.Undo.undoRedoPerformed -= Refresh;
#endif
        DisableEffect();
    }

    private void OnSpriteChanged(SpriteRenderer source) => Refresh();
    private void OnTilesChanged(Tilemap changed, Tilemap.SyncTile[] tiles)
    {
        if (changed == _tilemap) Refresh();
    }

    // Authoring shortcuts only: choosing a preset never starts another animation system.
    [Button("프리셋 적용"), EnableIf(nameof(HasSelectedPreset))]
    private void ApplySelectedPreset()
    {
        if (!HasSelectedPreset) return;
        RecordAuthoringUndo("픽셀 바람 프리셋 적용");
        (_swayPixels, _verticalPixels, _speed, _windScale, _rootPin, _edgeLockPixels, _pinFromTop) = _preset switch
        {
            WindPreset.GentleGrass => (1f, 0f, 0.25f, 0.8f, 0f, 2f, false),
            WindPreset.BreezyGrass => (2f, 0f, 0.45f, 1.1f, 0f, 2f, false),
            WindPreset.StrongGrass => (3f, 0f, 0.75f, 1.4f, 0f, 2f, false),
            WindPreset.RootedFlower => (2f, 0f, 0.35f, 0.4f, 1f, 0f, false),
            WindPreset.BushLeaves => (2f, 1f, 0.3f, 0.8f, 0.25f, 0f, false),
            WindPreset.HangingCloth => (3f, 0f, 0.28f, 0.4f, 1f, 0f, true),
            WindPreset.Dream => (1f, 1f, 0.2f, 0.6f, 0f, 0f, false),
            _ => (_swayPixels, _verticalPixels, _speed, _windScale, _rootPin, _edgeLockPixels, _pinFromTop)
        };
        FinishAuthoringChange();
    }

    private bool HasSelectedPreset => _preset != WindPreset.None;

    [Button("엇박자 바꾸기")]
    private void VaryPhase()
    {
        RecordAuthoringUndo("픽셀 바람 위상 변경");
        _phase = Mathf.Repeat(_phase + 2.399963f, Mathf.PI * 2f);
        FinishAuthoringChange();
    }

    private void RecordAuthoringUndo(string label)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.Undo.RecordObject(this, label);
#endif
    }

    private void FinishAuthoringChange()
    {
        Refresh();
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        }
#endif
    }

    [Button("설정 적용 / 연결 확인"), ContextMenu("설정 적용 / 연결 확인")]
    public void Refresh()
    {
        if (!isActiveAndEnabled) return;
        if (_renderer == null)
        {
            _status = "Grid 부모가 아니라 실제 Tilemap 또는 SpriteRenderer에 붙이세요.";
            return;
        }
        Material material = _renderer.sharedMaterial;
        if (material == null || !material.HasProperty(LayoutId))
        {
            _status = "Renderer Material에 Mat_PixelFoliage_Grass를 지정하세요.";
            DisableEffect();
            return;
        }

        Sprite reference;
        if (_tilemap != null)
        {
            int count = _tilemap.GetUsedSpritesCount();
            if (count == 0) { Fail("타일맵이 비어 있습니다. 잔디를 그리면 자동 적용됩니다."); return; }
            if (_sprites == null || _sprites.Length < count) _sprites = new Sprite[count];
            int used = _tilemap.GetUsedSpritesNonAlloc(_sprites);
            reference = null;
            for (int i = 0; i < used; i++)
            {
                Sprite sprite = _sprites[i];
                if (sprite == null) continue;
                if (sprite.packed) { Fail("타일맵 바람은 회전/패킹되지 않은 격자 스프라이트 시트가 필요합니다."); return; }
                if (reference == null) reference = sprite;
                Rect rect = sprite.rect;
                Vector2 size = reference.rect.size;
                if (sprite.texture != reference.texture)
                {
                    Fail($"바람 꺼짐: {reference.texture.name} / {sprite.texture.name} 텍스처가 섞여 있습니다. 잔디와 벽을 별도 Tilemap으로 나누세요.");
                    return;
                }
                if (rect.size != size
                    || !Mathf.Approximately(sprite.pixelsPerUnit, reference.pixelsPerUnit)
                    || !IsGridAligned(rect.x, size.x) || !IsGridAligned(rect.y, size.y))
                { Fail("같은 텍스처·크기·PPU의 격자 타일만 한 타일맵에서 사용하세요."); return; }
            }
        }
        else
        {
            reference = _spriteRenderer != null ? _spriteRenderer.sprite : null;
            if (_spriteRenderer != null && _spriteRenderer.drawMode != SpriteDrawMode.Simple)
            { Fail("일반 스프라이트는 Draw Mode = Simple이 필요합니다."); return; }
        }
        if (reference == null) { Fail("연결된 스프라이트가 없습니다."); return; }
        if (reference.packed) { Fail("이 버전은 패킹 전 원본 스프라이트 시트를 사용합니다."); return; }

        Rect region = reference.rect;
        if (_tilemap != null) region.position = Vector2.zero;
        ReadBlock();
        _block.SetVector(RegionId, new Vector4(region.x, region.y, region.width, region.height));
        _block.SetVector(LayoutId, new Vector4(Mathf.Max(1f, reference.pixelsPerUnit),
            _tilemap != null ? 1f : 0f, 1f, _phase));
        _block.SetFloat(SwayId, Mathf.Clamp(_swayPixels, 0f, 8f));
        _block.SetFloat(VerticalId, Mathf.Clamp(_verticalPixels, 0f, 8f));
        _block.SetFloat(SpeedId, Mathf.Clamp(_speed, 0f, 3f));
        _block.SetFloat(ScaleId, Mathf.Clamp(_windScale, 0.05f, 4f));
        _block.SetFloat(RootId, Mathf.Clamp01(_rootPin));
        _block.SetFloat(PinTopId, _pinFromTop ? 1f : 0f);
        _block.SetFloat(EdgeId, Mathf.Clamp(_edgeLockPixels, 0f, 8f));
        _renderer.SetPropertyBlock(_block);
        _status = $"정상: {(_tilemap != null ? "Tilemap" : "Sprite")} / {reference.texture.name} / {region.width}×{region.height}px / UV 픽셀 이동";
    }

    private static bool IsGridAligned(float value, float size)
    {
        if (size <= 0f) return false;
        float cell = value / size;
        return Mathf.Abs(cell - Mathf.Round(cell)) < 0.0001f;
    }

    private void Fail(string message)
    {
        _status = message;
        DisableEffect();
    }

    private void ReadBlock()
    {
        if (_block == null) _block = new MaterialPropertyBlock();
        _renderer.GetPropertyBlock(_block);
    }

    private void DisableEffect()
    {
        if (_renderer == null) return;
        ReadBlock();
        _block.SetVector(LayoutId, Vector4.zero);
        _renderer.SetPropertyBlock(_block);
    }
}
