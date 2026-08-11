using System.Text;

namespace RegConsole;

/// <summary>
/// 1件分のコマンド定義。
/// 「どんな文字列を送るか」をコードに埋め込まず、外部CSVから読むのがこのツールの肝。
/// これにより、コマンド体系を知らない人でもプルダウンから選んで叩けるようになる。
/// </summary>
public sealed class CommandDef
{
    public string Name { get; init; } = "";        // 画面に出す表示名（例：状態読み出し）
    public string Command { get; init; } = "";      // 実際に送る文字列（例：RD 0x00）
    public string Description { get; init; } = "";  // 補足説明（ツールチップ等に使う）

    public override string ToString() => Name; // ComboBox にそのまま入れると Name が表示される
}

/// <summary>
/// commands.csv を読み込むだけの小さなローダ。
/// フォーマット：1行目はヘッダ、以降 "表示名,送信文字列,説明"。
/// カンマを含めたいセルは "..." で囲む簡易クォートに対応。
/// </summary>
public static class CommandCsv
{
    public static List<CommandDef> Load(string path)
    {
        var list = new List<CommandDef>();
        if (!File.Exists(path))
            return list;

        // 日本語を含むので UTF-8 で読む
        var lines = File.ReadAllLines(path, Encoding.UTF8);
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (i == 0) continue;                       // ヘッダ行は飛ばす
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.TrimStart().StartsWith('#')) continue; // # 始まりはコメント扱い

            var cells = SplitCsvLine(line);
            if (cells.Count < 2) continue;              // 表示名と送信文字列は必須

            list.Add(new CommandDef
            {
                Name = cells[0].Trim(),
                Command = cells[1].Trim(),
                Description = cells.Count >= 3 ? cells[2].Trim() : ""
            });
        }
        return list;
    }

    /// <summary>簡易CSVパーサ（ダブルクォート囲みに対応）。</summary>
    private static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    // "" は 1個の " のエスケープ
                    if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else sb.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { result.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
        }
        result.Add(sb.ToString());
        return result;
    }
}
