using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;

public partial class CameraController
{
    [Title("전투 시야 · HUD 가림 방지")]
    [SerializeField, LabelText("전투 UI 안전영역 사용")] private bool _useBattleViewportSafety = true;
    [SerializeField, Min(0.5f), LabelText("전투 최소 카메라 크기")]
    [Tooltip("픽셀 규격의 허용값으로 올림합니다. PPU32/480에서는 3.75, 7.5 등이 허용됩니다.")]
    private float _battleMinimumLens = 3.75f;
    [SerializeField, Range(1, 4), LabelText("전투 기준 픽셀 확대 배율")]
    [Tooltip("1은 원본1픽셀을 기준화면1픽셀로 표시합니다. 전투 시작에 안전영역을 확보한 뒤 크기를 유지합니다.")]
    private int _battlePixelArtScale = 1;
    [SerializeField, Range(0f, 2.5f), LabelText("아군 뒤 회피 여유")] private float _battleDodgeReserve = 1.6f;
    [SerializeField, Range(0f, 0.8f), LabelText("몸체 가장자리 여유")] private float _battleBodyMargin = 0.25f;

    private struct BattleViewportMember
    {
        public Transform Root;
        public SpriteRenderer Body;
        public bool HasBodyBounds;
        public Vector3 CenterOffset;
        public Vector3 Extents;
        public bool IsPlayer;
        public int Seen;
    }

    private readonly List<BattleViewportMember> _battleViewportMembers = new List<BattleViewportMember>(8);
    private bool _battleViewportActive;
    private int _battleViewportGeneration;
    private float _battlePixelLockedSize;
    private float _battlePixelReferenceSize;
    private bool _battlePixelLockedIsStrict;
    private bool _battlePixelCapacityWarned;
    private bool IsStrictBattlePixelView => _battleViewportActive
        && _continuousPixelZoom != null && _continuousPixelZoom.IsPixelPerfectActive;

    private void UpdateBattleViewportSafety()
    {
        BattleManager battle = BattleManager.Instance;
        // Pause/대사로 HUD가 숨겨져도 같은 전투의 배율 정책은 해제하지 않습니다.
        bool active = _useBattleViewportSafety && !_timelineLease.IsValid && _vCam != null
            && (_battleShotActive || battle != null && battle.IsBattleSessionActive);
        if (!active)
        {
            if (_continuousPixelZoom != null && _continuousPixelZoom.IsBattleViewportOwnedBy(this))
            {
                if (!_battleShotActive && !_isFramingTargets) _continuousPixelZoom.Release();
                _continuousPixelZoom.SetBattleViewportOwner(null);
            }
            _battleViewportActive = false;
            if (battle == null || !battle.IsBattleSessionActive)
            {
                _battleViewportMembers.Clear();
                _battlePixelLockedSize = 0f;
                _battlePixelReferenceSize = 0f;
                _battlePixelLockedIsStrict = false;
                _battlePixelCapacityWarned = false;
            }
            return;
        }

        _battleViewportActive = true;
        if (battle != null && battle.IsBattleSessionActive)
        {
            int generation = ++_battleViewportGeneration;
            // 참조 비교만 반복합니다. Renderer/컴포넌트 검색은 새 멤버를 등록할 때 한 번뿐입니다.
            for (int i = 0; i < battle._playerParty.Count; i++)
            {
                PlayerCharacter actor = battle._playerParty[i];
                if (actor != null) CacheBattleViewportActor(actor.transform, actor, generation);
            }
            for (int i = 0; i < battle._enemies.Count; i++)
            {
                EnemyCharacter actor = battle._enemies[i];
                if (actor != null) CacheBattleViewportActor(actor.transform, actor, generation);
            }
            for (int i = _battleViewportMembers.Count - 1; i >= 0; i--)
                if (_battleViewportMembers[i].Root == null || _battleViewportMembers[i].Seen != generation)
                    _battleViewportMembers.RemoveAt(i);
        }
        // Reset/Timeline이 해제한 뒤에는 별도 플래그 대신 실제 소유 상태를 보고 다시 확보합니다.
        if (_continuousPixelZoom == null || !_continuousPixelZoom.IsBattleViewportOwnedBy(this))
            BeginContinuousBattleZoom();
        _continuousPixelZoom.SetBattleViewportOwner(this);
    }

    internal bool TryGetBattlePixelLockedSize(out float size)
    {
        size = _battleCounterFocus != null && _battleCounterFocus.IsActive
            ? _battleCounterFocus.Size : _battlePixelLockedSize;
        return isActiveAndEnabled && _useBattleViewportSafety && IsStrictBattlePixelView
            && _battlePixelLockedIsStrict && !_timelineLease.IsValid && size > 0f
            && BattleManager.Instance != null && BattleManager.Instance.IsBattleSessionActive;
    }

    private void CacheBattleViewportActor(Transform root, CharacterBase actor, int generation)
    {
        for (int i = 0; i < _battleViewportMembers.Count; i++)
        {
            BattleViewportMember existing = _battleViewportMembers[i];
            if (existing.Root != root) continue;
            existing.Seen = generation;
            // Animator가 첫 프레임에 Sprite를 지정하는 프리팹도 초기화 순서에 의존하지 않습니다.
            if (!existing.HasBodyBounds && existing.Body != null && existing.Body.sprite != null)
            {
                Bounds ready = existing.Body.bounds;
                existing.CenterOffset = ready.center - root.position;
                existing.Extents = new Vector3(Mathf.Max(0.65f, ready.extents.x), Mathf.Max(1f, ready.extents.y), 0f);
                existing.HasBodyBounds = true;
            }
            _battleViewportMembers[i] = existing;
            return;
        }
        if (root == null) return;
        SpriteRenderer body = root.GetComponent<SpriteRenderer>();
        if (body == null) body = root.GetComponentInChildren<SpriteRenderer>(true);
        Bounds bounds = body != null && body.sprite != null ? body.bounds
            : new Bounds(root.position, new Vector3(1.3f, 2f, 0f));
        _battleViewportMembers.Add(new BattleViewportMember
        {
            Root = root,
            Body = body,
            HasBodyBounds = body != null && body.sprite != null,
            CenterOffset = bounds.center - root.position,
            // 공격 프레임의 무기/VFX 크기를 매번 따라가면 렌즈가 출렁입니다.
            Extents = new Vector3(Mathf.Max(0.65f, bounds.extents.x), Mathf.Max(1f, bounds.extents.y), 0f),
            IsPlayer = actor is PlayerCharacter,
            Seen = generation
        });
    }

    private Transform ResolveBattleViewportFocus()
    {
        if (_battleShotActive || _timelineLease.IsValid || _vCam == null) return null;
        Transform tracked = _vCam.Target.TrackingTarget;
        if (tracked == null || !tracked.gameObject.activeInHierarchy) return null;
        // Reset의 기본 Follow가 배우인 구성도 명시 포커스로 오인하지 않습니다.
        // ResolveDefaultTarget과 같은 우선순위지만 런타임 fallback 오브젝트를 새로 만들지 않습니다.
        Transform defaultTarget = _centerTarget != null ? _centerTarget
            : _startupTarget != null ? _startupTarget : _fallbackTarget;
        if (tracked == defaultTarget) return null;
        for (int i = 0; i < _battleViewportMembers.Count; i++)
            if (_battleViewportMembers[i].Root == tracked) return tracked;
        return null;
    }

    /// <summary>현재 명령이 만든 최종 화면만 보정합니다. 원래 Tween·월드 위치·HUD는 건드리지 않습니다.</summary>
    public void ConstrainBattleViewport(ref CameraState state)
    {
        if (!isActiveAndEnabled || !_battleViewportActive || !_useBattleViewportSafety || _timelineLease.IsValid
            || !state.Lens.Orthographic) return;
        if (TryConstrainBattleCounterFocus(ref state)) return;
        // UI가 잠시 없거나 비활성이어도 복귀 명령의 원본 Lens가 드러나지 않게 합니다.
        if (_battlePixelLockedSize > 0f && _battlePixelLockedIsStrict == IsStrictBattlePixelView)
            state.Lens.OrthographicSize = _battlePixelLockedSize;
        if (TryConstrainScreenPassFrame(ref state)) return;
        if (_battleViewportMembers.Count == 0) return;
        BattleUIController ui = BattleUIController.Instance;
        if (ui == null || !ui.IsHudChromeVisible
            || !ui.TryGetBattlePlayfieldScreenRect(out Camera output, out Rect safe)) return;
        Rect pixels = output.pixelRect;
        if (pixels.width <= 0f || pixels.height <= 0f) return;
        Rect viewport = Rect.MinMaxRect((safe.xMin - pixels.xMin) / pixels.width,
            (safe.yMin - pixels.yMin) / pixels.height,
            (safe.xMax - pixels.xMin) / pixels.width, (safe.yMax - pixels.yMin) / pixels.height);

        Quaternion rotation = state.GetFinalOrientation();
        Quaternion inverse = Quaternion.Inverse(rotation);
        // 평면 전투용입니다. 별도 입체/Timeline 카메라의 원근 구도는 침범하지 않습니다.
        if (Mathf.Abs((rotation * Vector3.forward).z) < 0.999f) return;
        Vector3 right = inverse * Vector3.right;
        Vector3 up = inverse * Vector3.up;
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        bool any = false;
        float margin = Mathf.Clamp(_battleBodyMargin, 0f, 0.8f);
        Transform focus = ResolveBattleViewportFocus();
        for (int i = 0; i < _battleViewportMembers.Count; i++)
        {
            BattleViewportMember member = _battleViewportMembers[i];
            if (member.Root == null || !member.Root.gameObject.activeInHierarchy) continue;
            if (focus != null && member.Root != focus) continue;
            Vector3 center = member.Root.position + member.CenterOffset;
            Vector3 extents = member.Extents + new Vector3(margin, margin, 0f);
            if (member.IsPlayer)
            {
                float reserve = Mathf.Clamp(_battleDodgeReserve, 0f, 2.5f);
                center.x -= reserve * 0.5f;
                extents.x += reserve * 0.5f;
            }
            Vector3 local = inverse * center;
            Vector2 radius = new Vector2(Mathf.Abs(right.x) * extents.x + Mathf.Abs(up.x) * extents.y,
                Mathf.Abs(right.y) * extents.x + Mathf.Abs(up.y) * extents.y);
            min = Vector2.Min(min, (Vector2)local - radius);
            max = Vector2.Max(max, (Vector2)local + radius);
            any = true;
        }
        if (!any) return;
        Vector3 current = state.GetFinalPosition();
        Vector3 localCamera = inverse * current;
        Rect bodyBounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        float reference = _continuousPixelZoom != null ? _continuousPixelZoom.PixelReferenceSize : 7.5f;
        if (_battlePixelLockedSize <= 0f || !Mathf.Approximately(reference, _battlePixelReferenceSize)
            || _battlePixelLockedIsStrict != IsStrictBattlePixelView)
        {
            float requested = IsStrictBattlePixelView ? reference / Mathf.Clamp(_battlePixelArtScale, 1, 4)
                : Mathf.Max(state.Lens.OrthographicSize, _battleMinimumLens);
            BattleCameraViewportFrame initial = BattleCameraViewportFraming.Fit(bodyBounds, viewport,
                localCamera, requested, state.Lens.Aspect, _battleMinimumLens);
            _battlePixelLockedSize = IsStrictBattlePixelView
                ? CameraPixelGridPolicy.SnapSizeAtLeast(initial.Size, reference) : initial.Size;
            _battlePixelReferenceSize = reference;
            _battlePixelLockedIsStrict = IsStrictBattlePixelView;
        }
        // 한 전투의 이동·홉·복귀 때문에 배율을 늘렸다 줄이지 않습니다. 중심만 안전범위로 제한합니다.
        BattleCameraViewportFrame fit = BattleCameraViewportFraming.FitAtFixedSize(bodyBounds, viewport,
            localCamera, _battlePixelLockedSize, state.Lens.Aspect);
        if (IsStrictBattlePixelView && !_battlePixelCapacityWarned
            && (bodyBounds.height > 2f * fit.Size * viewport.height + 0.01f
                || bodyBounds.width > 2f * fit.Size * state.Lens.Aspect * viewport.width + 0.01f))
        {
            _battlePixelCapacityWarned = true;
            Debug.LogWarning("[CameraController] 전투 배치가 고정 픽셀 시야를 넘습니다. 위치/이동 높이를 조정하세요. 자동 확대·축소는 하지 않습니다.", this);
        }
        localCamera.x = fit.Center.x;
        localCamera.y = fit.Center.y;
        Vector3 corrected = rotation * localCamera;
        corrected.z = current.z;
        state.PositionCorrection += corrected - current;
        state.Lens.OrthographicSize = fit.Size;
    }
}
