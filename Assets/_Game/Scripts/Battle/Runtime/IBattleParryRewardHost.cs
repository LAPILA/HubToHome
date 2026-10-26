using System;

/// <summary>선택형 AP 보상 수명. 구형 호스트와 테스트는 기존 인터페이스를 그대로 유지합니다.</summary>
public interface IBattleParryRewardHost
{
    IDisposable BeginParryRewardAction();
    int AwardPerfectParryAP(PlayerCharacter actor);
    void ResetParryRewardAction();
}
