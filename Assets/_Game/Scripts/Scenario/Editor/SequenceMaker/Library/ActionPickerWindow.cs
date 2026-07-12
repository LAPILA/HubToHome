using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class ActionPickerWindow : EditorWindow
{
    public const string MenuPath = "HubToHome/시나리오/액션 라이브러리";

    [SerializeField] private ActionCatalogAsset _catalog;
    private ActionPickerContext _context;
    private ActionPickerHistory _history;
    private Action<ActionCatalogEntry> _onPicked;
    private string _commandLabel = "액션 추가";
    private string _heading = "액션 라이브러리";
    private Func<string, int> _usageCount;

    [MenuItem(MenuPath)]
    public static void OpenLibrary()
    {
        ActionCatalogAsset catalog = AssetDatabase.LoadAssetAtPath<ActionCatalogAsset>(
            ProductionActionLibraryBuildCommand.GeneratedAssetPath);
        ShowPicker(
            null,
            catalog,
            new ActionPickerContext(string.Empty),
            new ActionPickerHistory(new EditorSequenceMakerPreferences()),
            "액션 라이브러리",
            string.Empty,
            null);
    }

    public static ActionPickerWindow ShowPicker(
        EditorWindow owner,
        ActionCatalogAsset catalog,
        ActionPickerContext context,
        ActionPickerHistory history,
        string heading,
        string commandLabel,
        Action<ActionCatalogEntry> onPicked,
        Func<string, int> usageCount = null)
    {
        ActionPickerWindow window = CreateInstance<ActionPickerWindow>();
        window._catalog = catalog;
        window._context = context ?? new ActionPickerContext(string.Empty);
        window._history = history ?? new ActionPickerHistory(new EditorSequenceMakerPreferences());
        window._heading = string.IsNullOrWhiteSpace(heading) ? "액션 라이브러리" : heading.Trim();
        window._commandLabel = string.IsNullOrWhiteSpace(commandLabel) ? "액션 선택" : commandLabel.Trim();
        window._onPicked = onPicked;
        window._usageCount = usageCount;
        window.titleContent = new GUIContent(window._heading);
        window.minSize = new Vector2(820f, 560f);
        window.maxSize = new Vector2(1320f, 920f);
        Rect ownerRect = owner != null ? owner.position : new Rect(180f, 120f, 1100f, 760f);
        window.position = new Rect(
            ownerRect.x + Math.Max(24f, (ownerRect.width - 1040f) * 0.5f),
            ownerRect.y + 56f,
            Math.Min(1040f, Math.Max(820f, ownerRect.width - 48f)),
            Math.Min(720f, Math.Max(560f, ownerRect.height - 96f)));
        window.ShowUtility();
        window.Focus();
        return window;
    }

    public void CreateGUI()
    {
        rootVisualElement.Clear();
        StyleSheet style = AssetDatabase.LoadAssetAtPath<StyleSheet>(SequenceMakerWindow.UssPath);
        if (style != null)
        {
            rootVisualElement.styleSheets.Add(style);
        }
        rootVisualElement.AddToClassList("sm-library-window");

        var heading = new VisualElement();
        heading.AddToClassList("sm-library-window-heading");
        var title = new Label(_heading);
        title.AddToClassList("sm-library-window-title");
        heading.Add(title);
        var copy = new Label(_onPicked == null
            ? "Action Library 전체를 검색하고 사용법과 파라미터를 확인"
            : "추가할 액션을 검색하거나 카테고리에서 선택");
        copy.AddToClassList("sm-library-window-copy");
        heading.Add(copy);
        rootVisualElement.Add(heading);

        if (_catalog == null)
        {
            var missing = new Label("공식 Action Library 에셋을 찾지 못했습니다.");
            missing.AddToClassList("sm-library-missing");
            rootVisualElement.Add(missing);
            return;
        }

        var library = new ActionLibraryView();
        library.Picked += entry =>
        {
            _onPicked?.Invoke(entry);
            Close();
        };
        library.Bind(
            _catalog,
            _context,
            _history,
            _onPicked != null,
            _commandLabel,
            _usageCount);
        rootVisualElement.Add(library);
    }
}
