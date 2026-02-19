using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DMM.AssetManagers.GameStores.Steam;

public static class SteamVdf
{
    public sealed class Node
    {
        // flat values: key -> string OR Node
        public Dictionary<string, object> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

        public IEnumerable<KeyValuePair<string, Node>> Children
        {
            get
            {
                foreach (var kv in Values)
                    if (kv.Value is Node n) yield return new KeyValuePair<string, Node>(kv.Key, n);
            }
        }

        public string? GetString(string key)
            => Values.TryGetValue(key, out var v) && v is string s ? s : null;

        public Node? GetSection(string key)
            => Values.TryGetValue(key, out var v) && v is Node n ? n : null;
    }

    public static Node ParseFile(string path)
        => ParseText(File.ReadAllText(path, Encoding.UTF8));

    public static Node ParseText(string text)
    {
        var t = new Tokenizer(text);
        var root = new Node();

        while (true)
        {
            var key = t.ReadStringOrNull();
            if (key is null) break;

            t.SkipWhitespace();

            if (t.PeekChar() == '{')
            {
                t.ReadChar(); // consume {
                root.Values[key] = ReadObject(t);
            }
            else
            {
                var value = t.ReadString();
                root.Values[key] = value;
            }
        }

        return root;
    }

    private static Node ReadObject(Tokenizer t)
    {
        var node = new Node();

        while (true)
        {
            t.SkipWhitespace();

            var c = t.PeekChar();
            if (c == '\0') break;

            if (c == '}')
            {
                t.ReadChar(); // consume }
                break;
            }

            var key = t.ReadString();
            t.SkipWhitespace();

            if (t.PeekChar() == '{')
            {
                t.ReadChar(); // {
                node.Values[key] = ReadObject(t);
            }
            else
            {
                var value = t.ReadString();
                node.Values[key] = value;
            }
        }

        return node;
    }

    private sealed class Tokenizer
    {
        private readonly string _s;
        private int _i;

        public Tokenizer(string s) => _s = s ?? "";

        public char PeekChar()
            => _i >= _s.Length ? '\0' : _s[_i];

        public char ReadChar()
            => _i >= _s.Length ? '\0' : _s[_i++];

        public void SkipWhitespace()
        {
            while (true)
            {
                var c = PeekChar();
                if (c == '\0') return;

                // comments //...
                if (c == '/')
                {
                    if (_i + 1 < _s.Length && _s[_i + 1] == '/')
                    {
                        _i += 2;
                        while (PeekChar() != '\0' && PeekChar() != '\n') _i++;
                        continue;
                    }
                }

                if (!char.IsWhiteSpace(c)) return;
                _i++;
            }
        }

        public string ReadString()
        {
            SkipWhitespace();

            var c = PeekChar();
            if (c == '"')
                return ReadQuoted();

            // unquoted token until whitespace or brace
            var sb = new StringBuilder();
            while (true)
            {
                c = PeekChar();
                if (c == '\0' || char.IsWhiteSpace(c) || c == '{' || c == '}') break;
                sb.Append(ReadChar());
            }
            return sb.ToString();
        }

        public string? ReadStringOrNull()
        {
            SkipWhitespace();
            if (PeekChar() == '\0') return null;
            if (PeekChar() == '}') return null;
            return ReadString();
        }

        private string ReadQuoted()
        {
            var quote = ReadChar(); // "
            _ = quote;

            var sb = new StringBuilder();
            while (true)
            {
                var c = ReadChar();
                if (c == '\0') break;
                if (c == '"') break;

                // basic escapes
                if (c == '\\')
                {
                    var n = PeekChar();
                    if (n == '"' || n == '\\')
                    {
                        sb.Append(ReadChar());
                        continue;
                    }
                }

                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
