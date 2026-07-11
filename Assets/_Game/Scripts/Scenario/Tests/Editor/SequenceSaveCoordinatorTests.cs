using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class SequenceSaveCoordinatorTests
{
    private readonly DateTime _savedAtUtc = new DateTime(2026, 7, 12, 3, 4, 5, DateTimeKind.Utc);

    [Test]
    public void ExportValidationFailureDoesNotTouchFileOrMetadata()
    {
        var fileSystem = new FakeFileSystem();
        var target = new FakeSaveTarget("Assets/test.sequence.yaml", "new text");
        target.ExportValidation.AddError("export.invalid", "invalid", "target");

        SequenceSaveResult result = Coordinator(fileSystem).Save(target);

        Assert.That(result.Status, Is.EqualTo(SequenceSaveStatus.ValidationFailed));
        Assert.That(fileSystem.WriteCount, Is.EqualTo(0));
        Assert.That(target.MetadataApplied, Is.False);
    }

    [Test]
    public void ExistingFileChangedOutsideEditorReturnsDetailedConflict()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.Files["Assets/test.sequence.yaml"] = "external change";
        var target = new FakeSaveTarget("Assets/test.sequence.yaml", "editor change")
        {
            StoredSourceHash = ScenarioSourceHash.Compute("known baseline")
        };

        SequenceSaveResult result = Coordinator(fileSystem).Save(target);

        Assert.That(result.Status, Is.EqualTo(SequenceSaveStatus.Conflict));
        Assert.That(result.Conflict.Kind, Is.EqualTo(SequenceSourceConflictKind.ModifiedExternally));
        Assert.That(result.Conflict.ExpectedHash, Is.EqualTo(target.StoredSourceHash));
        Assert.That(result.Conflict.ActualHash,
            Is.EqualTo(ScenarioSourceHash.Compute("external change")));
        Assert.That(fileSystem.WriteCount, Is.EqualTo(0));
        Assert.That(target.MetadataApplied, Is.False);
    }

    [Test]
    public void ExistingFileWithoutKnownBaselineRequiresExplicitOverwrite()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.Files["Assets/test.sequence.yaml"] = "untracked source";
        var target = new FakeSaveTarget("Assets/test.sequence.yaml", "editor change");

        SequenceSaveResult result = Coordinator(fileSystem).Save(target);

        Assert.That(result.Status, Is.EqualTo(SequenceSaveStatus.Conflict));
        Assert.That(result.Conflict.Kind, Is.EqualTo(SequenceSourceConflictKind.UntrackedExistingFile));
        Assert.That(fileSystem.Files["Assets/test.sequence.yaml"], Is.EqualTo("untracked source"));
    }

    [Test]
    public void MatchingBaselineUsesTempThenAtomicallyReplacesExistingFile()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.Files["Assets/test.sequence.yaml"] = "baseline";
        var target = new FakeSaveTarget("Assets/test.sequence.yaml", "editor change")
        {
            StoredSourceHash = ScenarioSourceHash.Compute("baseline")
        };

        SequenceSaveResult result = Coordinator(fileSystem).Save(target);

        Assert.That(result.Success, Is.True);
        Assert.That(fileSystem.WriteCount, Is.EqualTo(1));
        Assert.That(fileSystem.ReplaceCount, Is.EqualTo(1));
        Assert.That(fileSystem.MoveCount, Is.EqualTo(0));
        Assert.That(fileSystem.Files["Assets/test.sequence.yaml"], Is.EqualTo("editor change"));
        Assert.That(fileSystem.Files.ContainsKey("Assets/test.sequence.yaml.tmp-test"), Is.False);
    }

    [Test]
    public void NewSourceUsesAtomicMoveAfterTempValidation()
    {
        var fileSystem = new FakeFileSystem();
        var target = new FakeSaveTarget("Assets/new.sequence.yaml", "new source");

        SequenceSaveResult result = Coordinator(fileSystem).Save(target);

        Assert.That(result.Success, Is.True);
        Assert.That(fileSystem.MoveCount, Is.EqualTo(1));
        Assert.That(fileSystem.ReplaceCount, Is.EqualTo(0));
        Assert.That(fileSystem.Files["Assets/new.sequence.yaml"], Is.EqualTo("new source"));
    }

    [Test]
    public void TempFileIsReadBackAndReparsedBeforeReplacement()
    {
        var fileSystem = new FakeFileSystem();
        var target = new FakeSaveTarget("Assets/new.sequence.yaml", "source text");

        SequenceSaveResult result = Coordinator(fileSystem).Save(target);

        Assert.That(result.Success, Is.True);
        Assert.That(target.ValidatedText, Is.EqualTo("source text"));
        Assert.That(target.ValidatedPath, Is.EqualTo("Assets/new.sequence.yaml.tmp-test"));
        Assert.That(target.ValidationCallCount, Is.EqualTo(1));
    }

    [Test]
    public void RoundTripValidationFailureKeepsOriginalAndDeletesTemp()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.Files["Assets/test.sequence.yaml"] = "baseline";
        var target = new FakeSaveTarget("Assets/test.sequence.yaml", "invalid export")
        {
            StoredSourceHash = ScenarioSourceHash.Compute("baseline")
        };
        target.RoundTripValidation.AddError("parse.failed", "bad yaml", "temp");

        SequenceSaveResult result = Coordinator(fileSystem).Save(target);

        Assert.That(result.Status, Is.EqualTo(SequenceSaveStatus.RoundTripFailed));
        Assert.That(fileSystem.Files["Assets/test.sequence.yaml"], Is.EqualTo("baseline"));
        Assert.That(fileSystem.Files.ContainsKey("Assets/test.sequence.yaml.tmp-test"), Is.False);
        Assert.That(target.MetadataApplied, Is.False);
    }

    [Test]
    public void CorruptedTempWriteIsDetectedBeforeReplacement()
    {
        var fileSystem = new FakeFileSystem { CorruptTempWrites = true };
        fileSystem.Files["Assets/test.sequence.yaml"] = "baseline";
        var target = new FakeSaveTarget("Assets/test.sequence.yaml", "editor change")
        {
            StoredSourceHash = ScenarioSourceHash.Compute("baseline")
        };

        SequenceSaveResult result = Coordinator(fileSystem).Save(target);

        Assert.That(result.Status, Is.EqualTo(SequenceSaveStatus.TempVerificationFailed));
        Assert.That(fileSystem.Files["Assets/test.sequence.yaml"], Is.EqualTo("baseline"));
        Assert.That(target.ValidationCallCount, Is.EqualTo(0));
        Assert.That(target.MetadataApplied, Is.False);
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    public void WriteOrReplaceExceptionLeavesMetadataUnchanged(
        bool throwOnWrite,
        bool throwOnReplace)
    {
        var fileSystem = new FakeFileSystem
        {
            ThrowOnWrite = throwOnWrite,
            ThrowOnReplace = throwOnReplace
        };
        fileSystem.Files["Assets/test.sequence.yaml"] = "baseline";
        var target = new FakeSaveTarget("Assets/test.sequence.yaml", "editor change")
        {
            StoredSourceHash = ScenarioSourceHash.Compute("baseline")
        };

        SequenceSaveResult result = Coordinator(fileSystem).Save(target);

        Assert.That(result.Status, Is.EqualTo(SequenceSaveStatus.WriteFailed));
        Assert.That(fileSystem.Files["Assets/test.sequence.yaml"], Is.EqualTo("baseline"));
        Assert.That(target.MetadataApplied, Is.False);
        Assert.That(fileSystem.Files.ContainsKey("Assets/test.sequence.yaml.tmp-test"), Is.False);
    }

    [Test]
    public void ExplicitOverwriteResolvesConflictAndUpdatesMetadataAfterReplace()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.Files["Assets/test.sequence.yaml"] = "external";
        var target = new FakeSaveTarget("Assets/test.sequence.yaml", "editor change")
        {
            StoredSourceHash = ScenarioSourceHash.Compute("baseline")
        };

        SequenceSaveResult result = Coordinator(fileSystem).Save(
            target,
            new SequenceSaveOptions { OverwriteExternalChanges = true });

        Assert.That(result.Success, Is.True);
        Assert.That(target.MetadataApplied, Is.True);
        Assert.That(target.AppliedPath, Is.EqualTo("Assets/test.sequence.yaml"));
        Assert.That(target.AppliedHash, Is.EqualTo(ScenarioSourceHash.Compute("editor change")));
        Assert.That(target.AppliedAtUtc, Is.EqualTo(_savedAtUtc));
        Assert.That(fileSystem.Files["Assets/test.sequence.yaml"], Is.EqualTo("editor change"));
    }

    [Test]
    public void SourceChangedAgainDuringValidationAbortsBeforeAtomicReplace()
    {
        var fileSystem = new FakeFileSystem
        {
            MutatePathOnSecondRead = "Assets/test.sequence.yaml",
            MutationText = "changed while validating"
        };
        fileSystem.Files["Assets/test.sequence.yaml"] = "baseline";
        var target = new FakeSaveTarget("Assets/test.sequence.yaml", "editor change")
        {
            StoredSourceHash = ScenarioSourceHash.Compute("baseline")
        };

        SequenceSaveResult result = Coordinator(fileSystem).Save(target);

        Assert.That(result.Status, Is.EqualTo(SequenceSaveStatus.Conflict));
        Assert.That(result.Conflict.Kind, Is.EqualTo(SequenceSourceConflictKind.ChangedDuringSave));
        Assert.That(fileSystem.ReplaceCount, Is.EqualTo(0));
        Assert.That(fileSystem.Files["Assets/test.sequence.yaml"], Is.EqualTo("changed while validating"));
        Assert.That(target.MetadataApplied, Is.False);
    }

    [Test]
    public void StandaloneTargetRoundTripsAndUpdatesRuntimeMetadata()
    {
        var fileSystem = new FakeFileSystem();
        ActionSequenceAsset sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        sequence.SequenceId = "standalone.save";
        sequence.DisplayNameKo = "저장 테스트";
        sequence.Source.SourcePath = "Assets/standalone.sequence.yaml";
        sequence.Actions.Add(new ScenarioActionData
        {
            BlockId = "wait",
            ActionId = FlowWaitActionAdapter.Id,
            ParametersJson = "{\"duration\":0.25}"
        });
        var target = new StandaloneSequenceSaveTarget(sequence);

        try
        {
            SequenceSaveResult result = Coordinator(fileSystem).Save(target);

            Assert.That(result.Success, Is.True);
            Assert.That(sequence.Source.SourceHash, Is.EqualTo(result.SourceHash));
            Assert.That(sequence.Source.ImportedAtIso8601, Is.EqualTo(_savedAtUtc.ToString("O")));
            ActionSequenceSourceImportResult imported = ActionSequenceSourceSync.Import(
                fileSystem.Files[sequence.Source.SourcePath],
                sequence.Source.SourcePath);
            Assert.That(imported.Success, Is.True);
            UnityEngine.Object.DestroyImmediate(imported.Sequence);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sequence);
        }
    }

    [Test]
    public void BattleScenarioTargetUpdatesScenarioAndSequenceMetadataTogether()
    {
        var fileSystem = new FakeFileSystem();
        BattleScenarioData scenario = ScriptableObject.CreateInstance<BattleScenarioData>();
        ActionSequenceAsset sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        scenario.ScenarioId = "battle.save";
        scenario.TitleKo = "저장 전투";
        scenario.Source.SourcePath = "Assets/battle.scenario.yaml";
        sequence.SequenceId = "battle.save.opening";
        sequence.Actions.Add(new ScenarioActionData
        {
            BlockId = "wait",
            ActionId = FlowWaitActionAdapter.Id,
            ParametersJson = "{\"duration\":0}"
        });
        scenario.Sequences.Add(sequence);
        var target = new BattleScenarioSaveTarget(scenario);

        try
        {
            SequenceSaveResult result = Coordinator(fileSystem).Save(target);

            Assert.That(result.Success, Is.True);
            Assert.That(scenario.Source.SourceHash, Is.EqualTo(result.SourceHash));
            Assert.That(sequence.Source.SourceHash, Is.EqualTo(result.SourceHash));
            Assert.That(sequence.Source.SourcePath, Is.EqualTo(scenario.Source.SourcePath));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sequence);
            UnityEngine.Object.DestroyImmediate(scenario);
        }
    }

    private SequenceSaveCoordinator Coordinator(FakeFileSystem fileSystem)
    {
        return new SequenceSaveCoordinator(
            fileSystem,
            new FixedClock(_savedAtUtc),
            new FixedTempPathProvider());
    }

    private sealed class FakeSaveTarget : ISequenceSaveTarget
    {
        public FakeSaveTarget(string sourcePath, string exportText)
        {
            SourcePath = sourcePath;
            ExportText = exportText;
        }

        public UnityEngine.Object RuntimeAsset => null;
        public string TargetId => "fake";
        public string SourcePath { get; }
        public string StoredSourceHash { get; set; } = string.Empty;
        public string ExportText { get; set; }
        public ScenarioValidationResult ExportValidation { get; } = new ScenarioValidationResult();
        public ScenarioValidationResult RoundTripValidation { get; } = new ScenarioValidationResult();
        public bool MetadataApplied { get; private set; }
        public string AppliedPath { get; private set; } = string.Empty;
        public string AppliedHash { get; private set; } = string.Empty;
        public DateTime AppliedAtUtc { get; private set; }
        public string ValidatedText { get; private set; } = string.Empty;
        public string ValidatedPath { get; private set; } = string.Empty;
        public int ValidationCallCount { get; private set; }

        public SequenceSaveExportResult Export()
        {
            var result = new SequenceSaveExportResult { Text = ExportText };
            result.Validation.Merge(ExportValidation);
            return result;
        }

        public ScenarioValidationResult ValidateRoundTrip(string sourceText, string sourcePath)
        {
            ValidationCallCount++;
            ValidatedText = sourceText;
            ValidatedPath = sourcePath;
            var result = new ScenarioValidationResult();
            result.Merge(RoundTripValidation);
            return result;
        }

        public void ApplySourceMetadata(string sourcePath, string sourceHash, DateTime writtenAtUtc)
        {
            MetadataApplied = true;
            AppliedPath = sourcePath;
            AppliedHash = sourceHash;
            AppliedAtUtc = writtenAtUtc;
        }
    }

    private sealed class FakeFileSystem : ISequenceSourceFileSystem
    {
        public Dictionary<string, string> Files { get; } =
            new Dictionary<string, string>(StringComparer.Ordinal);
        public int WriteCount { get; private set; }
        public int ReplaceCount { get; private set; }
        public int MoveCount { get; private set; }
        public int DeleteCount { get; private set; }
        public bool ThrowOnWrite { get; set; }
        public bool ThrowOnReplace { get; set; }
        public bool CorruptTempWrites { get; set; }
        public string MutatePathOnSecondRead { get; set; } = string.Empty;
        public string MutationText { get; set; } = string.Empty;
        private readonly Dictionary<string, int> _readCounts =
            new Dictionary<string, int>(StringComparer.Ordinal);

        public bool FileExists(string path)
        {
            return Files.ContainsKey(path);
        }

        public string ReadAllText(string path)
        {
            _readCounts.TryGetValue(path, out int readCount);
            readCount++;
            _readCounts[path] = readCount;
            if (readCount == 2
                && string.Equals(path, MutatePathOnSecondRead, StringComparison.Ordinal))
            {
                Files[path] = MutationText;
            }

            return Files[path];
        }

        public void WriteAllText(string path, string text)
        {
            WriteCount++;
            if (ThrowOnWrite)
            {
                throw new InvalidOperationException("write failed");
            }

            Files[path] = CorruptTempWrites ? text + "\ncorrupted" : text;
        }

        public void ReplaceFile(string sourcePath, string destinationPath)
        {
            ReplaceCount++;
            if (ThrowOnReplace)
            {
                throw new InvalidOperationException("replace failed");
            }

            Files[destinationPath] = Files[sourcePath];
            Files.Remove(sourcePath);
        }

        public void MoveFile(string sourcePath, string destinationPath)
        {
            MoveCount++;
            Files[destinationPath] = Files[sourcePath];
            Files.Remove(sourcePath);
        }

        public void DeleteFile(string path)
        {
            DeleteCount++;
            Files.Remove(path);
        }
    }

    private sealed class FixedClock : ISequenceSaveClock
    {
        public FixedClock(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }
    }

    private sealed class FixedTempPathProvider : ISequenceTemporaryPathProvider
    {
        public string Create(string destinationPath)
        {
            return destinationPath + ".tmp-test";
        }
    }
}
