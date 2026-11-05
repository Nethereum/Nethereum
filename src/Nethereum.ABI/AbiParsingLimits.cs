using System;

namespace Nethereum.ABI
{
    internal static class AbiParsingLimits
    {
        internal const int MaxNestingDepth = 32;

        internal static void EnsureParenthesesDepth(string text, int maxDepth)
        {
            if (text == null) return;
            var depth = 0;
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '(')
                {
                    if (++depth > maxDepth)
                        throw new ArgumentException("ABI type nesting exceeds " + MaxNestingDepth + " levels");
                }
                else if (text[i] == ')' && depth > 0)
                {
                    depth--;
                }
            }
        }

        internal static void EnsureBracketCount(string typeName)
        {
            var count = 0;
            for (var i = 0; i < typeName.Length; i++)
            {
                if (typeName[i] == '[' && ++count > MaxNestingDepth)
                    throw new ArgumentException("ABI array dimensions exceed " + MaxNestingDepth + " levels");
            }
        }
    }
}
