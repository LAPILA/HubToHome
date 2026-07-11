using System;
using System.Collections.Generic;
using System.Text;

public sealed class ResolvedActionLibrary
{
    public readonly List<ActionCatalogEntry> Entries = new List<ActionCatalogEntry>();
    public readonly List<string> SourcePaths = new List<string>();
    public readonly ScenarioValidationResult Validation = new ScenarioValidationResult();
    public string SourceHash = string.Empty;

    public ActionCatalogEntry Find(string actionId)
    {
        string normalized = Normalize(actionId);
        for (int i = 0; i < Entries.Count; i++)
        {
            if (Entries[i] != null && Normalize(Entries[i].ActionId) == normalized)
            {
                return Entries[i];
            }
        }

        return null;
    }

    public static ResolvedActionLibrary Build(
        IEnumerable<ActionLibrarySourceDocument> documents,
        IEnumerable<ActionCatalogAsset> compatibilityCatalogs = null)
    {
        var result = new ResolvedActionLibrary();
        var owners = new Dictionary<string, string>(StringComparer.Ordinal);
        var sourceTexts = new List<SourceText>();
        if (documents != null)
        {
            foreach (ActionLibrarySourceDocument document in documents)
            {
                if (document == null)
                {
                    continue;
                }

                result.Validation.Merge(ActionLibrarySourceValidation.Validate(document));
                string source = string.IsNullOrWhiteSpace(document.SourcePath)
                    ? "library:" + Normalize(document.LibraryId)
                    : document.SourcePath;
                AddSourcePath(result.SourcePaths, source);
                ActionLibrarySourceWriteResult write = ActionLibrarySourceWriter.Write(document);
                if (write.Success)
                {
                    sourceTexts.Add(new SourceText(source, write.Text));
                }

                AddEntries(result, owners, document.Entries, source);
            }
        }

        if (compatibilityCatalogs != null)
        {
            foreach (ActionCatalogAsset catalog in compatibilityCatalogs)
            {
                if (catalog == null)
                {
                    continue;
                }

                string source = "asset:" + Normalize(catalog.CatalogId);
                AddSourcePath(result.SourcePaths, source);
                AddEntries(result, owners, catalog.Entries, source);
            }
        }

        result.Entries.Sort(CompareEntries);
        result.SourcePaths.Sort(StringComparer.Ordinal);
        sourceTexts.Sort((left, right) => string.Compare(left.Path, right.Path, StringComparison.Ordinal));
        var hashText = new StringBuilder();
        for (int i = 0; i < sourceTexts.Count; i++)
        {
            hashText.Append(sourceTexts[i].Path);
            hashText.Append('\n');
            hashText.Append(sourceTexts[i].Text);
            hashText.Append('\n');
        }

        result.SourceHash = ScenarioSourceHash.Compute(hashText.ToString());
        return result;
    }

    private static void AddEntries(
        ResolvedActionLibrary result,
        Dictionary<string, string> owners,
        IList<ActionCatalogEntry> entries,
        string source)
    {
        if (entries == null)
        {
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            ActionCatalogEntry entry = entries[i];
            if (entry == null)
            {
                continue;
            }

            string actionId = Normalize(entry.ActionId);
            if (owners.TryGetValue(actionId, out string previousSource))
            {
                result.Validation.AddError(
                    "action_library.action.duplicate",
                    "Action ID '" + actionId + "' is defined by both '" + previousSource + "' and '" + source + "'.",
                    "action:" + actionId);
                continue;
            }

            owners[actionId] = source;
            result.Entries.Add(ActionCatalogContractCopy.Entry(entry));
        }
    }

    private static int CompareEntries(ActionCatalogEntry left, ActionCatalogEntry right)
    {
        int category = string.Compare(Normalize(left?.Category), Normalize(right?.Category), StringComparison.Ordinal);
        return category != 0
            ? category
            : string.Compare(Normalize(left?.ActionId), Normalize(right?.ActionId), StringComparison.Ordinal);
    }

    private static void AddSourcePath(List<string> paths, string path)
    {
        if (!string.IsNullOrWhiteSpace(path) && !paths.Contains(path))
        {
            paths.Add(path);
        }
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private readonly struct SourceText
    {
        public SourceText(string path, string text)
        {
            Path = path;
            Text = text;
        }

        public string Path { get; }
        public string Text { get; }
    }
}
