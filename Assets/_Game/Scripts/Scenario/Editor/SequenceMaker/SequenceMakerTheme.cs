using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public static class SequenceMakerTheme
{
    public static void Apply(VisualElement root, SequenceMakerDensity density)
    {
        if (root == null)
        {
            return;
        }

        root.EnableInClassList("sm-dark", EditorGUIUtility.isProSkin);
        root.EnableInClassList("sm-light", !EditorGUIUtility.isProSkin);
        root.EnableInClassList("sm-compact", density == SequenceMakerDensity.Compact);
        root.EnableInClassList("sm-comfortable", density == SequenceMakerDensity.Comfortable);
    }

    public static void SetButtonIcon(Button button, string iconName, string fallbackText = "")
    {
        if (button == null)
        {
            return;
        }

        Texture image = EditorGUIUtility.IconContent(iconName)?.image;
        if (image == null)
        {
            button.text = fallbackText ?? string.Empty;
            return;
        }

        button.text = string.Empty;
        button.Clear();
        button.Add(new Image
        {
            image = image,
            scaleMode = ScaleMode.ScaleToFit
        });
        button[0].AddToClassList("sm-button-icon");
    }

    public static void SetSaveState(VisualElement dot, bool isDirty, bool hasError)
    {
        if (dot == null)
        {
            return;
        }

        dot.EnableInClassList("is-clean", !isDirty && !hasError);
        dot.EnableInClassList("is-dirty", isDirty && !hasError);
        dot.EnableInClassList("is-error", hasError);
    }
}
