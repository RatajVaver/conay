using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Conay.Utils;

public sealed class IniFile
{
    private readonly string _path;
    private readonly List<string> _lines;
    private readonly Encoding _encoding;
    private readonly string _newLine;
    private readonly bool _trailingNewLine;
    private bool _dirty;

    private IniFile(string path, List<string> lines, Encoding encoding, string newLine, bool trailingNewLine)
    {
        _path = path;
        _lines = lines;
        _encoding = encoding;
        _newLine = newLine;
        _trailingNewLine = trailingNewLine;
    }

    public static IniFile Load(string path)
    {
        if (!File.Exists(path))
            return new IniFile(path, [], new UTF8Encoding(false), "\r\n", true);

        string text;
        Encoding encoding;

        using (StreamReader reader = new(path, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true))
        {
            text = reader.ReadToEnd();
            encoding = reader.CurrentEncoding;
        }

        string newLine = text.Contains("\r\n") || !text.Contains('\n') ? "\r\n" : "\n";
        bool trailing = text.EndsWith('\n');
        if (trailing) text = text[..^(text.EndsWith("\r\n") ? 2 : 1)];

        List<string> lines = text.Length == 0 ? [] : [.. text.Split('\n')];
        for (int i = 0; i < lines.Count; i++)
            if (lines[i].EndsWith('\r'))
                lines[i] = lines[i][..^1];

        return new IniFile(path, lines, encoding, newLine, trailing);
    }

    public string? Get(string section, string key)
    {
        string? value = null;
        bool inSection = false;
        foreach (string line in _lines)
        {
            if (IsHeader(line)) inSection = IsSection(line, section);
            else if (inSection && IsKey(line, key, out int eq)) value = line[(eq + 1)..];
        }

        return value;
    }

    public void Set(string section, string key, string value)
    {
        value = value.Replace("\r", "").Replace("\n", "");

        bool found = false;
        bool inSection = false;
        int lastContent = -1;
        int insertAt = -1;

        for (int i = 0; i < _lines.Count; i++)
        {
            string line = _lines[i];
            if (IsHeader(line))
            {
                if (inSection && insertAt == -1) insertAt = lastContent + 1;
                inSection = IsSection(line, section);
                if (inSection) lastContent = i;
                continue;
            }

            if (!inSection) continue;
            if (!string.IsNullOrWhiteSpace(line)) lastContent = i;
            if (!IsKey(line, key, out int eq)) continue;

            found = true;
            if (line[(eq + 1)..] == value) continue;
            _lines[i] = $"{line[..eq]}={value}";
            _dirty = true;
        }

        if (found) return;
        if (inSection && insertAt == -1) insertAt = lastContent + 1;

        if (insertAt != -1)
        {
            _lines.Insert(insertAt, $"{key}={value}");
        }
        else
        {
            if (_lines.Count > 0 && !string.IsNullOrWhiteSpace(_lines[^1])) _lines.Add("");
            _lines.Add(section);
            _lines.Add($"{key}={value}");
        }

        _dirty = true;
    }

    public void Set(string section, IEnumerable<KeyValuePair<string, string>> values)
    {
        foreach (KeyValuePair<string, string> kv in values)
            Set(section, kv.Key, kv.Value);
    }

    public void Remove(string section, string key)
    {
        bool inSection = false;
        for (int i = 0; i < _lines.Count; i++)
        {
            string line = _lines[i];
            if (IsHeader(line))
            {
                inSection = IsSection(line, section);
                continue;
            }

            if (!inSection || !IsKey(line, key, out _)) continue;
            _lines.RemoveAt(i--);
            _dirty = true;
        }
    }

    public void SwapArrayOp(string section, string key, char from, char to)
    {
        bool inSection = false;
        for (int i = 0; i < _lines.Count; i++)
        {
            string line = _lines[i];
            if (IsHeader(line))
            {
                inSection = IsSection(line, section);
                continue;
            }

            if (!inSection) continue;
            string trimmed = line.TrimStart();
            if (trimmed.Length == 0 || trimmed[0] != from) continue;

            string rest = trimmed[1..];
            if (!IsKey(rest, key, out int eq) || string.IsNullOrWhiteSpace(rest[(eq + 1)..])) continue;

            _lines[i] = line[..^trimmed.Length] + to + rest;
            _dirty = true;
        }
    }

    public bool Save()
    {
        if (!_dirty) return false;

        if (File.Exists(_path) && File.GetAttributes(_path).HasFlag(FileAttributes.ReadOnly))
            throw new UnauthorizedAccessException($"'{_path}' is read-only.");

        string? dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        string text = string.Join(_newLine, _lines);
        if (_trailingNewLine && _lines.Count > 0) text += _newLine;

        string tmpPath = _path + ".tmp";
        try
        {
            File.WriteAllText(tmpPath, text, _encoding);

            if (File.Exists(_path))
            {
                File.Replace(tmpPath, _path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tmpPath, _path);
            }
        }
        finally
        {
            if (File.Exists(tmpPath)) File.Delete(tmpPath);
        }

        _dirty = false;
        return true;
    }

    private static bool IsHeader(string line) => line.TrimStart().StartsWith('[');

    private static bool IsSection(string line, string section) =>
        string.Equals(line.Trim(), section, StringComparison.OrdinalIgnoreCase);

    private static bool IsKey(string line, string key, out int eq)
    {
        eq = line.IndexOf('=');
        return eq > 0 && string.Equals(line[..eq].Trim(), key, StringComparison.OrdinalIgnoreCase);
    }
}