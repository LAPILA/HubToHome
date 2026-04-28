// =======================================================
// Text Animator for Unity - Copyright (c) 2018-Today, Febucci SRL, febucci.com
// - LICENSE: https://www.textanimatorforgames.com/legal/eula
// - DOCUMENTATION: https://docs.febucci.com/text-animator-unity/
// - WEBSITE: https://www.textanimatorforgames.com/
// =======================================================

using Febucci.TextAnimatorCore.Typing;
using Febucci.TextAnimatorForUnity.Actions.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace Febucci.TextAnimatorForUnity.Actions
{
    public enum WaitInputMode
    {
        AnyInput = 0,
        SpecificAction = 1
    }

    [System.Serializable]
    [CreateAssetMenu(menuName = ScriptablePaths.ACTIONS_PATH + "Wait Any Input", fileName = "Wait Any Input Action")]
    [TagInfo("waitinput")]
    public sealed partial class WaitAnyInputAction : ActionScriptableBase
    {
        [SerializeField] internal string tagID;
        [Tooltip("Maximum time in seconds to keep waiting. Use 0 or less for no timeout.")]
        [SerializeField] internal float maxSecondsWaiting = -1f;

        public override string TagID
        {
            get => tagID;
            set => tagID = value;
        }

        public override IActionState CreateActionFrom(ActionMarker marker, object typewriter)
        {
#if ENABLE_INPUT_SYSTEM
            return CreateActionState(marker);
#else
            return new LegacyInputState(maxSecondsWaiting);
#endif
        }
    }
}
