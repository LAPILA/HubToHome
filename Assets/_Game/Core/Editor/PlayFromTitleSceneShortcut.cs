#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.ShortcutManagement;
using UnityEngine;

public static class PlayFromTitleSceneShortcut
{
    private const string TitleScenePath = "Assets/_Game/Scenes/Title/00_TitleScene.unity";

    [Shortcut("HubToHome/Play From Title Scene", KeyCode.P, ShortcutModifiers.Alt | ShortcutModifiers.Shift)]
    private static void PlayFromTitleScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[PlayFromTitleScene] Play Mode is already active or changing. Stop Play Mode before using Alt+Shift+P.");
            return;
        }

        if (!File.Exists(TitleScenePath))
        {
            Debug.LogError($"[PlayFromTitleScene] Title scene not found: {TitleScenePath}");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
    }
}
#endif
