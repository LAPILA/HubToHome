using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
internal static class CodexTemporaryEditModeTestRunner
{
    private const string RequestPath = "Temp/CodexRunEditModeTests.request";
    private const string ResultPath = "Temp/CodexEditModeTestResults.txt";
    private static TestRunnerApi _api;
    private static TestCallbacks _callbacks;

    static CodexTemporaryEditModeTestRunner()
    {
        if (!File.Exists(RequestPath))
            return;

        EditorApplication.delayCall += Start;
    }

    private static void Start()
    {
        if (!File.Exists(RequestPath) || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        File.WriteAllText(ResultPath, "RUNNING");
        _api = ScriptableObject.CreateInstance<TestRunnerApi>();
        _callbacks = new TestCallbacks();
        _api.RegisterCallbacks(_callbacks);
        _api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
    }

    private sealed class TestCallbacks : ICallbacks
    {
        private readonly List<string> _failures = new List<string>();
        private int _passed;
        private int _failed;
        private int _skipped;

        public void RunStarted(ITestAdaptor testsToRun)
        {
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            var lines = new List<string>
            {
                "COMPLETE",
                "Passed=" + _passed,
                "Failed=" + _failed,
                "Skipped=" + _skipped
            };
            lines.AddRange(_failures);
            File.WriteAllLines(ResultPath, lines);
            File.Delete(RequestPath);
            _api.UnregisterCallbacks(this);
            ScriptableObject.DestroyImmediate(_api);
            _api = null;
            _callbacks = null;
        }

        public void TestStarted(ITestAdaptor test)
        {
        }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result == null || result.HasChildren)
                return;

            string status = result.TestStatus.ToString();
            if (string.Equals(status, "Passed", StringComparison.OrdinalIgnoreCase))
            {
                _passed++;
                return;
            }

            if (string.Equals(status, "Skipped", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "Inconclusive", StringComparison.OrdinalIgnoreCase))
            {
                _skipped++;
                return;
            }

            _failed++;
            _failures.Add(
                "FAIL " + result.Test.FullName + ": "
                + (result.Message ?? string.Empty).Replace("\r", " ").Replace("\n", " "));
        }
    }
}
