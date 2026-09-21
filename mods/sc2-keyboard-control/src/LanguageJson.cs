using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace SCDEKeyboardControl
{
    // Text-only JSON reader. No Unity native calls during plugin construction or language loading.
    internal sealed class LanguageJson
    {
        private readonly string _text;
        private int _position;

        private LanguageJson(string text) { _text = text; }

        internal static Dictionary<string, object> Read(string text)
        {
            if (text == null || text.Length > 512 * 1024) throw new InvalidDataException("Invalid language JSON size");
            var reader = new LanguageJson(text.TrimStart('\uFEFF'));
            var result = reader.Value(0) as Dictionary<string, object>;
            reader.WhiteSpace();
            if (result == null || reader._position != reader._text.Length) throw Invalid();
            return result;
        }

        private object Value(int depth)
        {
            if (depth > 32) throw Invalid();
            WhiteSpace();
            if (_position == _text.Length) throw Invalid();
            char token = _text[_position];
            if (token == '"') return String();
            if (token == '{')
            {
                _position++;
                var map = new Dictionary<string, object>(StringComparer.Ordinal);
                if (Take('}')) return map;
                do
                {
                    WhiteSpace();
                    string key = String();
                    if (!Take(':')) throw Invalid();
                    map[key] = Value(depth + 1);
                } while (Take(','));
                if (!Take('}')) throw Invalid();
                return map;
            }
            if (token == '[')
            {
                // Arrays may occur in translator metadata but are never UI text.
                _position++;
                if (Take(']')) return null;
                do { Value(depth + 1); } while (Take(','));
                if (!Take(']')) throw Invalid();
                return null;
            }
            foreach (string literal in new[] { "null", "true", "false" })
            {
                if (_position + literal.Length <= _text.Length &&
                    System.String.CompareOrdinal(_text, _position, literal, 0, literal.Length) == 0)
                {
                    _position += literal.Length;
                    return null;
                }
            }
            Match number = Regex.Match(_text.Substring(_position), @"^-?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?");
            if (!number.Success) throw Invalid();
            _position += number.Length;
            return null;
        }

        private string String()
        {
            if (_position >= _text.Length || _text[_position++] != '"') throw Invalid();
            var value = new StringBuilder();
            while (_position < _text.Length)
            {
                char c = _text[_position++];
                if (c == '"') return value.ToString();
                if (c < 0x20) throw Invalid();
                if (c != '\\') { value.Append(c); continue; }
                if (_position == _text.Length) throw Invalid();
                switch (_text[_position++])
                {
                    case '"': value.Append('"'); break;
                    case '\\': value.Append('\\'); break;
                    case '/': value.Append('/'); break;
                    case 'b': value.Append('\b'); break;
                    case 'f': value.Append('\f'); break;
                    case 'n': value.Append('\n'); break;
                    case 'r': value.Append('\r'); break;
                    case 't': value.Append('\t'); break;
                    case 'u':
                        ushort code;
                        if (_position + 4 > _text.Length || !UInt16.TryParse(_text.Substring(_position, 4),
                            NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out code)) throw Invalid();
                        value.Append((char)code);
                        _position += 4;
                        break;
                    default: throw Invalid();
                }
            }
            throw Invalid();
        }

        private bool Take(char token)
        {
            WhiteSpace();
            if (_position == _text.Length || _text[_position] != token) return false;
            _position++;
            return true;
        }

        private void WhiteSpace()
        {
            while (_position < _text.Length && (_text[_position] == ' ' || _text[_position] == '\t' ||
                _text[_position] == '\r' || _text[_position] == '\n')) _position++;
        }

        private static InvalidDataException Invalid() { return new InvalidDataException("Invalid language JSON"); }
    }
}
