using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 전반의 이벤트를 문자열(String) ID로 쉽게 호출하고 구독할 수 있는 범용 이벤트 매니저입니다.
/// </summary>
public static class EventManager
{
    private static readonly Dictionary<string, Action> _events = new Dictionary<string, Action>();

    // 이벤트 구독 (예: 카메라 스크립트에서 EventManager.Subscribe("ShakeCamera", ShakeFunc); )
    public static void Subscribe(string eventName, Action listener)
    {
        if (string.IsNullOrEmpty(eventName)) return;
        if (!_events.ContainsKey(eventName)) _events[eventName] = null;
        _events[eventName] += listener;
    }

    // 이벤트 구독 해제
    public static void Unsubscribe(string eventName, Action listener)
    {
        if (string.IsNullOrEmpty(eventName) || !_events.ContainsKey(eventName)) return;
        _events[eventName] -= listener;
    }

    // 이벤트 실행 (DialogueData에서 EventTriggerID가 적혀있을 때 호출됨)
    public static void Trigger(string eventName)
    {
        if (string.IsNullOrEmpty(eventName)) return;

        if (_events.TryGetValue(eventName, out Action thisEvent))
        {
            thisEvent?.Invoke();
            Debug.Log($"<color=green>[EventManager]</color> 이벤트 실행됨: {eventName}");
        }
        else
        {
            Debug.LogWarning($"<color=yellow>[EventManager]</color> 구독자가 없는 이벤트 호출 시도: {eventName}");
        }
    }
}