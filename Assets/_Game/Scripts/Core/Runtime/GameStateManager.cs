using System;
using UnityEngine;

/// <summary>
/// 게임의 거시적인 상태(Macro State)를 관리하는 매니저.
/// 플레이어의 입력 락(Lock)이나 컷신 처리 등을 통제할 때 사용합니다.
/// </summary>
public enum GameState 
{ 
    Exploration, // 자유롭게 돌아다니는 상태
    Dialogue,    // 대화 중 (플레이어 이동 불가)
    Battle,      // 전투 중 (이동 불가, 전투 입력만 허용)
    Cutscene,    // 연출 컷신 중 (모든 조작 불가)
    Paused       // 메뉴/일시정지 창 열림
}

public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

    public GameState CurrentState { get; private set; } = GameState.Exploration;

    // 상태가 변할 때마다 옵저버들에게 알림
    public event Action<GameState> OnStateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void ChangeState(GameState newState)
    {
        if (CurrentState == newState) return;

        CurrentState = newState;
        Debug.Log($"<color=#FFD700>[GameState] 상태 변경: {newState}</color>");
        
        OnStateChanged?.Invoke(newState);
    }

    // 편의 속성 (PlayerController 등에서 쉽게 확인)
    public bool CanPlayerMove => CurrentState == GameState.Exploration;
}