using System;
using System.IO;
using UnityEngine;

public static class ScenarioSourcePathPolicy
{
    public const string AllowedRoot = "Assets/_Game/Content/Scenarios/";

    public static bool TryNormalize(
        string path,
        out string normalizedPath,
        out string error)
    {
        normalizedPath = string.Empty;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            error = "Scenario Source 경로가 비어 있습니다.";
            return false;
        }

        string raw = path.Trim().Replace('\\', '/');
        if (Path.IsPathRooted(raw))
        {
            error = "Scenario Source는 프로젝트 기준 상대 경로여야 합니다.";
            return false;
        }

        string[] segments = raw.Split('/');
        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i] == "..")
            {
                error = "Scenario Source 경로에서 상위 폴더 이동(..)은 허용되지 않습니다.";
                return false;
            }
        }

        if (!raw.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
        {
            error = "Scenario Source 확장자는 .yaml이어야 합니다.";
            return false;
        }

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string allowedRoot = Path.GetFullPath(Path.Combine(projectRoot, AllowedRoot));
        string absolute = Path.GetFullPath(Path.Combine(projectRoot, raw));
        string rootWithSeparator = allowedRoot.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!absolute.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            error = "Scenario Source는 " + AllowedRoot + " 아래에 있어야 합니다.";
            return false;
        }

        normalizedPath = raw;
        return true;
    }

    public static string RequireProjectYamlAbsolute(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("Scenario YAML 경로가 비어 있습니다.");

        string raw = path.Trim().Replace('\\', '/');
        if (Path.IsPathRooted(raw) || raw == ".." || raw.StartsWith("../") || raw.Contains("/../") || raw.EndsWith("/.."))
            throw new InvalidOperationException("Scenario YAML은 프로젝트 내부 상대 경로여야 합니다.");

        if (!raw.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Scenario YAML 확장자는 .yaml이어야 합니다.");

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string assetsRoot = Path.GetFullPath(Path.Combine(projectRoot, "Assets"))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        string absolute = Path.GetFullPath(Path.Combine(projectRoot, raw));
        if (!absolute.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Scenario YAML은 프로젝트 Assets 내부에 있어야 합니다.");

        return absolute;
    }

    public static string RequireAbsolute(string path)
    {
        if (!TryNormalize(path, out string normalized, out string error))
            throw new InvalidOperationException(error);

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.GetFullPath(Path.Combine(projectRoot, normalized));
    }
}
