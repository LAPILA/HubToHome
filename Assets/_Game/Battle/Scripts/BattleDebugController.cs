using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using UnityEngine.InputSystem;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
/// <summary>
/// BattleScene 전용 디버그 컨트롤러.
/// </summary>
public class BattleDebugController : MonoBehaviour
{
    // ── 설정 ──────────────────────────────────────────────────
    [BoxGroup("Debug Settings")]
    [SerializeField] private bool _showOnStart = true;

    [BoxGroup("Debug Settings")]
    [SerializeField] private Key _toggleKey = Key.F1;

    [BoxGroup("Debug UI Appearance")]
    [Tooltip("UI 전체 크기 비율을 조절합니다 (고해상도 모니터용)")]
    [SerializeField, Range(1f, 3f)] private float _guiScale = 1.5f;

    [BoxGroup("Debug UI Appearance")]
    [SerializeField] private float _panelWidth = 450f;

    // ── 내부 상태 ─────────────────────────────────────────────
    private bool _showPanel = true;
    private Vector2 _scrollPos;
    private GUIStyle _boxStyle;
    private GUIStyle _innerBoxStyle;
    private GUIStyle _labelStyle;
    private GUIStyle _buttonStyle;
    private GUIStyle _headerStyle;
    private bool _stylesInitialized = false;

    private void Start()
    {
        _showPanel = _showOnStart;
        DumpStateToConsole(); 
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current[_toggleKey].wasPressedThisFrame)
            _showPanel = !_showPanel;

        if (Keyboard.current.f2Key.wasPressedThisFrame) ForcePlayerTurn();
        if (Keyboard.current.f3Key.wasPressedThisFrame) ForceEnemyTurn();
        if (Keyboard.current.f4Key.wasPressedThisFrame) ForceBattleEnd(true);
        if (Keyboard.current.f5Key.wasPressedThisFrame) ForceBattleEnd(false);
        if (Keyboard.current.f6Key.wasPressedThisFrame) SetAllEnemyHP(1);
        if (Keyboard.current.f7Key.wasPressedThisFrame) SetAllPlayerHP(1);
        if (Keyboard.current.f8Key.wasPressedThisFrame) DumpStateToConsole();
    }

    // ── GUI ───────────────────────────────────────────────────
    private void OnGUI()
    {
        if (!_showPanel) return;
        InitStyles();

        // ✅ 고해상도 대응: GUI 전체 스케일링
        Vector2 scale = new Vector2(_guiScale, _guiScale);
        GUIUtility.ScaleAroundPivot(scale, Vector2.zero);

        // 스케일이 커지면 화면 가용 높이가 줄어들므로 역산해서 계산
        float panelH = (Screen.height / _guiScale) * 0.9f;
        var rect = new Rect(10, 10, _panelWidth, panelH);

        GUI.Box(rect, "", _boxStyle);
        GUILayout.BeginArea(new Rect(rect.x + 10, rect.y + 10, rect.width - 20, rect.height - 20));
        _scrollPos = GUILayout.BeginScrollView(_scrollPos);

        // ── 헤더 ──────────────────────────────────────────────
        GUILayout.Label("⚔ BATTLE DEBUG PANEL", _headerStyle);
        GUILayout.Label($"<color=#aaaaaa>[{_toggleKey}] 패널 토글  |  현재 시간: {Time.time:F1}s</color>", _labelStyle);
        GUILayout.Space(10);

        var bm = BattleManager.Instance;
        if (bm == null)
        {
            GUILayout.Label("❌ BattleManager.Instance == null", _labelStyle);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            return;
        }

        // ── 그룹 1: 전투 상태 ─────────────────────────────────
        BeginGroup("📊 전투 상태");
        GUILayout.Label($"<b>CurrentState :</b> {bm.CurrentState}", _labelStyle);
        GUILayout.Label($"<b>PlayerParty :</b> {bm.PlayerParty?.Count ?? 0}명", _labelStyle);
        GUILayout.Label($"<b>Enemies     :</b> {bm.Enemies?.Count ?? 0}마리", _labelStyle);
        EndGroup();

        // ── 그룹 2: 플레이어 파티 ─────────────────────────────
        BeginGroup("👤 플레이어 파티");
        if (bm.PlayerParty != null && bm.PlayerParty.Count > 0)
        {
            foreach (var p in bm.PlayerParty)
            {
                if (p == null) { GUILayout.Label("  [null]", _labelStyle); continue; }
                int mp = bm.GetMP(p);
                string hpBar = BuildBar(p.CurrentHP, p.MaxHP, 12, true);
                string mpBar = BuildBar(mp, 100, 12, false);
                
                GUILayout.Label($"<b>{p.CharacterID}</b>\n  HP: {hpBar} {p.CurrentHP}/{p.MaxHP} \n  MP: {mpBar} {mp}", _labelStyle);

                var pc = p.GetComponent<PlayerController>();
                if (pc != null) GUILayout.Label($"  <color=#88ff88>State:</color> {pc.State}  <color=#88ff88>Facing:</color> {pc.FacingDirection}", _labelStyle);
                GUILayout.Space(4);
            }
        }
        else GUILayout.Label("  (비어있음)", _labelStyle);
        EndGroup();

        // ── 그룹 3: 적 목록 ───────────────────────────────────
        BeginGroup("👾 적 목록");
        if (bm.Enemies != null && bm.Enemies.Count > 0)
        {
            foreach (var e in bm.Enemies)
            {
                if (e == null) { GUILayout.Label("  [null]", _labelStyle); continue; }
                string hpBar = BuildBar(e.CurrentHP, e.MaxHP, 12, true);
                string alive = e.IsAlive ? "✅" : "💀";
                
                GUILayout.Label($"{alive} <b>{e.Data?.EnemyName ?? e.name}</b>\n  HP: {hpBar} {e.CurrentHP}/{e.MaxHP}", _labelStyle);
                
                var anim = e.GetComponent<Animator>();
                if (anim != null) GUILayout.Label($"  <color=#ffaa88>Anim:</color> {anim.GetCurrentAnimatorStateInfo(0).shortNameHash}", _labelStyle);
                GUILayout.Space(4);
            }
        }
        else GUILayout.Label("  (비어있음)", _labelStyle);
        EndGroup();

        // ── 그룹 4: 관리자 상태 ───────────────────────────────
        BeginGroup("⚙️ 시스템 상태");
        var ui = BattleUIController.Instance;
        var qte = QTEManager.Instance;
        GUILayout.Label($"  UI Controller : {(ui != null ? "✅" : "❌ null")}", _labelStyle);
        GUILayout.Label($"  QTE Manager   : {(qte != null ? $"✅ (Active:{qte.IsActive})" : "❌ null")}", _labelStyle);
        EndGroup();

        // ── 그룹 5: 강제 실행 액션 ────────────────────────────
        BeginGroup("🔧 치트 / 강제 실행");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("[F2] 플레이어 턴", _buttonStyle)) ForcePlayerTurn();
        if (GUILayout.Button("[F3] 적 턴", _buttonStyle)) ForceEnemyTurn();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("[F4] 승리 (적 즉사)", _buttonStyle)) ForceBattleEnd(true);
        if (GUILayout.Button("[F5] 패배 (아군 즉사)", _buttonStyle)) ForceBattleEnd(false);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("[F6] 적 HP = 1", _buttonStyle)) SetAllEnemyHP(1);
        if (GUILayout.Button("[F7] 아군 HP = 1", _buttonStyle)) SetAllPlayerHP(1);
        GUILayout.EndHorizontal();

        if (GUILayout.Button("[F8] 콘솔에 상세 덤프 기록", _buttonStyle)) DumpStateToConsole();
        EndGroup();

        // ── 그룹 6: 애니메이션 테스트 ─────────────────────────
        BeginGroup("🎬 애니메이션 테스트");
        GUILayout.Label("<color=#aaaaaa>[적 애니메이션]</color>", _labelStyle);
        if (bm.Enemies != null)
        {
            foreach (var e in bm.Enemies)
            {
                if (e == null) continue;
                var anim = e.GetComponent<Animator>();
                if (anim == null) continue;

                GUILayout.BeginHorizontal();
                GUILayout.Label($" {e.Data?.EnemyName ?? e.name}", _labelStyle, GUILayout.Width(100));
                if (GUILayout.Button("Atk", _buttonStyle)) anim.SetTrigger("Attack");
                if (GUILayout.Button("Hurt", _buttonStyle)) anim.SetTrigger("Hurt");
                if (GUILayout.Button("Idle", _buttonStyle)) anim.SetTrigger("BattleIdle");
                GUILayout.EndHorizontal();
            }
        }

        GUILayout.Space(6);
        GUILayout.Label("<color=#aaaaaa>[플레이어 애니메이션]</color>", _labelStyle);
        if (bm.PlayerParty != null)
        {
            foreach (var p in bm.PlayerParty)
            {
                if (p == null) continue;
                var pc = p.GetComponent<PlayerController>();
                if (pc == null) continue;

                GUILayout.BeginHorizontal();
                GUILayout.Label($" {p.CharacterID}", _labelStyle, GUILayout.Width(100));
                if (GUILayout.Button("Atk", _buttonStyle)) pc.PlayBattleAnim(PlayerController.HashAttack);
                if (GUILayout.Button("Hurt", _buttonStyle)) pc.PlayBattleAnim(PlayerController.HashHurt);
                if (GUILayout.Button("Parry", _buttonStyle)) pc.PlayBattleAnim(PlayerController.HashParry);
                GUILayout.EndHorizontal();
            }
        }
        EndGroup();

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    // ── UI 레이아웃 헬퍼 ───────────────────────────────────────
    private void BeginGroup(string title)
    {
        GUILayout.BeginVertical(_innerBoxStyle);
        GUILayout.Label(title, _headerStyle);
        GUILayout.Space(2);
    }

    private void EndGroup()
    {
        GUILayout.EndVertical();
        GUILayout.Space(8);
    }

    // ── 내부 로직 (이전과 동일) ───────────────────────────────
    private void ValidateScene(BattleManager bm) { /* 기존 내용 유지 */ }
    private void ForcePlayerTurn() { Debug.Log("[BattleDebug] ForcePlayerTurn"); DumpStateToConsole(); }
    private void ForceEnemyTurn() { Debug.Log("[BattleDebug] ForceEnemyTurn"); DumpStateToConsole(); }
    
    private void ForceBattleEnd(bool victory)
    {
        if (victory) SetAllEnemyHP(0);
        else SetAllPlayerHP(0);
    }

    private void SetAllEnemyHP(int hp)
    {
        var bm = BattleManager.Instance;
        if (bm?.Enemies == null) return;
        foreach (var e in bm.Enemies)
        {
            if (e != null && e.CurrentHP - hp > 0) e.TakePureDamage(e.CurrentHP - hp);
        }
    }

    private void SetAllPlayerHP(int hp)
    {
        var bm = BattleManager.Instance;
        if (bm?.PlayerParty == null) return;
        foreach (var p in bm.PlayerParty)
        {
            if (p != null && p.CurrentHP - hp > 0) p.TakePureDamage(p.CurrentHP - hp);
        }
    }

    public void DumpStateToConsole() { /* 기존 내용 유지 - 너무 길어서 생략 또는 기존 함수 사용 */ }

    // ── 유틸리티 ──────────────────────────────────────────────
    private static string BuildBar(int current, int max, int width, bool isHp)
    {
        if (max <= 0) return "[----------]";
        int filled = Mathf.RoundToInt((float)current / max * width);
        filled = Mathf.Clamp(filled, 0, width);
        
        string colorHex = isHp ? "#ff4444" : "#44ccff";
        string bar = new string('█', filled) + new string('░', width - filled);
        return $"<color={colorHex}>[{bar}]</color>";
    }

    private void InitStyles()
    {
        if (_stylesInitialized) return;
        _stylesInitialized = true;

        // 메인 배경 패널 (살짝 투명한 검정)
        _boxStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = MakeTex(2, 2, new Color(0.08f, 0.08f, 0.12f, 0.95f)) }
        };

        // 그룹 내부 박스 (조금 더 밝은 색상으로 영역 구분)
        _innerBoxStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = MakeTex(2, 2, new Color(0.15f, 0.15f, 0.2f, 0.5f)) },
            padding = new RectOffset(8, 8, 8, 8),
            margin = new RectOffset(0, 0, 4, 8)
        };

        // 기본 텍스트 (크기 키움)
        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 13,
            normal    = { textColor = new Color(0.9f, 0.9f, 0.9f) },
            wordWrap  = true,
            richText  = true,
        };

        // 헤더 텍스트 (눈에 띄게)
        _headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 15,
            fontStyle = FontStyle.Bold,
            normal    = { textColor = new Color(0.4f, 0.9f, 1f) },
            richText  = true,
        };

        // 버튼 텍스트
        _buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 12,
            normal   = { textColor = Color.white },
            padding  = new RectOffset(4, 4, 6, 6)
        };
    }

    private static Texture2D MakeTex(int w, int h, Color col)
    {
        var pix = new Color[w * h];
        for (int i = 0; i < pix.Length; i++) pix[i] = col;
        var tex = new Texture2D(w, h);
        tex.SetPixels(pix);
        tex.Apply();
        return tex;
    }
}
#endif