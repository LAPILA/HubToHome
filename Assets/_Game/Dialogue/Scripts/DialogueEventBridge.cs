using UnityEngine;

/// <summary>
/// 대화 도중 또는 종료 시 특정 게임 로직을 실행하는 가교 클래스.
/// DialogueManager의 명령 문자열을 파싱하여 실제 함수를 호출합니다.
/// 
/// 지원 명령:
///   GIVE_ITEM:[ItemID]              → 아이템 지급
///   START_BATTLE:[EnemyGroupID]     → 전투 씬 전환
///   SET_FLAG:[FlagName]:[Value]     → 이벤트 플래그 설정
///   LOAD_SCENE:[SceneName]          → 씬 전환
/// </summary>
public static class DialogueEventBridge
{
    public static void Execute(string eventID)
    {
        if (string.IsNullOrEmpty(eventID)) return;

        string[] parts = eventID.Split(':');
        string command = parts[0].ToUpper();

        switch (command)
        {
            case "GIVE_ITEM":
                if (parts.Length > 1)
                    ExecuteGiveItem(parts[1]);
                break;

            case "START_BATTLE":
                if (parts.Length > 1)
                    ExecuteStartBattle(parts[1]);
                break;

            case "SET_FLAG":
                if (parts.Length > 2 && int.TryParse(parts[2], out int flagValue))
                    ExecuteSetFlag(parts[1], flagValue);
                break;

            case "LOAD_SCENE":
                if (parts.Length > 1)
                    SceneLoader.Instance?.LoadScene(parts[1]);
                break;

            default:
                Debug.LogWarning($"[DialogueEventBridge] Unknown command: {eventID}");
                break;
        }
    }

    private static void ExecuteGiveItem(string itemID)
    {
        GlobalDataManager.Instance?.AddItem(itemID);
        Debug.Log($"[DialogueEventBridge] Item given: {itemID}");
    }

    private static void ExecuteStartBattle(string enemyGroupID)
    {
        // TODO: GlobalDataManager에 적 그룹 ID 저장
        Debug.Log($"[DialogueEventBridge] Starting battle: {enemyGroupID}");
        SceneLoader.Instance?.LoadBattleScene(SceneName.Battle);
    }

    private static void ExecuteSetFlag(string flagName, int value)
    {
        GlobalDataManager.Instance?.SetFlag(flagName, value);
        Debug.Log($"[DialogueEventBridge] Flag set: {flagName} = {value}");
    }
}
