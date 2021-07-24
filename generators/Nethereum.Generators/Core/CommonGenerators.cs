using System;

namespace Nethereum.Generators.Core
{
    public class CommonGenerators
    {
        private Utils utils;

        public CommonGenerators()
        {
            utils = new Utils();
        }

        //https://docs.microsoft.com/en-us/dotnet/visual-basic/language-reference/keywords/
        //https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/
        //https://docs.microsoft.com/en-us/dotnet/fsharp/language-reference/keyword-reference
        //https://stackoverflow.com/questions/6639688/using-keywords-as-identifiers-in-f

        public string GenerateVariableName(string value, CodeGenLanguage codeGenLanguage)
        {
            return utils.LowerCaseFirstCharAndRemoveUnderscorePrefix(value).EscapeKeywordMatch(codeGenLanguage);
        }

        public string GeneratePropertyName(string value, CodeGenLanguage codeGenLanguage)
        {
            return utils.CapitaliseFirstCharAndRemoveUnderscorePrefix(value).EscapeKeywordMatch(codeGenLanguage);
        }

        public string GenerateClassName(string value)
        {
            return utils.CapitaliseFirstCharAndRemoveUnderscorePrefix(value);
        }
    }


    public static class Keywords
    {
        public static string[] CSharp = new string[] {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const",
            "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern",
            "FALSE", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface",
            "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override",
            "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
            "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "TRUE", "try", "typeof",
            "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while"
        };

        public static string[] VbNet = new string[]
        {
            "AddHandler", "AddressOf", "Alias", "And", "AndAlso", "As", "Boolean", "ByRef", "Byte", "ByVal", "Call",
            "Case", "Catch", "CBool", "CByte", "CChar", "CDate", "CDbl", "CDec", "Char", "CInt", "Class", "CLng",
            "CObj", "Const", "Continue", "CSByte", "CShort", "CSng", "CStr", "CType", "CUInt", "CULng", "CUShort",
            "Date", "Decimal", "Declare", "Default", "Delegate", "Dim", "DirectCast", "Do", "Double", "Each", "Else",
            "ElseIf", "End", "EndIf", "Enum", "Erase", "Error", "Event", "Exit", "Finally", "For", "Friend", "Function",
            "Get", "GetType", "GetXMLNamespace", "Global", "GoSub", "GoTo", "Handles", "If", "Implements", "Imports",
            "In", "Inherits", "Integer", "Interface", "Is", "IsNot", "Let", "Lib", "Like", "Long", "Loop", "Me", "Mod",
            "Module", "MustInherit", "MustOverride", "MyBase", "MyClass", "NameOf", "Namespace", "Narrowing", "New",
            "Next", "Not", "Nothing", "NotInheritable", "NotOverridable", "Object", "Of", "On", "Operator", "Option",
            "Optional", "Or", "OrElse", "Out", "Overloads", "Overridable", "Overrides", "ParamArray", "Partial",
            "Private", "Property", "Protected", "Public", "RaiseEvent", "ReadOnly", "ReDim", "REM", "RemoveHandler",
            "Resume", "Return", "SByte", "Select", "Set", "Shadows", "Shared", "Short", "Single", "Static", "Step",
            "Stop", "String", "Structure", "Sub", "SyncLock", "Then", "Throw", "To", "Try", "TryCast", "TypeOf",
            "UInteger", "ULong", "UShort", "Using", "Variant", "Wend", "When", "While", "Widening", "With",
            "WithEvents", "WriteOnly", "Xor", "FALSE", "TRUE"
        };

        public static string[] FSharp = new string[]
        {
            "fun", "function", "global", "if", "in", "inherit", "inline", "interface", "internal", "lazy", "let",
            "let!", "match", "match!", "member", "module", "mutable", "namespace", "new", "not", "null", "of", "open",
            "or", "override", "private", "public", "rec", "return", "return!", "select", "static", "struct", "then",
            "to", "TRUE", "try", "type", "upcast", "use", "use!", "val", "void", "when", "while", "with", "yield",
            "yield!", "const", "yield", "const", "asr", "land", "lor", "lsl", "lsr", "lxor", "mod", "sig", "atomic",
            "break", "checked", "component", "const", "constraint", "constructor", "continue", "eager", "event",
            "external", "functor", "include", "method", "mixin", "object", "parallel", "process", "protected", "pure",
            "sealed", "tailcall", "trait", "virtual", "volatile"
        };

        

        public static string EscapeKeywordMatch(this string value, CodeGenLanguage codeGenLanguage)
        {
            switch (codeGenLanguage)
            {
                case CodeGenLanguage.CSharp:
                    if (IsMatch(value, CSharp))
                    {
                        return "@" + value;
                    }

                    return value;
                case CodeGenLanguage.Vb:
                    if (IsMatch(value, VbNet))
                    {
                        //vb is already escaped
                        // return "[" + value + "]";
                        return value;
                    }
                    return value;
                case CodeGenLanguage.FSharp:
                    if (IsMatch(value, FSharp))
                    {
                        return "``" + value + "``";
                    }
                    return value;
                default:
                    return value;
            }
        }

        public static bool IsMatch(string value, string[] list)
        {
            foreach (var item in list)
            {
                if (value == item) return true;
            }

            return false;
        }

    }

}