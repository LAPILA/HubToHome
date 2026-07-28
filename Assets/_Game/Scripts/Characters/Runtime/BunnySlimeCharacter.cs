using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// 껍질이 깨지기를 기다리며 공격하지 않는 토끼 슬라임 전용 전투 행동입니다.
/// </summary>
public sealed class BunnySlimeCharacter : EnemyCharacter
{
    private const string FallbackMessage = "토끼 슬라임은 껍질 속에서 가만히 기다리고 있다...";

    [Title("Bunny Slime Turn Text")]
    [SerializeField, TextArea(1, 3), ListDrawerSettings(ShowIndexLabels = true)]
    private string[] _turnMessages =
    {
        FallbackMessage,
        "\"네 공격, 별로 안 아프네...\"",
        "\"나 껍질 빨리 부숴 줘...\""
    };

    private int _nextMessageIndex;

    public override EnemyAction DecideAction()
    {
        return EnemyAction.Wait;
    }

    public BattleNarrationMessage GetNextWaitNarration()
    {
        string text = ResolveNextMessage();
        return new BattleNarrationMessage(
            text,
            BattleNarrationStyle.Normal,
            BattleNarrationPriority.Normal,
            0.55f,
            requiresConfirm: false);
    }

    private string ResolveNextMessage()
    {
        if (_turnMessages == null || _turnMessages.Length == 0)
        {
            return FallbackMessage;
        }

        int startIndex = _nextMessageIndex % _turnMessages.Length;
        for (int offset = 0; offset < _turnMessages.Length; offset++)
        {
            int index = (startIndex + offset) % _turnMessages.Length;
            string message = _turnMessages[index];
            if (string.IsNullOrWhiteSpace(message))
            {
                continue;
            }

            _nextMessageIndex = (index + 1) % _turnMessages.Length;
            return message;
        }

        return FallbackMessage;
    }
}
