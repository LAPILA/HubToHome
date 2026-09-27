using System;
using UnityEngine;
using Sirenix.OdinInspector;

[Serializable]
public sealed class BattleResourceFrame
{
    [LabelText("스프라이트")] public Sprite Sprite;
    [MinValue(.01f), LabelText("표시 시간")] public float Duration = .1f;
}

[Serializable]
public sealed class BattleResourceStage
{
    [LabelText("원본 애니메이션")] public AnimationClip SourceClip;
    [LabelText("반복")] public bool Loop = true;
    [LabelText("UI 프레임")] public BattleResourceFrame[] Frames = Array.Empty<BattleResourceFrame>();
}

[CreateAssetMenu(fileName = "BattleResource", menuName = "Hub To Home/캐릭터/전투 고유 자원")]
public sealed class BattleResourceDefinition : ScriptableObject
{
    [LabelText("표시 이름")] public string DisplayName = "압력";
    [MinValue(1), LabelText("최대 단계")] public int MaxStep = 4;
    [MinValue(0), LabelText("전투 시작 단계")] public int InitialStep;
    [MinValue(0), LabelText("퍼펙트 패링 획득")] public int PerfectParryGain = 1;
    [MinValue(0), LabelText("C 특수 반격 획득")] public int CounterGain;
    [LabelText("단계별 표시 (0부터)")] public BattleResourceStage[] Stages = Array.Empty<BattleResourceStage>();

    public BattleResourceStage GetStage(int step)
        => Stages != null && step >= 0 && step < Stages.Length ? Stages[step] : null;

#if UNITY_EDITOR
    [Button("원본 애니메이션에서 UI 프레임 갱신")]
    public void BakeAnimationFrames()
    {
        if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;
        var baked = new BattleResourceFrame[Stages?.Length ?? 0][];
        for (int stageIndex = 0; stageIndex < baked.Length; stageIndex++)
        {
            AnimationClip clip = Stages[stageIndex]?.SourceClip;
            if (clip == null) continue;
            UnityEditor.ObjectReferenceKeyframe[] keys = null;
            foreach (var binding in UnityEditor.AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                if (binding.type != typeof(SpriteRenderer) || binding.propertyName != "m_Sprite") continue;
                if (keys != null)
                {
                    Debug.LogError("자원 UI는 한 SpriteRenderer의 애니메이션을 사용합니다: " + clip.name, this);
                    return;
                }
                keys = UnityEditor.AnimationUtility.GetObjectReferenceCurve(clip, binding);
            }
            if (keys == null || keys.Length == 0 || !(keys[0].value is Sprite))
            {
                Debug.LogError("스프라이트 프레임이 없는 원본입니다: " + clip.name, this);
                return;
            }
            var frames = new System.Collections.Generic.List<BattleResourceFrame>();
            for (int i = 0; i < keys.Length; i++)
            {
                // Aseprite's terminal hold key can sit exactly at clip.length.
                float end = i + 1 < keys.Length ? keys[i + 1].time : clip.length;
                float duration = end - keys[i].time;
                if (duration <= 0f) continue;
                frames.Add(new BattleResourceFrame { Sprite = keys[i].value as Sprite, Duration = duration });
            }
            if (frames.Count == 0)
                frames.Add(new BattleResourceFrame { Sprite = keys[0].value as Sprite, Duration = .1f });
            baked[stageIndex] = frames.ToArray();
        }
        UnityEditor.Undo.RecordObject(this, "Bake resource UI frames");
        for (int i = 0; i < baked.Length; i++)
            if (baked[i] != null) Stages[i].Frames = baked[i];
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.AssetDatabase.SaveAssetIfDirty(this);
    }
#endif
}
