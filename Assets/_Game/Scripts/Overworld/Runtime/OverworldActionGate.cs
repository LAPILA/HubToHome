public static class OverworldActionGate
{
    public static bool AllowsWorldActions
    {
        get
        {
            GameStateManager stateManager = GameStateManager.Instance;
            return stateManager == null
                || stateManager.CurrentState == GameState.Exploration;
        }
    }
}