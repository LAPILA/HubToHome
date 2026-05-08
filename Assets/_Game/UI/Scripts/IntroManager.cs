using UnityEngine;
using System.Collections;

public class IntroManager : MonoBehaviour
{
    [Header("인트로 데이터")]
    [SerializeField] private DialogueData _introPart1; 
    [SerializeField] private DialogueData _introPart2; 
    
    [Header("다음 스테이지")]
    [SerializeField] private string _nextSceneName = "02_OverworldScene";

    private NameInputUI _nameInput;
    private string _originalPart2Text;

    private IEnumerator Start()
    {
        // 글로벌 매니저에서 UI 찾기
        _nameInput = DialogueManager.Instance.GetComponentInChildren<NameInputUI>(true);

        if (_nameInput == null)
        {
            Debug.LogError("⚠️ NameInputUI를 찾을 수 없습니다!");
            yield break;
        }

        // 텍스트 백업 (영구 변조 방지)
        if (_introPart2 != null && _introPart2.Nodes.Count > 0)
            _originalPart2Text = _introPart2.Nodes[0].DefaultText;

        yield return new WaitForSeconds(1.0f);

        // [파트 1 시작]
        DialogueManager.Instance.StartDialogue(_introPart1, () => {
            // 대화 종료 후 이름 입력창 오픈
            _nameInput.Open(OnNameConfirmed);
        });
    }

    private void OnNameConfirmed(string playerName)
    {
        Debug.Log($"입력된 이름: {playerName}");
        
        if (GlobalDataManager.Instance != null)
            GlobalDataManager.Instance.PlayerName = playerName;

        // 이름 치환
        if (_introPart2 != null && _introPart2.Nodes.Count > 0)
        {
            _introPart2.Nodes[0].DefaultText = string.Format(_originalPart2Text, playerName);
        }

        // [파트 2 시작]
        DialogueManager.Instance.StartDialogue(_introPart2, () => {
            // 복구
            if (_introPart2 != null && _introPart2.Nodes.Count > 0)
                _introPart2.Nodes[0].DefaultText = _originalPart2Text;

            SceneLoader.Instance?.LoadScene(_nextSceneName);
        });
    }
}