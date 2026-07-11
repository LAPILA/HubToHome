using System;

public enum SequenceSourceConflictKind
{
    None,
    ModifiedExternally,
    UntrackedExistingFile,
    ChangedDuringSave
}

public sealed class SequenceSourceConflict
{
    public SequenceSourceConflict(
        SequenceSourceConflictKind kind,
        string sourcePath,
        string expectedHash,
        string actualHash,
        string message)
    {
        Kind = kind;
        SourcePath = sourcePath ?? string.Empty;
        ExpectedHash = expectedHash ?? string.Empty;
        ActualHash = actualHash ?? string.Empty;
        Message = message ?? string.Empty;
    }

    public SequenceSourceConflictKind Kind { get; }
    public string SourcePath { get; }
    public string ExpectedHash { get; }
    public string ActualHash { get; }
    public string Message { get; }

    public static SequenceSourceConflict Detect(
        string sourcePath,
        string storedSourceHash,
        string currentSourceText)
    {
        string actualHash = ScenarioSourceHash.Compute(currentSourceText ?? string.Empty);
        string expectedHash = Normalize(storedSourceHash);
        if (string.IsNullOrEmpty(expectedHash))
        {
            return new SequenceSourceConflict(
                SequenceSourceConflictKind.UntrackedExistingFile,
                sourcePath,
                string.Empty,
                actualHash,
                "기존 YAML의 기준 해시가 없어 자동으로 덮어쓸 수 없습니다.");
        }

        if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
        {
            return new SequenceSourceConflict(
                SequenceSourceConflictKind.ModifiedExternally,
                sourcePath,
                expectedHash,
                actualHash,
                "YAML이 Sequence Maker 밖에서 변경되었습니다.");
        }

        return null;
    }

    public static SequenceSourceConflict ChangedAfterValidation(
        string sourcePath,
        string expectedDiskHash,
        string actualDiskHash)
    {
        return new SequenceSourceConflict(
            SequenceSourceConflictKind.ChangedDuringSave,
            sourcePath,
            expectedDiskHash,
            actualDiskHash,
            "검증 중 YAML이 다시 변경되어 저장을 중단했습니다.");
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
