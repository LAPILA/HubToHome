/// <summary>기관사 장면의 복구는 같은 방·세대·표시 lease에만 허용합니다.</summary>
public static class PrologueTutorialIntroductionOwnership
{
    public static bool CanRestore(bool sameRoom, bool sameGeneration, bool ownsLease,
        bool ownedCancellation, bool battleActive)
        => sameRoom && ownsLease && !battleActive && (sameGeneration || ownedCancellation);
}
