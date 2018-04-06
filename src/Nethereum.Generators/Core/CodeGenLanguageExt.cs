using System;

namespace Nethereum.Generators.Core
{
    public static class CodeGenLanguageExt
    {
        public static string GetCodeOutputFileExtension(this CodeGenLanguage codeGenLanguage)
        {
            if (codeGenLanguage == CodeGenLanguage.CSharp)
                return "cs";
            else if (codeGenLanguage == CodeGenLanguage.Vb)
            {
                return "vb";
            }
            else if (codeGenLanguage == CodeGenLanguage.Proto)
            {
                return "proto";
            }
            else if (codeGenLanguage == CodeGenLanguage.FSharp)
            {
                return "fs";
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(codeGenLanguage), codeGenLanguage, null);
            }
        }
    }
}