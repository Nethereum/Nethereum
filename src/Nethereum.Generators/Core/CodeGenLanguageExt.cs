using System;

namespace Nethereum.Generators.Core
{
    public static class CodeGenLanguageExt
    {
        public static string GetCodeOutputFileExtension(this CodeGenLanguage codeGenLanguage)
        {
            switch (codeGenLanguage)
            {
                case CodeGenLanguage.CSharp:
                    return "cs";
                case CodeGenLanguage.Vb:
                    return "vb";
                case CodeGenLanguage.Proto:
                    return "proto";
                case CodeGenLanguage.FSharp:
                    return "fs";
                default:
                    throw new ArgumentOutOfRangeException(nameof(codeGenLanguage), codeGenLanguage, null);
            }
        }
    }
}