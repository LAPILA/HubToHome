using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

public sealed class SequenceConflictView : VisualElement
{
    public SequenceConflictView()
    {
        AddToClassList("sm-source-safety");
    }

    public event Action ReloadSourceRequested;
    public event Action OverwriteSourceRequested;
    public event Action OpenSourceRequested;
    public event Action<SequenceRecoverySnapshot> RestoreRecoveryRequested;
    public event Action<SequenceRecoverySnapshot> DeleteRecoveryRequested;

    public void Bind(
        SequenceSourceConflict conflict,
        IReadOnlyList<SequenceRecoverySnapshot> recoveries,
        string sourcePath,
        string editorYaml)
    {
        Clear();
        if (conflict != null)
        {
            AddConflict(conflict, sourcePath, editorYaml);
        }
        if (recoveries != null && recoveries.Count > 0)
        {
            AddRecoveries(recoveries);
        }
    }

    private void AddConflict(
        SequenceSourceConflict conflict,
        string sourcePath,
        string editorYaml)
    {
        var panel = new VisualElement();
        panel.AddToClassList("sm-conflict-panel");
        var title = new Label("YAML 외부 변경 충돌");
        title.AddToClassList("sm-source-safety-title");
        panel.Add(title);
        var message = new Label(conflict.Message);
        message.AddToClassList("sm-source-safety-copy");
        panel.Add(message);
        panel.Add(HashRow("에디터가 아는 기준", conflict.ExpectedHash));
        panel.Add(HashRow("현재 디스크", conflict.ActualHash));

        var commands = new VisualElement();
        commands.AddToClassList("sm-source-safety-commands");
        commands.Add(Command("YAML 다시 불러오기", "디스크 내용을 런타임 에셋에 반영", ReloadSourceRequested));
        commands.Add(Command("내 편집으로 덮어쓰기", "외부 변경을 덮어쓰므로 복구 스냅샷을 먼저 남김", OverwriteSourceRequested, true));
        commands.Add(Command("파일 열기", sourcePath, OpenSourceRequested));
        panel.Add(commands);

        var compare = new Foldout { text = "내 편집 YAML 확인", value = false };
        var yaml = new TextField
        {
            multiline = true,
            isReadOnly = true,
            value = editorYaml ?? string.Empty
        };
        yaml.AddToClassList("sm-yaml-field");
        compare.Add(yaml);
        panel.Add(compare);
        Add(panel);
    }

    private void AddRecoveries(IReadOnlyList<SequenceRecoverySnapshot> recoveries)
    {
        var panel = new VisualElement();
        panel.AddToClassList("sm-recovery-panel");
        var title = new Label("복구 가능한 편집 기록");
        title.AddToClassList("sm-source-safety-title");
        panel.Add(title);
        var copy = new Label("저장 전 또는 충돌 처리 전에 Sequence Maker가 로컬 Library에 남긴 기록");
        copy.AddToClassList("sm-source-safety-copy");
        panel.Add(copy);
        for (int i = 0; i < recoveries.Count; i++)
        {
            SequenceRecoverySnapshot snapshot = recoveries[i];
            var row = new VisualElement();
            row.AddToClassList("sm-recovery-row");
            var information = new VisualElement();
            information.style.flexGrow = 1f;
            var timestamp = new Label(snapshot.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
            timestamp.AddToClassList("sm-recovery-time");
            information.Add(timestamp);
            var detail = new Label(snapshot.TargetId + "  ·  " + ShortHash(snapshot.ContentHash));
            detail.AddToClassList("sm-recovery-detail");
            information.Add(detail);
            row.Add(information);
            row.Add(Command("복구", "이 기록을 런타임 에셋에 반영", () => RestoreRecoveryRequested?.Invoke(snapshot)));
            row.Add(Command("삭제", "이 복구 기록 삭제", () => DeleteRecoveryRequested?.Invoke(snapshot), true));
            panel.Add(row);
        }
        Add(panel);
    }

    private static VisualElement HashRow(string label, string hash)
    {
        var row = new VisualElement();
        row.AddToClassList("sm-source-hash-row");
        var name = new Label(label);
        name.AddToClassList("sm-source-hash-name");
        row.Add(name);
        var value = new Label(ShortHash(hash));
        value.AddToClassList("sm-source-hash-value");
        value.tooltip = hash ?? string.Empty;
        row.Add(value);
        return row;
    }

    private static Button Command(
        string text,
        string tooltip,
        Action clicked,
        bool danger = false)
    {
        var button = new Button(clicked) { text = text, tooltip = tooltip ?? string.Empty };
        button.AddToClassList("sm-source-command");
        button.EnableInClassList("is-danger", danger);
        return button;
    }

    private static string ShortHash(string hash)
    {
        return string.IsNullOrWhiteSpace(hash)
            ? "없음"
            : hash.Substring(0, Math.Min(12, hash.Length));
    }
}
