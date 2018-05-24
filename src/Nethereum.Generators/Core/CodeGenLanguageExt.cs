using System;
using System.Collections.Generic;

namespace Nethereum.Generators.Core
{
    public static class CodeGenLanguageExt
    {
        public static readonly Dictionary<CodeGenLanguage, string> ProjectFileExtensions = new Dictionary<CodeGenLanguage, string>
        {
            {CodeGenLanguage.CSharp, ".csproj"},
            {CodeGenLanguage.FSharp, ".fsproj"},
            {CodeGenLanguage.Vb, ".vbproj"}
        };

        public static readonly Dictionary<CodeGenLanguage, string> DotNetCliLanguage = new Dictionary<CodeGenLanguage, string>
        {
            {CodeGenLanguage.CSharp, "C#"},
            {CodeGenLanguage.FSharp, "F#"},
            {CodeGenLanguage.Vb, "VB"}
        };

        public static string ToDotNetCli(this CodeGenLanguage language)
        {
            if(DotNetCliLanguage.ContainsKey(language))
                return DotNetCliLanguage[language];

             throw new ArgumentException($"Language isn't supported by dot net cli '{language}'");
        }

        public static string AddProjectFileExtension(this CodeGenLanguage language, string projectFileName)
        {
            if (string.IsNullOrEmpty(projectFileName))
                throw new ArgumentNullException(nameof(projectFileName));
            

            if (ProjectFileExtensions.ContainsKey(language))
            {
                var extension = ProjectFileExtensions[language];

                if (projectFileName.EndsWith(extension, StringComparison.InvariantCultureIgnoreCase))
                    return projectFileName;

                return projectFileName + extension;
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