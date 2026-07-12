public static class CharacterPivotId
{
    public const string Root = "Pivots";
    public const string Center = "Center";
    public const string Front = "Front";
    public const string Back = "Back";
    public const string Top = "Top";
    public const string Bottom = "Bottom";

    public static string GetPath(string pivotId)
    {
        return Root + "/" + pivotId;
    }
}

