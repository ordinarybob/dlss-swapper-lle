using System.Text;

namespace DlssSwapper.Linux.Cli.Platform;

public sealed record ValveKeyValueEntry(
    string Key,
    string? Value,
    ValveKeyValueObject? Children);

public sealed class ValveKeyValueObject
{
    internal ValveKeyValueObject(List<ValveKeyValueEntry> entries)
    {
        Entries = entries;
    }

    public IReadOnlyList<ValveKeyValueEntry> Entries { get; }

    public string? GetString(string key) =>
        Entries.FirstOrDefault(entry =>
            entry.Children is null
            && string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))?.Value;

    public ValveKeyValueObject? GetObject(string key) =>
        Entries.FirstOrDefault(entry =>
            entry.Children is not null
            && string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))?.Children;
}

/// <summary>
/// Minimal parser for the text form of Valve KeyValues 1.
/// </summary>
public static class ValveKeyValuesParser
{
    public static ValveKeyValueObject Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new Parser(text).ParseDocument();
    }

    private enum TokenKind
    {
        End,
        String,
        OpenBrace,
        CloseBrace,
    }

    private readonly record struct Token(TokenKind Kind, string Value, int Position);

    private sealed class Parser
    {
        private const int MaximumDepth = 64;
        private const int MaximumTokenLength = 64 * 1024;

        private readonly string _text;
        private int _position;

        internal Parser(string text)
        {
            _text = text;
        }

        internal ValveKeyValueObject ParseDocument()
        {
            if (_text.Length > 0 && _text[0] == '\uFEFF')
            {
                _position = 1;
            }

            return ParseObject(expectClosingBrace: false, depth: 0);
        }

        private ValveKeyValueObject ParseObject(bool expectClosingBrace, int depth)
        {
            if (depth > MaximumDepth)
            {
                throw Error($"KeyValues nesting exceeds {MaximumDepth} levels.", _position);
            }

            var entries = new List<ValveKeyValueEntry>();
            while (true)
            {
                var key = ReadToken();
                if (key.Kind == TokenKind.End)
                {
                    if (expectClosingBrace)
                    {
                        throw Error("Unexpected end of file; expected '}'.", key.Position);
                    }

                    return new ValveKeyValueObject(entries);
                }

                if (key.Kind == TokenKind.CloseBrace)
                {
                    if (!expectClosingBrace)
                    {
                        throw Error("Unexpected '}'.", key.Position);
                    }

                    return new ValveKeyValueObject(entries);
                }

                if (key.Kind != TokenKind.String)
                {
                    throw Error("Expected a key.", key.Position);
                }

                var value = ReadToken();
                switch (value.Kind)
                {
                    case TokenKind.String:
                        entries.Add(new ValveKeyValueEntry(key.Value, value.Value, null));
                        break;
                    case TokenKind.OpenBrace:
                        entries.Add(new ValveKeyValueEntry(
                            key.Value,
                            null,
                            ParseObject(expectClosingBrace: true, depth + 1)));
                        break;
                    default:
                        throw Error($"Missing value for key '{key.Value}'.", value.Position);
                }
            }
        }

        private Token ReadToken()
        {
            SkipTrivia();
            if (_position >= _text.Length)
            {
                return new Token(TokenKind.End, string.Empty, _position);
            }

            var start = _position;
            return _text[_position] switch
            {
                '{' => SingleCharacterToken(TokenKind.OpenBrace, start),
                '}' => SingleCharacterToken(TokenKind.CloseBrace, start),
                '"' => ReadQuotedToken(),
                _ => ReadBareToken(),
            };
        }

        private Token SingleCharacterToken(TokenKind kind, int start)
        {
            _position++;
            return new Token(kind, _text[start].ToString(), start);
        }

        private Token ReadQuotedToken()
        {
            var start = _position++;
            var value = new StringBuilder();
            while (_position < _text.Length)
            {
                var current = _text[_position++];
                if (current == '"')
                {
                    return new Token(TokenKind.String, value.ToString(), start);
                }

                if (current != '\\')
                {
                    value.Append(current);
                    EnsureTokenLength(value.Length, start);
                    continue;
                }

                if (_position >= _text.Length)
                {
                    throw Error("Unterminated escape sequence.", _position - 1);
                }

                var escaped = _text[_position++];
                value.Append(escaped switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    '"' => '"',
                    '\\' => '\\',
                    _ => '\\',
                });
                if (escaped is not ('n' or 'r' or 't' or '"' or '\\'))
                {
                    value.Append(escaped);
                }

                EnsureTokenLength(value.Length, start);
            }

            throw Error("Unterminated quoted string.", start);
        }

        private Token ReadBareToken()
        {
            var start = _position;
            while (_position < _text.Length
                   && !char.IsWhiteSpace(_text[_position])
                   && _text[_position] is not ('{' or '}'))
            {
                _position++;
                EnsureTokenLength(_position - start, start);
            }

            if (_position == start)
            {
                throw Error($"Unexpected character '{_text[_position]}'.", _position);
            }

            return new Token(TokenKind.String, _text[start.._position], start);
        }

        private void SkipTrivia()
        {
            while (_position < _text.Length)
            {
                if (char.IsWhiteSpace(_text[_position]) || _text[_position] == '\uFEFF')
                {
                    _position++;
                    continue;
                }

                if (_text[_position] == '/'
                    && _position + 1 < _text.Length
                    && _text[_position + 1] == '/')
                {
                    _position += 2;
                    while (_position < _text.Length && _text[_position] is not ('\r' or '\n'))
                    {
                        _position++;
                    }

                    continue;
                }

                break;
            }
        }

        private static void EnsureTokenLength(int length, int position)
        {
            if (length > MaximumTokenLength)
            {
                throw Error($"Token exceeds {MaximumTokenLength:N0} characters.", position);
            }
        }

        private static FormatException Error(string message, int position) =>
            new($"{message} Character {position}.");
    }
}
