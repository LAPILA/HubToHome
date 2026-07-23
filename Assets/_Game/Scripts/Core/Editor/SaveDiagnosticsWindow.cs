using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class SaveDiagnosticsWindow : EditorWindow
{
    private static readonly int[] InspectableSlots =
    {
        0,
        1,
        2,
        SaveManager.AutoSlotIndex
    };

    private readonly List<SaveSlotInspection> _inspections =
        new List<SaveSlotInspection>();
    private Vector2 _scroll;

    [MenuItem("Hub To Home/Save/Diagnostics")]
    public static void Open()
    {
        GetWindow<SaveDiagnosticsWindow>("Save Diagnostics");
    }

    public static int[] GetInspectableSlotIndices()
    {
        return (int[])InspectableSlots.Clone();
    }

    public static string GetSlotDisplayName(int slotIndex)
    {
        if (slotIndex == SaveManager.AutoSlotIndex)
            return "자동 슬롯";
        if (slotIndex >= 0 && slotIndex < SaveManager.ManualSlotCount)
            return "수동 슬롯 " + (slotIndex + 1);
        return "슬롯 " + slotIndex;
    }

    private void OnEnable()
    {
        RefreshInspections();
    }

    private void OnFocus()
    {
        RefreshInspections();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Save Diagnostics",
            EditorStyles.boldLabel);
        DrawToolbar();

        EditorGUILayout.Space(6f);
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        for (int i = 0; i < _inspections.Count; i++)
            DrawSlot(_inspections[i]);
        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("새로 고침", EditorStyles.toolbarButton))
            RefreshInspections();
        if (GUILayout.Button("저장 폴더 열기", EditorStyles.toolbarButton))
            RevealSaveDirectory();

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("전체 초기화", EditorStyles.toolbarButton))
            ConfirmDeleteAll();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawSlot(SaveSlotInspection inspection)
    {
        if (inspection == null)
            return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(
            GetSlotDisplayName(inspection.SlotIndex),
            EditorStyles.boldLabel,
            GUILayout.Width(130f));
        EditorGUILayout.LabelField(
            BuildLoadSummary(inspection),
            GUILayout.MinWidth(180f));
        GUILayout.FlexibleSpace();

        bool canDelete = HasAnyCandidate(inspection);
        using (new EditorGUI.DisabledScope(!canDelete))
        {
            if (GUILayout.Button("삭제", GUILayout.Width(52f)))
                ConfirmDeleteSlot(inspection.SlotIndex);
        }

        EditorGUILayout.EndHorizontal();

        DrawCandidate("Primary", inspection.Primary);
        DrawCandidate("Backup", inspection.Backup);
        DrawCandidate("Temporary", inspection.Temporary);
        if (inspection.CorruptExists)
            EditorGUILayout.LabelField("Corrupt", "격리 파일 있음");

        SaveLoadResult load = inspection.LoadResult;
        if (load != null && !string.IsNullOrWhiteSpace(load.Message))
        {
            MessageType type = load.Success
                ? MessageType.Warning
                : load.Failure == SaveLoadFailure.NotFound
                    ? MessageType.None
                    : MessageType.Error;
            if (type != MessageType.None)
                EditorGUILayout.HelpBox(load.Message, type);
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(3f);
    }

    private static void DrawCandidate(
        string label,
        SaveCandidateInspection candidate)
    {
        if (candidate == null)
            return;

        string state;
        if (!candidate.Exists)
        {
            state = "없음";
        }
        else if (!candidate.IsValid)
        {
            state = "오류: " + candidate.Message;
        }
        else
        {
            state = "정상 v" + candidate.SourceVersion;
            if (candidate.WasMigrated)
                state += " → v" + SaveSchema.CurrentVersion;
        }

        EditorGUILayout.LabelField(label, state);
    }

    private static string BuildLoadSummary(SaveSlotInspection inspection)
    {
        SaveLoadResult load = inspection.LoadResult;
        if (load == null || !load.Success)
            return "사용 가능한 저장 없음";

        string source = load.Source == SaveLoadSource.Primary
            ? "정상"
            : "복구 가능: " + load.Source;
        string saveTime = load.Data != null
            ? load.Data.saveTime
            : string.Empty;
        return string.IsNullOrWhiteSpace(saveTime)
            ? source
            : source + " · " + saveTime;
    }

    private static bool HasAnyCandidate(SaveSlotInspection inspection)
    {
        return inspection.CorruptExists
            || inspection.Primary != null && inspection.Primary.Exists
            || inspection.Backup != null && inspection.Backup.Exists
            || inspection.Temporary != null && inspection.Temporary.Exists;
    }

    private void RefreshInspections()
    {
        _inspections.Clear();
        int[] slots = GetInspectableSlotIndices();
        for (int i = 0; i < slots.Length; i++)
            _inspections.Add(SaveManager.InspectSlot(slots[i]));
        Repaint();
    }

    private static void RevealSaveDirectory()
    {
        string path = SaveManager.SaveDirectoryPath;
        if (Directory.Exists(path))
        {
            EditorUtility.RevealInFinder(path);
            return;
        }

        string parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent))
            EditorUtility.RevealInFinder(parent);
        else
            EditorUtility.DisplayDialog(
                "저장 폴더",
                "아직 생성된 저장 폴더가 없습니다.",
                "확인");
    }

    private void ConfirmDeleteSlot(int slotIndex)
    {
        bool confirmed = EditorUtility.DisplayDialog(
            "저장 슬롯 삭제",
            GetSlotDisplayName(slotIndex)
            + "의 Primary, Backup, Temporary, Corrupt 파일을 삭제합니다.",
            "삭제",
            "취소");
        if (!confirmed)
            return;

        SaveStorageResult result = SaveManager.TryDelete(slotIndex);
        if (!result.Success)
        {
            EditorUtility.DisplayDialog(
                "삭제 실패",
                result.Message,
                "확인");
        }

        RefreshInspections();
    }

    private void ConfirmDeleteAll()
    {
        bool confirmed = EditorUtility.DisplayDialog(
            "모든 저장 초기화",
            "수동 슬롯과 자동 슬롯의 모든 저장 후보를 삭제합니다.",
            "전체 삭제",
            "취소");
        if (!confirmed)
            return;

        int[] slots = GetInspectableSlotIndices();
        var failures = new List<string>();
        for (int i = 0; i < slots.Length; i++)
        {
            SaveStorageResult result = SaveManager.TryDelete(slots[i]);
            if (!result.Success)
            {
                failures.Add(
                    GetSlotDisplayName(slots[i]) + ": " + result.Message);
            }
        }

        if (failures.Count > 0)
        {
            EditorUtility.DisplayDialog(
                "일부 삭제 실패",
                string.Join("\n", failures),
                "확인");
        }

        RefreshInspections();
    }
}
