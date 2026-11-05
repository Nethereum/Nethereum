using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nethereum.Siwe.Core
{
    public class SiweMessageParser
    {
        public const string DOMAIN = "(?<domain>([^?#]*)) wants you to sign in with your Ethereum account:";
        public const string ADDRESS = "\\n(?<address>0x[a-zA-Z0-9]{40})\\n\\n";
        public const string STATEMENT = "((?<statement>[^\\n]+)\\n)?";
        public const string URI = "(([^:?#]+):)?(([^?#]*))?([^?#]*)(\\?([^#]*))?(#(.*))";
        public const string URI_LINE = $"\\nURI: (?<uri>{URI}?)";
        public const string VERSION = "\\nVersion: (?<version>1)";
        public const string CHAIN_ID = "\\nChain ID: (?<chainId>[0-9]+)";
        public const string NONCE = "\\nNonce: (?<nonce>[a-zA-Z0-9]{8,})";
        public const string DATETIME = @"([0-9]+)-(0[1-9]|1[012])-(0[1-9]|[12][0-9]|3[01])[Tt]([01][0-9]|2[0-3]):([0-5][0-9]):([0-5][0-9]|60)(\.[0-9]+)?(([Zz])|([\+|\-]([01][0-9]|2[0-3]):[0-5][0-9]))";
        public const string ISSUED_AT = $"\\nIssued At: (?<issuedAt>{DATETIME})";
        public const string EXPIRATION_TIME = $"(\\nExpiration Time: (?<expirationTime>{DATETIME}))?";
        public const string NOT_BEFORE = $"(\\nNot Before: (?<notBefore>{DATETIME}))?";
        public const string REQUEST_ID = "(\\nRequest ID: (?<requestId>[-._~!$&'()*+,;=:@%a-zA-Z0-9]*))?";
        public const string RESOURCES = $"(\\nResources:(?<resources>(\\n- {URI}?)+))?";
        public const string MESSAGE = $@"^{DOMAIN}{ADDRESS}{STATEMENT}{URI_LINE}{VERSION}{CHAIN_ID}{NONCE}{ISSUED_AT}{EXPIRATION_TIME}{NOT_BEFORE}{REQUEST_ID}{RESOURCES}";
        
       

        public const int MaxMessageLength = 64 * 1024;

        private const string HeaderSuffix = " wants you to sign in with your Ethereum account:";

        private const string RequestIdPunctuation = "-._~!$&'()*+,;=:@%";

        public static SiweMessage Parse(string siweMessage)
        {
            if (string.IsNullOrEmpty(siweMessage))
                throw new ArgumentException("Siwe Message cannot be null or empty", nameof(siweMessage));

            var message = TryParse(siweMessage);
            if (message == null)
                throw new ArgumentException("Invalid Siwe Message", nameof(siweMessage));
            return message;
        }

        private sealed class LineReader
        {
            private readonly string _text;
            private int _position;

            public LineReader(string text)
            {
                _text = text;
            }

            public string Next()
            {
                if (_position > _text.Length) return null;
                var end = _text.IndexOf('\n', _position);
                string line;
                if (end < 0)
                {
                    line = _text.Substring(_position);
                    _position = _text.Length + 1;
                }
                else
                {
                    line = _text.Substring(_position, end - _position);
                    _position = end + 1;
                }
                return line;
            }
        }

        private static SiweMessage TryParse(string text)
        {
            if (text.Length > MaxMessageLength) return null;

            var end = text.Length;
            while (end > 0 && text[end - 1] == '\n')
                end -= end > 1 && text[end - 2] == '\r' ? 2 : 1;
            if (end < text.Length) text = text.Substring(0, end);

            var reader = new LineReader(text);
            var message = new SiweMessage();

            var header = reader.Next();
            if (header == null || !header.EndsWith(HeaderSuffix, StringComparison.Ordinal)) return null;
            var domain = header.Substring(0, header.Length - HeaderSuffix.Length);
            if (domain.Length == 0 || domain.IndexOf('?') >= 0 || domain.IndexOf('#') >= 0) return null;
            message.Domain = domain;

            var address = reader.Next();
            if (address == null || !IsAddress(address)) return null;
            message.Address = address;

            if (reader.Next() != "") return null;

            var line = reader.Next();
            if (line == null) return null;
            if (line.Length > 0)
            {
                message.Statement = line;
                if (reader.Next() != "") return null;
                line = reader.Next();
            }
            else
            {
                line = reader.Next();
            }

            if (!TryField(line, "URI: ", out var uri) || uri.Length == 0) return null;
            message.Uri = uri;

            if (!TryField(reader.Next(), "Version: ", out var version) || version != "1") return null;
            message.Version = version;

            if (!TryField(reader.Next(), "Chain ID: ", out var chainId) || !IsDigits(chainId)) return null;
            message.ChainId = chainId;

            if (!TryField(reader.Next(), "Nonce: ", out var nonce) || !IsNonce(nonce)) return null;
            message.Nonce = nonce;

            if (!TryField(reader.Next(), "Issued At: ", out var issuedAt) || !IsDateTime(issuedAt)) return null;
            message.IssuedAt = issuedAt;

            line = reader.Next();

            if (TryField(line, "Expiration Time: ", out var expiration))
            {
                if (!IsDateTime(expiration)) return null;
                message.ExpirationTime = expiration;
                line = reader.Next();
            }

            if (TryField(line, "Not Before: ", out var notBefore))
            {
                if (!IsDateTime(notBefore)) return null;
                message.NotBefore = notBefore;
                line = reader.Next();
            }

            if (TryField(line, "Request ID: ", out var requestId))
            {
                if (!IsRequestId(requestId)) return null;
                message.RequestId = requestId;
                line = reader.Next();
            }

            if (line == "Resources:")
            {
                var resources = new List<string>();
                var count = 0;
                while ((line = reader.Next()) != null)
                {
                    if (!line.StartsWith("- ", StringComparison.Ordinal)) return null;
                    count++;
                    if (line.Length > 2) resources.Add(line.Substring(2));
                }
                if (count == 0) return null;
                message.Resources = resources;
                return message;
            }

            return line == null ? message : null;
        }

        private static bool TryField(string line, string prefix, out string value)
        {
            if (line != null && line.StartsWith(prefix, StringComparison.Ordinal))
            {
                value = line.Substring(prefix.Length);
                return true;
            }
            value = null;
            return false;
        }

        private static bool IsAddress(string value)
        {
            if (value.Length != 42 || value[0] != '0' || value[1] != 'x') return false;
            for (var i = 2; i < 42; i++)
                if (!IsHex(value[i])) return false;
            return true;
        }

        private static bool IsHex(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }

        private static bool IsDigit(char c)
        {
            return c >= '0' && c <= '9';
        }

        private static bool IsLetterOrDigit(char c)
        {
            return IsDigit(c) || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }

        private static bool IsDigits(string value)
        {
            if (value.Length == 0) return false;
            for (var i = 0; i < value.Length; i++)
                if (!IsDigit(value[i])) return false;
            return true;
        }

        private static bool IsNonce(string value)
        {
            if (value.Length < 8) return false;
            for (var i = 0; i < value.Length; i++)
                if (!IsLetterOrDigit(value[i])) return false;
            return true;
        }

        private static bool IsRequestId(string value)
        {
            for (var i = 0; i < value.Length; i++)
                if (!IsLetterOrDigit(value[i]) && RequestIdPunctuation.IndexOf(value[i]) < 0) return false;
            return true;
        }

        private static bool TwoDigits(string value, int index, int min, int max)
        {
            if (index + 2 > value.Length || !IsDigit(value[index]) || !IsDigit(value[index + 1])) return false;
            var number = (value[index] - '0') * 10 + (value[index + 1] - '0');
            return number >= min && number <= max;
        }

        private static bool IsDateTime(string value)
        {
            var i = 0;
            while (i < value.Length && IsDigit(value[i])) i++;
            if (i == 0 || i >= value.Length || value[i] != '-') return false;
            i++;
            if (!TwoDigits(value, i, 1, 12)) return false;
            i += 2;
            if (i >= value.Length || value[i] != '-') return false;
            i++;
            if (!TwoDigits(value, i, 1, 31)) return false;
            i += 2;
            if (i >= value.Length || (value[i] != 'T' && value[i] != 't')) return false;
            i++;
            if (!TwoDigits(value, i, 0, 23)) return false;
            i += 2;
            if (i >= value.Length || value[i] != ':') return false;
            i++;
            if (!TwoDigits(value, i, 0, 59)) return false;
            i += 2;
            if (i >= value.Length || value[i] != ':') return false;
            i++;
            if (!TwoDigits(value, i, 0, 60)) return false;
            i += 2;
            if (i < value.Length && value[i] == '.')
            {
                i++;
                var start = i;
                while (i < value.Length && IsDigit(value[i])) i++;
                if (i == start) return false;
            }
            if (i >= value.Length) return false;
            if (value[i] == 'Z' || value[i] == 'z') return i + 1 == value.Length;
            if (value[i] != '+' && value[i] != '-') return false;
            i++;
            if (!TwoDigits(value, i, 0, 23)) return false;
            i += 2;
            if (i >= value.Length || value[i] != ':') return false;
            i++;
            if (!TwoDigits(value, i, 0, 59)) return false;
            return i + 2 == value.Length;
        }

        public static SiweMessage ParseUsingAbnf(string siweMessage)
        {
            var siweMessageRule =
                Parser.Parse(
                    "sign-in-with-ethereum",
                    siweMessage);
            var visitor = new MessageExtractor();
            siweMessageRule.Accept(visitor);
            return visitor.SiweMessage;
        }

    }
}
