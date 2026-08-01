using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(
    fileName = "SequencePuzzleDefinition",
    menuName = "Hub To Home/Overworld/Sequence Puzzle Definition")]
public sealed class SequencePuzzleDefinition : ScriptableObject
{
    [TitleGroup("기본 정보")]
    [SerializeField, Required, LabelText("퍼즐 ID")]
    private string _puzzleId;

    [TitleGroup("입력 순서")]
    [SerializeField, ListDrawerSettings(ShowIndexLabels = true), LabelText("정답 Node ID")]
    private List<string> _orderedNodeIds = new List<string>();

    [TitleGroup("완료 상태")]
    [SerializeField, Required, LabelText("완료 Flag ID")]
    private string _completionFlag;

    [TitleGroup("오답 처리")]
    [SerializeField, Min(0f), LabelText("초기화 지연(초)")]
    private float _incorrectResetDelay = 0.6f;

    public string PuzzleId => Normalize(_puzzleId);
    public IReadOnlyList<string> OrderedNodeIds => _orderedNodeIds;
    public string CompletionFlag => Normalize(_completionFlag);
    public float IncorrectResetDelay => Mathf.Max(0f, _incorrectResetDelay);

    public void Configure(
        string puzzleId,
        IEnumerable<string> orderedNodeIds,
        string completionFlag,
        float incorrectResetDelay)
    {
        _puzzleId = Normalize(puzzleId);
        _orderedNodeIds = new List<string>();
        if (orderedNodeIds != null)
        {
            foreach (string nodeId in orderedNodeIds)
                _orderedNodeIds.Add(Normalize(nodeId));
        }

        _completionFlag = Normalize(completionFlag);
        _incorrectResetDelay = Mathf.Max(0f, incorrectResetDelay);
    }

    public bool TryValidate(out string error)
    {
        if (string.IsNullOrEmpty(PuzzleId))
        {
            error = "Puzzle ID가 비어 있습니다.";
            return false;
        }

        if (string.IsNullOrEmpty(CompletionFlag))
        {
            error = "완료 Flag ID가 비어 있습니다.";
            return false;
        }

        if (_orderedNodeIds == null || _orderedNodeIds.Count == 0)
        {
            error = "정답 Node ID가 하나 이상 필요합니다.";
            return false;
        }

        var uniqueIds = new HashSet<string>(System.StringComparer.Ordinal);
        for (int i = 0; i < _orderedNodeIds.Count; i++)
        {
            string nodeId = Normalize(_orderedNodeIds[i]);
            if (string.IsNullOrEmpty(nodeId))
            {
                error = $"정답 Node ID #{i + 1}이 비어 있습니다.";
                return false;
            }

            if (!uniqueIds.Add(nodeId))
            {
                error = $"정답 Node ID가 중복됩니다: {nodeId}";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    [TitleGroup("검증")]
    [Button("Definition 검증")]
    private void ValidateAndLog()
    {
        if (TryValidate(out string error))
            Debug.Log($"[SequencePuzzleDefinition] 검증 통과: {PuzzleId}", this);
        else
            Debug.LogError($"[SequencePuzzleDefinition] {error}", this);
    }

    private void OnValidate()
    {
        _puzzleId = Normalize(_puzzleId);
        _completionFlag = Normalize(_completionFlag);
        _incorrectResetDelay = Mathf.Max(0f, _incorrectResetDelay);
        if (_orderedNodeIds == null)
            _orderedNodeIds = new List<string>();

        for (int i = 0; i < _orderedNodeIds.Count; i++)
            _orderedNodeIds[i] = Normalize(_orderedNodeIds[i]);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}