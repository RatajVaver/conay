using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Conay.Utils;

public class VdfNode
{
    private sealed class Entry(string key, object value)
    {
        public string Key { get; } = key;
        public object Value { get; set; } = value;
    }

    private readonly List<Entry> _entries = [];

    public IEnumerable<(string Key, VdfNode Node)> Children =>
        _entries.Where(e => e.Value is VdfNode).Select(e => (e.Key, (VdfNode)e.Value));

    public string? GetString(string key) => Find(key)?.Value as string;

    public VdfNode? GetNode(string key) => Find(key)?.Value as VdfNode;

    public void SetString(string key, string value)
    {
        Entry? entry = Find(key);
        if (entry != null)
            entry.Value = value;
        else
            _entries.Add(new Entry(key, value));
    }

    public VdfNode GetOrAddNode(string key)
    {
        Entry? entry = Find(key);
        if (entry?.Value is VdfNode existing)
            return existing;

        VdfNode node = new();
        if (entry != null)
            entry.Value = node;
        else
            _entries.Add(new Entry(key, node));

        return node;
    }

    private Entry? Find(string key) =>
        _entries.FirstOrDefault(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase));

    public static VdfNode Parse(string text) => new Parser(text).ParseRoot();

    public override string ToString()
    {
        StringBuilder sb = new();
        Write(sb, 0);
        return sb.ToString();
    }

    private void Write(StringBuilder sb, int depth)
    {
        string indent = new('\t', depth);
        foreach (Entry entry in _entries)
        {
            sb.Append(indent).Append(Quote(entry.Key));

            if (entry.Value is VdfNode node)
            {
                sb.Append('\n').Append(indent).Append("{\n");
                node.Write(sb, depth + 1);
                sb.Append(indent).Append("}\n");
            }
            else
            {
                sb.Append("\t\t").Append(Quote((string)entry.Value)).Append('\n');
            }
        }
    }

    private static string Quote(string value)
    {
        StringBuilder sb = new(value.Length + 2);
        sb.Append('"');
        foreach (char c in value)
        {
            sb.Append(c switch
            {
                '\\' => @"\\",
                '"' => "\\\"",
                '\n' => @"\n",
                '\t' => @"\t",
                _ => c.ToString()
            });
        }

        return sb.Append('"').ToString();
    }

    private sealed class Parser(string text)
    {
        private int _pos;

        public VdfNode ParseRoot()
        {
            VdfNode root = ParseEntries();
            if (_pos < text.Length)
                throw new FormatException($"Unexpected '}}' at position {_pos}");

            return root;
        }

        private VdfNode ParseEntries()
        {
            VdfNode node = new();

            while (true)
            {
                SkipIgnored();
                if (_pos >= text.Length || text[_pos] == '}')
                    return node;

                string key = ReadToken();
                SkipIgnored();

                if (_pos < text.Length && text[_pos] == '{')
                {
                    _pos++;
                    VdfNode child = ParseEntries();
                    if (_pos >= text.Length)
                        throw new FormatException($"Missing '}}' for '{key}'");

                    _pos++;
                    node._entries.Add(new Entry(key, child));
                }
                else
                {
                    node._entries.Add(new Entry(key, ReadToken()));
                }
            }
        }

        private string ReadToken()
        {
            if (_pos >= text.Length)
                throw new FormatException("Unexpected end of file");

            if (text[_pos] != '"')
            {
                int start = _pos;
                while (_pos < text.Length && !char.IsWhiteSpace(text[_pos]) && text[_pos] is not ('{' or '}' or '"'))
                    _pos++;

                if (_pos == start)
                    throw new FormatException($"Unexpected '{text[_pos]}' at position {_pos}");

                return text[start.._pos];
            }

            _pos++;
            StringBuilder sb = new();
            while (_pos < text.Length && text[_pos] != '"')
            {
                char c = text[_pos++];
                if (c == '\\' && _pos < text.Length && Unescape(text[_pos]) is { } unescaped)
                {
                    sb.Append(unescaped);
                    _pos++;
                }
                else
                {
                    sb.Append(c);
                }
            }

            if (_pos >= text.Length)
                throw new FormatException("Unterminated string");

            _pos++;
            return sb.ToString();
        }

        private static char? Unescape(char c) => c switch
        {
            '\\' => '\\',
            '"' => '"',
            'n' => '\n',
            't' => '\t',
            _ => null
        };

        private void SkipIgnored()
        {
            while (_pos < text.Length)
            {
                if (char.IsWhiteSpace(text[_pos]))
                {
                    _pos++;
                }
                else if (text[_pos] == '/' && _pos + 1 < text.Length && text[_pos + 1] == '/')
                {
                    while (_pos < text.Length && text[_pos] != '\n')
                        _pos++;
                }
                else
                {
                    return;
                }
            }
        }
    }
}
