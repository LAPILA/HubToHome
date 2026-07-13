using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class EditorPreviewStateScopeIsolationTests
{
    [SetUp]
    public void SetUp()
    {
        Undo.ClearAll();
    }

    [TearDown]
    public void TearDown()
    {
        Undo.ClearAll();
    }

    [Test]
    public void Restore_DoesNotUndoUnrelatedEditsMadeAfterPreviewStarted()
    {
        PreviewValueObject previewObject =
            ScriptableObject.CreateInstance<PreviewValueObject>();
        PreviewValueObject unrelated =
            ScriptableObject.CreateInstance<PreviewValueObject>();
        var participant = new PreviewParticipant(previewObject);

        try
        {
            using (var scope = new EditorPreviewStateScope())
            {
                Assert.That(
                    scope.TryCapture("preview", participant, out string error),
                    Is.True,
                    error);
                previewObject.Value = 20;

                Undo.RecordObject(unrelated, "Unrelated edit");
                unrelated.Value = 99;
                scope.Restore();

                Assert.That(previewObject.Value, Is.Zero);
                Assert.That(unrelated.Value, Is.EqualTo(99));
            }

            Undo.PerformUndo();
            Assert.That(unrelated.Value, Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(previewObject);
            Object.DestroyImmediate(unrelated);
        }
    }

    private sealed class PreviewParticipant :
        IPreviewStateParticipant,
        IPreviewUndoObjectProvider
    {
        private readonly PreviewValueObject _target;

        public PreviewParticipant(PreviewValueObject target)
        {
            _target = target;
        }

        public object CapturePreviewState() => _target.Value;

        public void RestorePreviewState(object state)
        {
            _target.Value = state is int value ? value : 0;
        }

        public IEnumerable<Object> GetPreviewUndoObjects()
        {
            yield return _target;
        }
    }

    private sealed class PreviewValueObject : ScriptableObject
    {
        public int Value;
    }
}
