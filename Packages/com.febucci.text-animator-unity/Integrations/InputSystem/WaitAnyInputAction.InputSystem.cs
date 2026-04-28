// =======================================================
// Text Animator for Unity - Copyright (c) 2018-Today, Febucci SRL, febucci.com
// - LICENSE: https://www.textanimatorforgames.com/legal/eula
// - DOCUMENTATION: https://docs.febucci.com/text-animator-unity/
// - WEBSITE: https://www.textanimatorforgames.com/
// =======================================================

#if ENABLE_INPUT_SYSTEM
using System;
using System.Collections.Generic;
using System.Linq;
using Febucci.TextAnimatorCore.Typing;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.Serialization;

namespace Febucci.TextAnimatorForUnity.Actions
{
    public sealed partial class WaitAnyInputAction
    {
        const string AnyKey = "any";

        [Tooltip("Choose whether to wait for any input or for one of the mapped input actions.")]
        [SerializeField] internal WaitInputMode inputMode = WaitInputMode.AnyInput;
        [Tooltip("Map a key (first tag parameter) to a specific Input Action.")]
        [SerializeField] internal WaitInputMap[] inputActions;
        [Tooltip("Default key used when the tag has no first parameter.")]
        [FormerlySerializedAs("inputActionReference")]
        [FormerlySerializedAs("defaultInputActionReference")]
        [SerializeField] internal string defaultInputKey;
        [Tooltip("If enabled, automatically enables mapped Input Actions when needed.")]
        [SerializeField] internal bool autoEnableInputAction = true;
        [Tooltip("If enabled, restores each Input Action enabled state after this action finishes.")]
        [SerializeField] internal bool restoreActionAfterFinish = true;

        [Serializable]
        internal struct WaitInputMap
        {
            [SerializeField] internal string key;
            [SerializeField] internal InputActionReference actionReference;
        }

        IActionState CreateActionState(ActionMarker marker)
        {
            WaitInputMode resolvedMode = inputMode;
            InputActionReference[] targetActions = ResolveTargetActions(marker, ref resolvedMode);

            return new InputSystemWaitState(
                resolvedMode,
                targetActions,
                maxSecondsWaiting,
                autoEnableInputAction,
                restoreActionAfterFinish);
        }

        InputActionReference[] ResolveTargetActions(ActionMarker marker, ref WaitInputMode resolvedMode)
        {
            if (inputMode == WaitInputMode.AnyInput)
            {
                resolvedMode = WaitInputMode.AnyInput;
                return Array.Empty<InputActionReference>();
            }

            int mapCount = inputActions != null ? inputActions.Length : 0;
            string requestedKey = GetRequestedKey(marker);
            string selectedKey = !string.IsNullOrEmpty(requestedKey)
                ? requestedKey
                : NormalizeKey(defaultInputKey);

            if (!string.IsNullOrEmpty(selectedKey))
            {
                for (int i = 0; i < mapCount; i++)
                {
                    var map = inputActions[i];
                    if (!string.Equals(NormalizeKey(map.key), selectedKey, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (map.actionReference == null)
                    {
                        Debug.LogWarning($"{nameof(WaitAnyInputAction)}: key '{selectedKey}' is mapped to a null InputActionReference. The action will finish immediately.");
                        return Array.Empty<InputActionReference>();
                    }

                    return new[] { map.actionReference };
                }

                if (IsAnyKey(selectedKey))
                {
                    resolvedMode = WaitInputMode.AnyInput;
                    return Array.Empty<InputActionReference>();
                }

                Debug.LogWarning($"{nameof(WaitAnyInputAction)}: no Input Action mapping found for key '{selectedKey}'. The action will finish immediately.");
                return Array.Empty<InputActionReference>();
            }

            var collected = new List<InputActionReference>();
            for (int i = 0; i < mapCount; i++)
            {
                if (inputActions[i].actionReference != null)
                    collected.Add(inputActions[i].actionReference);
            }

            return collected.ToArray();
        }

        static string GetRequestedKey(ActionMarker marker)
        {
            if (marker.parameters == null || marker.parameters.Length == 0)
                return null;

            var value = marker.parameters[0];
            return NormalizeKey(value);
        }

        static string NormalizeKey(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        static bool IsAnyKey(string value)
        {
            return string.Equals(NormalizeKey(value), AnyKey, StringComparison.OrdinalIgnoreCase);
        }

        sealed class InputSystemWaitState : IActionState
        {
            readonly WaitInputMode inputMode;
            readonly InputAction[] inputActions;
            readonly bool restoreActionAfterFinish;
            readonly float maxTimeWaiting;
            readonly bool[] initialInputActionEnabled;

            bool inputSystemPassed;
            bool specificActionPassed;
            bool finishImmediately;
            bool hasCleanedUp;
            IDisposable eventListener;
            float elapsedTime;

            public InputSystemWaitState(
                WaitInputMode inputMode,
                InputActionReference[] inputActionReferences,
                float maxTimeWaiting,
                bool autoEnableInputAction,
                bool restoreActionAfterFinish)
            {
                this.inputMode = inputMode;
                this.maxTimeWaiting = maxTimeWaiting;
                this.restoreActionAfterFinish = restoreActionAfterFinish;

                if (inputMode == WaitInputMode.AnyInput)
                {
                    eventListener = InputSystem.onAnyButtonPress.CallOnce(PassInput);
                    return;
                }

                inputActions = (inputActionReferences ?? Array.Empty<InputActionReference>())
                    .Where(reference => reference != null && reference.action != null)
                    .Select(reference => reference.action)
                    .Distinct()
                    .ToArray();

                if (inputActions.Length == 0)
                {
                    finishImmediately = true;
                    return;
                }

                initialInputActionEnabled = new bool[inputActions.Length];
                for (int i = 0; i < inputActions.Length; i++)
                {
                    initialInputActionEnabled[i] = inputActions[i].enabled;
                    if (!inputActions[i].enabled && autoEnableInputAction)
                        inputActions[i].Enable();
                }

                var disabledActions = inputActions.Where(action => !action.enabled).Select(action => action.name).ToArray();
                if (disabledActions.Length > 0)
                {
                    Debug.LogWarning($"{nameof(WaitAnyInputAction)} is set to SpecificAction but these InputActions are disabled and Auto Enable is off: {string.Join(", ", disabledActions)}. The action will finish immediately.");
                    finishImmediately = true;
                    return;
                }

                foreach (var action in inputActions)
                    action.performed += PassSpecificAction;
            }

            void PassInput(InputControl control)
            {
                inputSystemPassed = true;
            }

            void PassSpecificAction(InputAction.CallbackContext context)
            {
                specificActionPassed = true;
            }

            void RestoreInputActionsState()
            {
                if (!restoreActionAfterFinish || inputActions == null || initialInputActionEnabled == null)
                    return;

                for (int i = 0; i < inputActions.Length; i++)
                {
                    var action = inputActions[i];
                    if (action == null)
                        continue;

                    bool wasEnabled = initialInputActionEnabled[i];
                    if (wasEnabled && !action.enabled)
                        action.Enable();
                    else if (!wasEnabled && action.enabled)
                        action.Disable();
                }
            }

            void Cleanup()
            {
                if (hasCleanedUp)
                    return;

                hasCleanedUp = true;

                if (inputMode == WaitInputMode.AnyInput)
                    eventListener?.Dispose();
                else if (inputActions != null)
                    foreach (var action in inputActions)
                        action.performed -= PassSpecificAction;

                RestoreInputActionsState();
            }

            ActionStatus Complete()
            {
                Cleanup();
                return ActionStatus.Finished;
            }

            public ActionStatus Progress(float deltaTime, ref TypingInfo typingInfo)
            {
                elapsedTime += deltaTime;
                if (maxTimeWaiting > 0f && elapsedTime >= maxTimeWaiting)
                    return Complete();

#if ENABLE_LEGACY_INPUT_MANAGER
                if (inputMode == WaitInputMode.AnyInput && Input.anyKeyDown)
                    return Complete();
#endif

                if (inputMode == WaitInputMode.AnyInput)
                {
                    if (inputSystemPassed)
                        return Complete();
                }
                else if (finishImmediately || specificActionPassed)
                {
                    return Complete();
                }

                return ActionStatus.Running;
            }

            public void Cancel()
            {
                Cleanup();
            }
        }
    }
}
#endif
