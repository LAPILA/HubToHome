// =======================================================
// Text Animator for Unity - Copyright (c) 2018-Today, Febucci SRL, febucci.com
// - LICENSE: https://www.textanimatorforgames.com/legal/eula
// - DOCUMENTATION: https://docs.febucci.com/text-animator-unity/
// - WEBSITE: https://www.textanimatorforgames.com/
// =======================================================

using Febucci.TextAnimatorCore.Typing;
using UnityEngine;

namespace Febucci.TextAnimatorForUnity.Actions
{
    public sealed partial class WaitAnyInputAction
    {
        sealed class LegacyInputState : IActionState
        {
            readonly float maxTimeWaiting;
            float elapsedTime;
            bool noInputSystemWarningLogged;

            public LegacyInputState(float maxTimeWaiting)
            {
                this.maxTimeWaiting = maxTimeWaiting;
            }

            public ActionStatus Progress(float deltaTime, ref TypingInfo typingInfo)
            {
                elapsedTime += deltaTime;
                if (maxTimeWaiting > 0f && elapsedTime >= maxTimeWaiting)
                    return ActionStatus.Finished;

#if ENABLE_LEGACY_INPUT_MANAGER
                if (Input.anyKeyDown)
                    return ActionStatus.Finished;

                return ActionStatus.Running;
#else
                if (maxTimeWaiting > 0f)
                    return ActionStatus.Running;

                if (!noInputSystemWarningLogged)
                {
                    Debug.LogWarning($"No input system is enabled (?) - skipping this {nameof(WaitAnyInputAction)} action.");
                    noInputSystemWarningLogged = true;
                }

                return ActionStatus.Finished;
#endif
            }

            public void Cancel()
            {
            }
        }
    }
}
