using System;
using System.Collections.Generic;
using System.IO;

namespace Nethereum.Generators.Core
{
    public static class CodeGenLanguageExt
    {

        private static readonly Dictionary<CodeGenLanguage, string> ProjectFileExtensions = new Dictionary<CodeGenLanguage, string>
        {
            {CodeGenLanguage.CSharp, ".csproj"},
            {CodeGenLanguage.FSharp, ".fsproj"},
            {CodeGenLanguage.Vb, ".vbproj"}
        };

        public static string AddProjectFileExtension(this CodeGenLanguage language, string projectFileName)
        {
            if (string.IsNullOrEmpty(projectFileName))
                throw new ArgumentNullException(nameof(projectFileName));
            

            if (ProjectFileExtensions.ContainsKey(language))
            {
                var extension = ProjectFileExtensions[language];
                var requestedExtension = Path.GetExtension(projectFileName);
                if (string.IsNullOrEmpty(requestedExtension))
                    return $"{projectFileName.TrimEnd('.')}{extension}";

                return Path.ChangeExtension(projectFileName, extension);
            }

            return null;
        }

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