using System.Collections.Generic;
using System.Text;

/// <summary>인용된 쉼표·큰따옴표·물리 줄바꿈을 지원하는 번역표 읽기 전용 parser.</summary>
public static class LocalizationCsv
{
    public static List<string[]> Parse(string source)
    {
        var rows = new List<string[]>();
        if (string.IsNullOrEmpty(source)) return rows;
        var fields = new List<string>(5);
        var field = new StringBuilder();
        bool quoted = false;
        for (int i = source[0] == '\uFEFF' ? 1 : 0; i < source.Length; i++)
        {
            char c = source[i];
            if (c == '"')
            {
                if (quoted && i + 1 < source.Length && source[i + 1] == '"')
                { field.Append('"'); i++; }
                else if (quoted || field.Length == 0) quoted = !quoted;
                else field.Append(c);
            }
            else if (!quoted && c == ',')
            { fields.Add(field.ToString()); field.Clear(); }
            else if (!quoted && (c == '\r' || c == '\n'))
            {
                if (c == '\r' && i + 1 < source.Length && source[i + 1] == '\n') i++;
                fields.Add(field.ToString()); field.Clear();
                if (fields.Count > 1 || fields[0].Length > 0) rows.Add(fields.ToArray());
                fields.Clear();
            }
            else field.Append(c);
        }
        // 잘못 닫힌 행을 다른 번역으로 오인하지 않습니다. 앞의 정상 행만 보존합니다.
        if (!quoted && (fields.Count > 0 || field.Length > 0))
        { fields.Add(field.ToString()); rows.Add(fields.ToArray()); }
        return rows;
    }
}
