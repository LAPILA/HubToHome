#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>사용자 명령으로만 실행되는 종합 샘플 진입점. 자동 생성/자동 Play는 하지 않습니다.</summary>
public static class BunnySlimeBattleLabBuilder
{
    public const string Root = "Assets/_Game/Content/Maps/Development/BunnySlimeBattleLab";

    [MenuItem("Hub To Home/테스트/토끼 슬라임 종합 전투 열기", false, 140)]
    public static void Open()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Play를 종료한 뒤 토끼 슬라임 전투 실험실을 열어 주세요.");
            return;
        }
        // 다른 씬의 저장되지 않은 작업을 보호합니다. 사용자가 메뉴를 누른 시점에만 확인합니다.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        try
        {
            BunnySlimeBattleLabData data = BunnySlimeLabContentBuilder.Build(Root);
            if (data.Scenario == null)
            {
                data.Scenario = BunnySlimeLabStoryBuilder.Build(Root, data.Enemy);
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssetIfDirty(data);
            }
            string path = BunnySlimeLabSceneBuilder.Build(Root, data);
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Selection.activeObject = data;
            Debug.Log("토끼 슬라임 전투 실험실 준비 완료. Play → ↑↓ 선택 → Z 시작. 기존 생성 자산은 덮어쓰지 않습니다.", data);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("토끼 슬라임 전투 실험실", "생성을 완료하지 못했습니다. 기존 원본은 변경하지 않았습니다.\n\n" + exception.Message, "확인");
        }
    }
}
#endif
