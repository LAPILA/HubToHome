using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public struct ElementalStatusIconDefinition
{
    [LabelText("누적 상태")] public ElementalStatusKind Kind;
    [LabelText("아이콘")] public Sprite Icon;
}

/// <summary>적 발밑의 누적 상태 표시. 수치는 이벤트로, 월드 추적 좌표만 LateUpdate로 갱신합니다.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(1000)]
public sealed class BattleEnemyStatusPresenter : MonoBehaviour
{
    private const int SlotCount = 6; // 다섯 누적 상태 + 빙결
    private const float CellWidth = 40f;
    private const float CellHeight = 18f;
    private const float Gap = 2f;
    private readonly List<Row> _rows = new List<Row>(4);
    private RectTransform _host;
    private Canvas _canvas;
    private Camera _worldCamera;
    private TMP_FontAsset _font;
    private ElementalStatusIconDefinition[] _icons;
    private float _bottomOffset;
    private int _columns;
    private int _boundCount;

    public void Bind(RectTransform host, Camera worldCamera, TMP_FontAsset font,
        IReadOnlyList<EnemyCharacter> enemies, ElementalStatusIconDefinition[] icons,
        float bottomOffset = 8f, int columns = 3)
    {
        Clear();
        if (host == null) return;
        _host = host;
        Canvas canvas = host.GetComponentInParent<Canvas>();
        _canvas = canvas != null ? canvas.rootCanvas : null;
        _worldCamera = worldCamera;
        _font = font != null ? font : TMP_Settings.defaultFontAsset;
        _icons = icons;
        _bottomOffset = bottomOffset;
        _columns = Mathf.Clamp(columns, 1, SlotCount);
        for (int i = 0; enemies != null && i < enemies.Count; i++)
        {
            EnemyCharacter actor = enemies[i];
            if (actor == null) continue;
            Row row = _boundCount < _rows.Count ? _rows[_boundCount] : CreateRow();
            _boundCount++;
            row.Root.SetParent(_host, false);
            row.Bind(actor);
        }
        UpdatePositions();
    }

    public void BindWorldCamera(Camera camera) => _worldCamera = camera;

    public void Clear()
    {
        for (int i = 0; i < _rows.Count; i++) _rows[i].Unbind();
        _boundCount = 0;
    }

    public Sprite ResolveIcon(ElementalStatusKind kind)
    {
        for (int i = 0; _icons != null && i < _icons.Length; i++)
            if (_icons[i].Kind == kind) return _icons[i].Icon;
        return null;
    }

    private void LateUpdate() => UpdatePositions();

    private void UpdatePositions()
    {
        if (_boundCount == 0 || _host == null) return;
        if (_worldCamera == null) _worldCamera = Camera.main;
        Camera uiCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _canvas.worldCamera : null;
        for (int i = 0; i < _boundCount; i++)
        {
            Row row = _rows[i];
            bool visible = row.Count > 0 && row.Actor != null && row.Actor.IsAlive
                && row.Actor.isActiveAndEnabled && row.Bottom != null && _worldCamera != null;
            Vector2 position = default;
            if (visible)
            {
                Vector3 screen = _worldCamera.WorldToScreenPoint(row.Bottom.position);
                visible = screen.z > 0f && _worldCamera.pixelRect.Contains(new Vector2(screen.x, screen.y))
                    && RectTransformUtility.ScreenPointToLocalPointInRectangle(_host, screen, uiCamera, out position);
            }
            if (row.Root == null) continue;
            if (row.Root.gameObject.activeSelf != visible) row.Root.gameObject.SetActive(visible);
            if (visible)
                row.Root.anchoredPosition = new Vector2(Mathf.Round(position.x), Mathf.Round(position.y - _bottomOffset));
        }
    }

    private Row CreateRow()
    {
        RectTransform root = CreateRect("Enemy elemental status", _host, Vector2.zero);
        root.pivot = new Vector2(0.5f, 1f);
        // 월드 표시는 고정 하단 메뉴/대사보다 뒤에 그립니다.
        root.SetAsFirstSibling();
        var row = new Row(this, root);
        for (int i = 0; i < SlotCount; i++)
        {
            RectTransform cell = CreateRect("Status " + i, root, new Vector2(CellWidth, CellHeight));
            cell.anchorMin = cell.anchorMax = cell.pivot = new Vector2(0f, 1f);
            Image background = cell.gameObject.AddComponent<Image>();
            background.color = new Color(0.025f, 0.035f, 0.05f, 0.85f);
            background.raycastTarget = false;
            RectTransform iconRect = CreateRect("Icon", cell, new Vector2(16f, 16f));
            iconRect.anchoredPosition = new Vector2(-11f, 0f);
            Image icon = iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            RectTransform valueRect = CreateRect("Value", cell, new Vector2(22f, 18f));
            valueRect.anchoredPosition = new Vector2(8f, 0f);
            TextMeshProUGUI value = valueRect.gameObject.AddComponent<TextMeshProUGUI>();
            value.font = _font;
            value.fontSize = 10f;
            value.alignment = TextAlignmentOptions.Center;
            value.enableAutoSizing = false;
            value.textWrappingMode = TextWrappingModes.NoWrap;
            value.overflowMode = TextOverflowModes.Overflow;
            value.raycastTarget = false;
            row.Cells[i] = cell;
            row.Icons[i] = icon;
            row.Values[i] = value;
            cell.gameObject.SetActive(false);
        }
        root.gameObject.SetActive(false);
        _rows.Add(row);
        return row;
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.gameObject.layer = parent.gameObject.layer;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        return rect;
    }

    private void OnDisable() => Clear();

    private void OnDestroy()
    {
        Clear();
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].Root == null) continue;
            if (Application.isPlaying) Destroy(_rows[i].Root.gameObject);
            else DestroyImmediate(_rows[i].Root.gameObject);
        }
        _rows.Clear();
    }

    private sealed class Row
    {
        private readonly BattleEnemyStatusPresenter _owner;
        private bool _wasAlive;
        public readonly RectTransform Root;
        public readonly RectTransform[] Cells = new RectTransform[SlotCount];
        public readonly Image[] Icons = new Image[SlotCount];
        public readonly TMP_Text[] Values = new TMP_Text[SlotCount];
        public EnemyCharacter Actor { get; private set; }
        public Transform Bottom { get; private set; }
        public int Count { get; private set; }

        public Row(BattleEnemyStatusPresenter owner, RectTransform root) { _owner = owner; Root = root; }

        public void Bind(EnemyCharacter actor)
        {
            Actor = actor;
            Bottom = actor.GetPivot(CharacterPivotId.Bottom);
            actor.OnStatusEffectsChanged += Refresh;
            actor.OnHPChanged += OnHPChanged;
            Refresh(actor);
        }

        public void Unbind()
        {
            // C# 이벤트는 파괴된 Unity 객체에서도 구독 해제해야 합니다.
            if (!ReferenceEquals(Actor, null))
            {
                Actor.OnStatusEffectsChanged -= Refresh;
                Actor.OnHPChanged -= OnHPChanged;
            }
            Actor = null;
            Bottom = null;
            Count = 0;
            if (Root != null) Root.gameObject.SetActive(false);
        }

        private void OnHPChanged(CharacterBase actor, int hp, int maxHp)
        {
            if ((hp > 0) != _wasAlive) Refresh(actor);
        }

        private void Refresh(CharacterBase actor)
        {
            if (Root == null) return;
            _wasAlive = actor != null && actor.IsAlive;
            Count = _wasAlive
                ? Mathf.Min(SlotCount, ElementalStatusPresentation.GetEntryCount(actor.Elements)) : 0;
            int columns = Mathf.Min(_owner._columns, Count);
            Root.sizeDelta = new Vector2(columns * (CellWidth + Gap) - (columns > 0 ? Gap : 0f),
                Count == 0 ? 0f : Mathf.CeilToInt((float)Count / columns) * (CellHeight + Gap) - Gap);
            for (int i = 0; i < SlotCount; i++)
            {
                if (Cells[i] == null) continue;
                Cells[i].gameObject.SetActive(i < Count);
                if (i >= Count) continue;
                ElementalStatusPresentation.TryGetEntry(actor.Elements, i, out ElementalStatusKind kind,
                    out int amount, out bool frozen);
                Sprite sprite = _owner.ResolveIcon(frozen ? ElementalStatusKind.Chill : kind);
                Icons[i].sprite = sprite;
                Icons[i].enabled = sprite != null;
                Values[i].text = frozen ? "빙" : amount.ToString();
                Values[i].color = ElementalStatusPresentation.GetColor(frozen ? ElementalStatusKind.Chill : kind);
                int inThisRow = Mathf.Min(columns, Count - (i / columns) * columns);
                float inset = (columns - inThisRow) * (CellWidth + Gap) * 0.5f;
                Cells[i].anchoredPosition = new Vector2(inset + (i % columns) * (CellWidth + Gap),
                    -(i / columns) * (CellHeight + Gap));
            }
            if (Count == 0) Root.gameObject.SetActive(false);
        }
    }
}
