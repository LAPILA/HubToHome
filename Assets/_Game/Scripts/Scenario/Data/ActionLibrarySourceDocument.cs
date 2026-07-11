using System.Collections.Generic;

public sealed class ActionLibrarySourceDocument
{
    public string SourcePath = string.Empty;
    public string LibraryId = string.Empty;
    public string DisplayNameKo = string.Empty;
    public string DescriptionKo = string.Empty;
    public string Category = string.Empty;
    public int SortOrder;
    public string AccentHex = string.Empty;
    public List<ActionCatalogEntry> Entries = new List<ActionCatalogEntry>();
}
