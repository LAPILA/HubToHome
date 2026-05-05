using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 대화 시스템 싱글톤 매니저. (추후 확장을 위한 뼈대)
/// 텍스트 타이핑 연출, 초상화 변경, 대화 중 플레이어 락(Lock) 기능을 담당합니다.
/// </summary>
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    private bool _isPlayingDialogue = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 대화를 시작합니다. (델타룬식 연출의 시작점)
    /// </summary>
    /// <param name="dialogueID">대화 데이터 ID (추후 Json이나 ScriptableObject와 연결)</param>
    /// <param name="onComplete">대화가 끝난 후 실행될 콜백</param>
    public void StartDialogue(string dialogueID, Action onComplete = null)
    {
        if (_isPlayingDialogue) return;

        _isPlayingDialogue = true;
        
        // 1. 매크로 상태를 Dialogue로 변경하여 플레이어 이동 잠금
        GameStateManager.Instance?.ChangeState(GameState.Dialogue);
        
        // 2. UI 패널 열기
        UIManager.Instance.OpenPanel("Dialogue"); // UIManager 딕셔너리에 등록된 이름 사용

        // 3. TODO: 실제 대화 진행 코루틴 시작 (Typewriter 이펙트, 사운드 등)
        Debug.Log($"[DialogueManager] 대화 시작: {dialogueID}");
        
        // 테스트용 임시 종료 처리 (2초 후 대화 종료)
        StartCoroutine(TempDialogueRoutine(onComplete));
    }

    private IEnumerator TempDialogueRoutine(Action onComplete)
    {
        // TODO: 실제로는 Z키를 눌러 다음 대화로 넘어가는 로직이 들어갑니다.
        yield return new WaitForSeconds(2.0f);
        EndDialogue(onComplete);
    }

    public void EndDialogue(Action onComplete = null)
    {
        _isPlayingDialogue = false;
        
        // UI 닫고
        UIManager.Instance.CloseTopPanel();

        // 이동 가능 상태로 복귀
        GameStateManager.Instance?.ChangeState(GameState.Exploration);
        
        onComplete?.Invoke();
    }
}