using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class SequenceDeletionCoordinatorTests
{
    private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();
    private readonly List<string> _assetFolders = new List<string>();

    [TearDown]
    public void TearDown()
    {
        for (int i = _assetFolders.Count - 1; i >= 0; i--)
        {
            AssetDatabase.DeleteAsset(_assetFolders[i]);
        }
        _assetFolders.Clear();

        for (int i = _created.Count - 1; i >= 0; i--)
        {
            if (_created[i] != null && !EditorUtility.IsPersistent(_created[i]))
            {
                UnityEngine.Object.DestroyImmediate(_created[i]);
            }
        }

        _created.Clear();
    }

    [Test]
    public void BattleOwnershipAloneDoesNotBlockDeletion()
    {
        ActionSequenceAsset sequence = Sequence("battle.opening", "Assets/battle.scenario.yaml");
        BattleScenarioData battle = Battle("battle", "Assets/battle.scenario.yaml", sequence);
        SequenceUsageIndex usage = BuildUsage(battle, sequence);

        SequenceDeletionAnalysis analysis = SequenceDeletionCoordinator.Analyze(
            sequence,
            battle,
            usage);

        Assert.That(analysis.Kind, Is.EqualTo(SequenceDeletionKind.BattleOwned));
        Assert.That(analysis.CanDelete, Is.True);
        Assert.That(analysis.BlockingUsages, Is.Empty);
    }

    [TestCase(SequenceUsageKind.TriggerRule)]
    [TestCase(SequenceUsageKind.LegacyBattleRule)]
    [TestCase(SequenceUsageKind.SequenceCall)]
    public void RuntimeReferencesBlockDeletion(SequenceUsageKind kind)
    {
        ActionSequenceAsset target = Sequence("target", "Assets/battle.scenario.yaml");
        ActionSequenceAsset caller = Sequence("caller", "Assets/battle.scenario.yaml");
        BattleScenarioData battle = Battle("battle", "Assets/battle.scenario.yaml", target, caller);
        if (kind == SequenceUsageKind.TriggerRule)
        {
            battle.TriggerRules.Add(new ScenarioTriggerRuleData { RuleId = "trigger", SequenceId = "target" });
        }
        else if (kind == SequenceUsageKind.LegacyBattleRule)
        {
            battle.Rules.Add(new BattleEventRuleData { RuleId = "legacy", SequenceId = "target" });
        }
        else
        {
            caller.Actions.Add(new ScenarioActionData
            {
                BlockId = "call",
                ActionId = SequenceCallActionAdapter.Id,
                ParametersJson = "{\"sequence\":\"target\"}"
            });
        }

        SequenceDeletionAnalysis analysis = SequenceDeletionCoordinator.Analyze(
            target,
            battle,
            BuildUsage(battle, target, caller));

        Assert.That(analysis.CanDelete, Is.False);
        Assert.That(analysis.BlockingUsages, Has.Count.EqualTo(1));
        Assert.That(analysis.BlockingUsages[0].Kind, Is.EqualTo(kind));
    }

    [Test]
    public void OwnershipByAnotherBattleBlocksDeletion()
    {
        ActionSequenceAsset target = Sequence("target", "Assets/one.scenario.yaml");
        BattleScenarioData current = Battle("one", "Assets/one.scenario.yaml", target);
        BattleScenarioData other = Battle("two", "Assets/two.scenario.yaml", target);
        SequenceAssetIndex assets = SequenceAssetIndex.Build(
            new[] { current, other },
            new[] { target });

        SequenceDeletionAnalysis analysis = SequenceDeletionCoordinator.Analyze(
            target,
            current,
            SequenceUsageIndex.Build(assets));

        Assert.That(analysis.CanDelete, Is.False);
        Assert.That(analysis.BlockingUsages, Has.Count.EqualTo(1));
        Assert.That(analysis.BlockingUsages[0].SourceScenario, Is.SameAs(other));
    }

    [Test]
    public void DuplicateOwnershipInsideCurrentBattleBlocksDeletion()
    {
        ActionSequenceAsset target = Sequence("target", "Assets/battle.scenario.yaml");
        BattleScenarioData battle = Battle(
            "battle",
            "Assets/battle.scenario.yaml",
            target,
            target);

        SequenceDeletionAnalysis analysis = SequenceDeletionCoordinator.Analyze(
            target,
            battle,
            BuildUsage(battle, target));

        Assert.That(analysis.CanDelete, Is.False);
        Assert.That(analysis.BlockingReasons, Has.Some.Contains("중복"));
    }

    [Test]
    public void MissingUsageIndexBlocksDeletion()
    {
        ActionSequenceAsset target = Sequence("target", "Assets/target.sequence.yaml");

        SequenceDeletionAnalysis analysis = SequenceDeletionCoordinator.Analyze(
            target,
            null,
            null);

        Assert.That(analysis.CanDelete, Is.False);
        Assert.That(analysis.BlockingReasons, Has.Some.Contains("사용 위치"));
    }

    [TestCase("", "Assets/sequence.yaml", "Sequence ID")]
    [TestCase("standalone", "", "YAML")]
    public void InvalidIdentityOrSourcePathBlocksDeletion(
        string sequenceId,
        string sourcePath,
        string expectedReason)
    {
        ActionSequenceAsset sequence = Sequence(sequenceId, sourcePath);

        SequenceDeletionAnalysis analysis = SequenceDeletionCoordinator.Analyze(
            sequence,
            null,
            BuildUsage(null, sequence));

        Assert.That(analysis.CanDelete, Is.False);
        Assert.That(analysis.BlockingReasons, Has.Some.Contains(expectedReason));
    }

    [Test]
    public void BattleDeletionCapturesRecoverySavesYamlThenDeletesRuntimeAsset()
    {
        var events = new List<string>();
        ActionSequenceAsset before = Sequence("before", "Assets/battle.scenario.yaml");
        ActionSequenceAsset target = Sequence("target", "Assets/battle.scenario.yaml");
        ActionSequenceAsset after = Sequence("after", "Assets/battle.scenario.yaml");
        BattleScenarioData battle = Battle(
            "battle",
            "Assets/battle.scenario.yaml",
            before,
            target,
            after);
        var saves = new FakeSaveService(events);
        var recovery = new FakeRecovery(events);
        var assets = new FakeAssetStore(events);
        var coordinator = new SequenceDeletionCoordinator(saves, recovery, assets);

        SequenceDeletionResult result = coordinator.Delete(
            target,
            battle,
            BuildUsage(battle, before, target, after));

        Assert.That(result.Success, Is.True);
        Assert.That(events, Is.EqualTo(new[] { "recovery", "save", "delete-runtime" }));
        Assert.That(battle.Sequences, Is.EqualTo(new[] { before, after }));
        Assert.That(assets.DeletedRuntimeAsset, Is.SameAs(target));
    }

    [Test]
    public void BattleSaveFailureRestoresSequenceAtExactIndexAndKeepsRuntimeAsset()
    {
        var events = new List<string>();
        ActionSequenceAsset before = Sequence("before", "Assets/battle.scenario.yaml");
        ActionSequenceAsset target = Sequence("target", "Assets/battle.scenario.yaml");
        ActionSequenceAsset after = Sequence("after", "Assets/battle.scenario.yaml");
        BattleScenarioData battle = Battle(
            "battle",
            "Assets/battle.scenario.yaml",
            before,
            target,
            after);
        var saves = new FakeSaveService(events) { SaveSucceeds = false };
        var recovery = new FakeRecovery(events);
        var assets = new FakeAssetStore(events);
        var coordinator = new SequenceDeletionCoordinator(saves, recovery, assets);

        SequenceDeletionResult result = coordinator.Delete(
            target,
            battle,
            BuildUsage(battle, before, target, after));

        Assert.That(result.Status, Is.EqualTo(SequenceDeletionStatus.SaveFailed));
        Assert.That(battle.Sequences, Is.EqualTo(new[] { before, target, after }));
        Assert.That(assets.DeletedRuntimeAsset, Is.Null);
        Assert.That(events, Is.EqualTo(new[] { "recovery", "save" }));
    }

    [Test]
    public void StandaloneDeletionBlocksWhenDiskHashChanged()
    {
        var events = new List<string>();
        ActionSequenceAsset target = Sequence("standalone", "Assets/standalone.sequence.yaml");
        target.Source.SourceHash = ScenarioSourceHash.Compute("known source");
        var assets = new FakeAssetStore(events) { SourceText = "external change" };
        var coordinator = new SequenceDeletionCoordinator(
            new FakeSaveService(events),
            new FakeRecovery(events),
            assets);

        SequenceDeletionResult result = coordinator.Delete(
            target,
            null,
            BuildUsage(null, target));

        Assert.That(result.Status, Is.EqualTo(SequenceDeletionStatus.Conflict));
        Assert.That(events, Is.Empty);
    }

    [Test]
    public void StandaloneSourceDeleteFailureKeepsRuntimeAsset()
    {
        var events = new List<string>();
        ActionSequenceAsset target = Standalone("source");
        var assets = new FakeAssetStore(events)
        {
            SourceText = "source",
            DeleteSourceSucceeds = false
        };
        var coordinator = new SequenceDeletionCoordinator(
            new FakeSaveService(events),
            new FakeRecovery(events),
            assets);

        SequenceDeletionResult result = coordinator.Delete(
            target,
            null,
            BuildUsage(null, target));

        Assert.That(result.Status, Is.EqualTo(SequenceDeletionStatus.SourceDeleteFailed));
        Assert.That(assets.DeletedRuntimeAsset, Is.Null);
        Assert.That(events, Is.EqualTo(new[] { "recovery", "delete-source" }));
    }

    [Test]
    public void StandaloneRuntimeDeleteFailureRestoresExactSourceBytes()
    {
        var events = new List<string>();
        ActionSequenceAsset target = Standalone("source\r\nwith exact newlines\r\n");
        var assets = new FakeAssetStore(events)
        {
            SourceText = "source\r\nwith exact newlines\r\n",
            DeleteRuntimeSucceeds = false
        };
        var coordinator = new SequenceDeletionCoordinator(
            new FakeSaveService(events),
            new FakeRecovery(events),
            assets);

        SequenceDeletionResult result = coordinator.Delete(
            target,
            null,
            BuildUsage(null, target));

        Assert.That(result.Status, Is.EqualTo(SequenceDeletionStatus.RuntimeAssetDeleteFailed));
        Assert.That(assets.RestoredBytes, Is.EqualTo(assets.CapturedBytes));
        Assert.That(events, Is.EqualTo(new[]
        {
            "recovery", "delete-source", "delete-runtime", "restore-source"
        }));
    }

    [Test]
    public void StandaloneSuccessDeletesSourceThenRuntimeAsset()
    {
        var events = new List<string>();
        ActionSequenceAsset target = Standalone("source");
        var assets = new FakeAssetStore(events) { SourceText = "source" };
        var coordinator = new SequenceDeletionCoordinator(
            new FakeSaveService(events),
            new FakeRecovery(events),
            assets);

        SequenceDeletionResult result = coordinator.Delete(
            target,
            null,
            BuildUsage(null, target));

        Assert.That(result.Success, Is.True);
        Assert.That(events, Is.EqualTo(new[]
        {
            "recovery", "delete-source", "delete-runtime"
        }));
    }

    [Test]
    public void RealAssetDatabaseDeletesStandaloneYamlAndRuntimeAsset()
    {
        string root = CreateAssetFolder();
        string sourcePath = root + "/standalone.sequence.yaml";
        string runtimePath = root + "/standalone.asset";
        ActionSequenceAsset target = Sequence("integration.standalone", sourcePath);
        AssetDatabase.CreateAsset(target, runtimePath);
        ActionSequenceSourceExportResult exported = ActionSequenceSourceSync.Export(target);
        Assert.That(exported.Success, Is.True);
        WriteAssetText(sourcePath, exported.Text);
        target.Source.SourceHash = ScenarioSourceHash.Compute(exported.Text);
        EditorUtility.SetDirty(target);
        AssetDatabase.SaveAssets();

        SequenceDeletionResult result = RealAssetCoordinator().Delete(
            target,
            null,
            BuildUsage(null, target));

        Assert.That(result.Success, Is.True, result.ErrorMessage);
        Assert.That(AssetDatabase.LoadAssetAtPath<ActionSequenceAsset>(runtimePath), Is.Null);
        Assert.That(File.Exists(Path.GetFullPath(sourcePath)), Is.False);
    }

    [Test]
    public void RealAssetDatabaseDeletesBattleSubAssetAfterYamlSave()
    {
        string root = CreateAssetFolder();
        string sourcePath = root + "/battle.scenario.yaml";
        string runtimePath = root + "/battle.asset";
        ActionSequenceAsset target = Sequence("integration.battle.phase", sourcePath);
        BattleScenarioData battle = Battle("integration.battle", sourcePath, target);
        battle.TitleKo = "삭제 통합 테스트";
        AssetDatabase.CreateAsset(battle, runtimePath);
        AssetDatabase.AddObjectToAsset(target, battle);
        AssetDatabase.SaveAssets();
        ScenarioSourceYamlExportResult exported =
            new ScenarioSourceYamlExportCommand().ExportToText(battle);
        Assert.That(exported.Success, Is.True);
        WriteAssetText(sourcePath, exported.Text);
        string hash = ScenarioSourceHash.Compute(exported.Text);
        battle.Source.SourceHash = hash;
        target.Source.SourceHash = hash;
        EditorUtility.SetDirty(battle);
        EditorUtility.SetDirty(target);
        AssetDatabase.SaveAssets();

        SequenceDeletionResult result = RealAssetCoordinator().Delete(
            target,
            battle,
            BuildUsage(battle, target));

        Assert.That(result.Success, Is.True, result.ErrorMessage);
        Assert.That(battle.Sequences, Is.Empty);
        Assert.That(Array.Exists(
            AssetDatabase.LoadAllAssetsAtPath(runtimePath),
            asset => asset is ActionSequenceAsset), Is.False);
        Assert.That(File.ReadAllText(Path.GetFullPath(sourcePath)), Does.Not.Contain("integration.battle.phase"));
    }

    private SequenceUsageIndex BuildUsage(
        BattleScenarioData battle,
        params ActionSequenceAsset[] sequences)
    {
        return SequenceUsageIndex.Build(SequenceAssetIndex.Build(
            battle != null ? new[] { battle } : Array.Empty<BattleScenarioData>(),
            sequences));
    }

    private ActionSequenceAsset Sequence(string id, string sourcePath)
    {
        ActionSequenceAsset sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        sequence.SequenceId = id;
        sequence.DisplayNameKo = string.IsNullOrEmpty(id) ? "빈 시퀀스" : id;
        sequence.Source.SourcePath = sourcePath;
        sequence.name = id;
        _created.Add(sequence);
        return sequence;
    }

    private ActionSequenceAsset Standalone(string sourceText)
    {
        ActionSequenceAsset sequence = Sequence(
            "standalone",
            "Assets/standalone.sequence.yaml");
        sequence.Source.SourceHash = ScenarioSourceHash.Compute(sourceText);
        return sequence;
    }

    private BattleScenarioData Battle(
        string id,
        string sourcePath,
        params ActionSequenceAsset[] sequences)
    {
        BattleScenarioData battle = ScriptableObject.CreateInstance<BattleScenarioData>();
        battle.ScenarioId = id;
        battle.Source.SourcePath = sourcePath;
        battle.Sequences.AddRange(sequences);
        battle.name = id;
        _created.Add(battle);
        return battle;
    }

    private string CreateAssetFolder()
    {
        string name = "__SequenceDeletionTests_" + Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets", name);
        string path = "Assets/" + name;
        _assetFolders.Add(path);
        return path;
    }

    private static SequenceDeletionCoordinator RealAssetCoordinator()
    {
        return new SequenceDeletionCoordinator(
            new SequenceDeletionSaveService(),
            new NoOpDeletionRecovery(),
            new AssetDatabaseSequenceDeletionStore());
    }

    private static void WriteAssetText(string path, string text)
    {
        File.WriteAllText(Path.GetFullPath(path), text ?? string.Empty, new UTF8Encoding(false));
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
    }

    private sealed class FakeSaveService : ISequenceDeletionSaveService
    {
        private readonly List<string> _events;

        public FakeSaveService(List<string> events)
        {
            _events = events;
        }

        public bool SaveSucceeds { get; set; } = true;

        public SequenceSaveResult Save(ISequenceSaveTarget target)
        {
            _events.Add("save");
            return new SequenceSaveResult
            {
                Status = SaveSucceeds
                    ? SequenceSaveStatus.Succeeded
                    : SequenceSaveStatus.WriteFailed,
                ErrorMessage = SaveSucceeds ? string.Empty : "save failed"
            };
        }
    }

    private sealed class NoOpDeletionRecovery : ISequenceDeletionRecovery
    {
        public void Capture(ISequenceSaveTarget target)
        {
        }
    }

    private sealed class FakeRecovery : ISequenceDeletionRecovery
    {
        private readonly List<string> _events;

        public FakeRecovery(List<string> events)
        {
            _events = events;
        }

        public void Capture(ISequenceSaveTarget target)
        {
            _events.Add("recovery");
        }
    }

    private sealed class FakeAssetStore : ISequenceDeletionAssetStore
    {
        private readonly List<string> _events;

        public FakeAssetStore(List<string> events)
        {
            _events = events;
        }

        public UnityEngine.Object DeletedRuntimeAsset { get; private set; }
        public string SourceText { get; set; } = "source";
        public bool SourceExistsValue { get; set; } = true;
        public bool DeleteSourceSucceeds { get; set; } = true;
        public bool DeleteRuntimeSucceeds { get; set; } = true;
        public bool RestoreSourceSucceeds { get; set; } = true;
        public byte[] CapturedBytes { get; private set; }
        public byte[] RestoredBytes { get; private set; }

        public string GetAssetPath(UnityEngine.Object asset) => "Assets/runtime.asset";
        public bool IsSubAsset(UnityEngine.Object asset) => true;
        public bool SourceExists(string path) => SourceExistsValue;
        public SequenceDeletionSourceBackup CaptureSource(string path)
        {
            CapturedBytes = System.Text.Encoding.UTF8.GetBytes(SourceText);
            return new SequenceDeletionSourceBackup
            {
                SourcePath = path,
                SourceBytes = CapturedBytes
            };
        }

        public bool DeleteSource(SequenceDeletionSourceBackup backup, out string error)
        {
            _events.Add("delete-source");
            error = DeleteSourceSucceeds ? string.Empty : "delete source failed";
            return DeleteSourceSucceeds;
        }

        public bool RestoreSource(SequenceDeletionSourceBackup backup, out string error)
        {
            _events.Add("restore-source");
            RestoredBytes = backup.SourceBytes;
            error = RestoreSourceSucceeds ? string.Empty : "restore failed";
            return RestoreSourceSucceeds;
        }

        public bool DeleteRuntimeAsset(UnityEngine.Object asset, out string error)
        {
            _events.Add("delete-runtime");
            if (DeleteRuntimeSucceeds)
            {
                DeletedRuntimeAsset = asset;
            }

            error = DeleteRuntimeSucceeds ? string.Empty : "delete runtime failed";
            return DeleteRuntimeSucceeds;
        }
    }
}
