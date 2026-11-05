using System.Globalization;

namespace Nethereum.ABI.ABIDeserialisation
{
    internal static class AbiSignatureScanner
    {
        internal static bool IsSpace(char c)
        {
            return char.IsWhiteSpace(c) && (c < '\u001C' || c > '\u001F');
        }

        internal static bool IsWord(char c)
        {
            switch (CharUnicodeInfo.GetUnicodeCategory(c))
            {
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                case UnicodeCategory.ModifierLetter:
                case UnicodeCategory.OtherLetter:
                case UnicodeCategory.NonSpacingMark:
                case UnicodeCategory.DecimalDigitNumber:
                case UnicodeCategory.ConnectorPunctuation:
                    return true;
                default:
                    return false;
            }
        }

        internal static int[] BuildPartners(string text)
        {
            var partners = new int[text.Length];
            var stack = new int[text.Length];
            var top = 0;
            for (var i = 0; i < text.Length; i++)
            {
                partners[i] = -1;
                if (text[i] == '(')
                {
                    stack[top++] = i;
                }
                else if (text[i] == ')' && top > 0)
                {
                    var open = stack[--top];
                    partners[open] = i;
                    partners[i] = open;
                }
            }
            return partners;
        }

        internal static bool TryFirstBalanced(string text, int[] partners, int from, out string inner)
        {
            for (var i = from; i < text.Length; i++)
            {
                if (text[i] == '(' && partners[i] >= 0)
                {
                    inner = text.Substring(i + 1, partners[i] - i - 1);
                    return true;
                }
            }
            inner = "";
            return false;
        }

        internal static bool TryMatchHead(string text, int[] partners, out string scope, out string name, out string parameters)
        {
            for (var q = 0; q < text.Length; q++)
            {
                if (text[q] != '(' || partners[q] < 0) continue;

                var p = q;
                while (p > 0 && IsSpace(text[p - 1])) p--;
                while (p > 0 && IsWord(text[p - 1])) p--;
                while (p > 0 && IsSpace(text[p - 1])) p--;
                while (p > 0 && IsWord(text[p - 1])) p--;

                var i = p;
                var scopeEnd = i;
                while (scopeEnd < q && IsWord(text[scopeEnd])) scopeEnd++;
                i = scopeEnd;
                while (i < q && IsSpace(text[i])) i++;
                var nameStart = i;
                while (i < q && IsWord(text[i])) i++;
                var nameEnd = i;
                while (i < q && IsSpace(text[i])) i++;
                if (i != q) break;

                scope = text.Substring(p, scopeEnd - p);
                name = text.Substring(nameStart, nameEnd - nameStart);
                parameters = text.Substring(q + 1, partners[q] - q - 1);
                return true;
            }

            scope = "";
            name = "";
            parameters = "";
            return false;
        }

        internal static string ReturnsParameters(string text, int[] partners)
        {
            const string keyword = "returns";
            var index = text.IndexOf(keyword, 0, System.StringComparison.Ordinal);
            while (index >= 0)
            {
                var i = index + keyword.Length;
                while (i < text.Length && IsSpace(text[i])) i++;
                if (i < text.Length && text[i] == '(' && partners[i] >= 0)
                    return text.Substring(i + 1, partners[i] - i - 1);
                index = text.IndexOf(keyword, index + 1, System.StringComparison.Ordinal);
            }
            return "";
        }

        internal static bool HasStateModifier(string text)
        {
            for (var close = 0; close < text.Length; close++)
            {
                if (text[close] != ')') continue;

                var a = close + 1;
                while (a < text.Length && IsSpace(text[a])) a++;
                if (a > close + 1 && StartsWithModifier(text, a)) return true;

                var b = a;
                while (b < text.Length && IsWord(text[b])) b++;
                var c = b;
                while (c < text.Length && IsSpace(text[c])) c++;
                if (c > b && StartsWithModifier(text, c)) return true;
            }
            return false;
        }

        private static bool StartsWithModifier(string text, int index)
        {
            return string.CompareOrdinal(text, index, "view", 0, 4) == 0
                   || string.CompareOrdinal(text, index, "pure", 0, 4) == 0
                   || string.CompareOrdinal(text, index, "constant", 0, 8) == 0;
        }
    }
}
