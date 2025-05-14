using System;
using System.Collections.Generic;
using System.IO;

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

        public static IEnumerable<string> GetValidProjectFileExtensions()
        {
            return ProjectFileExtensions.Values;
        }

        /// <summary>
        /// Only necessary for DuoCode as it doesnt support StringComparer
        /// </summary>
        public class StringComparerIgnoreCase : IEqualityComparer<string>
        {
            public bool Equals(string x, string y)
            {
                return GetHashCode(x) == GetHashCode(y);
            }

            public int GetHashCode(string obj)
            {
                if(obj == null)
                    throw new ArgumentNullException("obj");

                return obj.ToLowerInvariant().GetHashCode();
            }
        }

        public static readonly Dictionary<string, CodeGenLanguage> LanguageMappings = 
            new Dictionary<string, CodeGenLanguage>(new StringComparerIgnoreCase())
        {
            {"C#", CodeGenLanguage.CSharp},
            {"CSharp", CodeGenLanguage.CSharp},
            {"F#", CodeGenLanguage.FSharp},
            {"FSharp", CodeGenLanguage.FSharp},
            {"VB", CodeGenLanguage.Vb}
        };

        public static readonly Dictionary<CodeGenLanguage, string> DotNetCliLanguage = 
            new Dictionary<CodeGenLanguage, string>
        {
            {CodeGenLanguage.CSharp, "C#"},
            {CodeGenLanguage.FSharp, "F#"},
            {CodeGenLanguage.Vb, "VB"}
        };

        public static CodeGenLanguage ParseLanguage(string languageTag)
        {
            if (LanguageMappings.ContainsKey(languageTag))
                return LanguageMappings[languageTag];


            throw new ArgumentException($"Unknown or unsupported language '{languageTag}'");
        }

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

        public static CodeGenLanguage GetCodeGenLanguageFromProjectFile(string projectFilePath)
        {
            var projectFileExtension = Path.GetExtension(projectFilePath);
            foreach (var language in ProjectFileExtensions.Keys)
            {
                var extension = ProjectFileExtensions[language];
                if (extension.Equals(projectFileExtension, StringComparison.InvariantCultureIgnoreCase))
                {
                    return language;
                }
            }

            throw new ArgumentException($"Unsupported or unrecognised file extension for project file path '{projectFilePath}'");
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
            else if (codeGenLanguage == CodeGenLanguage.Razor)
            {
                return "razor";
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(codeGenLanguage), codeGenLanguage, null);
            }
        }
    }
}