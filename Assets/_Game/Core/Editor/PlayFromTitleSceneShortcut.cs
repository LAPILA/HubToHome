#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.ShortcutManagement;
using UnityEngine;

public static class PlayFromTitleSceneShortcut
{
    private const string TitleScenePath = "Assets/_Game/Scenes/Title/00_TitleScene.unity";
    private const string OverworldScenePath = "Assets/_Game/Scenes/OverworldScene.unity";

    [Shortcut("HubToHome/Play From Title Scene", KeyCode.Alpha1, ShortcutModifiers.Alt | ShortcutModifiers.Shift)]
    private static void PlayFromTitleScene()
    {
        PlayFromScene(TitleScenePath, "Title", "Alt+Shift+1");
    }

    [Shortcut("HubToHome/Play From Overworld Scene", KeyCode.Alpha2, ShortcutModifiers.Alt | ShortcutModifiers.Shift)]
    private static void PlayFromOverworldScene()
    {
        PlayFromScene(OverworldScenePath, "Overworld", "Alt+Shift+2");
    }

    private static void PlayFromScene(string scenePath, string sceneLabel, string shortcutLabel)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning($"[PlayFromSceneShortcut] Play Mode is already active or changing. Stop Play Mode before using {shortcutLabel}.");
            return;
        }

        if (!File.Exists(scenePath))
        {
            Debug.LogError($"[PlayFromSceneShortcut] {sceneLabel} scene not found: {scenePath}");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
    }
}
#endif
