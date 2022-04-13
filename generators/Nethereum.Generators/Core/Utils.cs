using System;

namespace Nethereum.Generators.Core
{
    public class Utils
    {
        public string RemoveUnderscorePrefix(string value)
        {
            return value?.TrimStart('_') ?? string.Empty;
        }

        public string LowerCaseFirstCharAndRemoveUnderscorePrefix(string value)
        {
            value = RemoveUnderscorePrefix(value);
            value = ConvertIfAllCapitalsToLower(value);
            value = UnderscoreToCamelCase(value);
            return LowerCaseFirstChar(value);
        }

        public string CapitaliseFirstCharAndRemoveUnderscorePrefix(string value)
        {
            value = RemoveUnderscorePrefix(value);
            value = ConvertIfAllCapitalsToLower(value);
            value = UnderscoreToCamelCase(value);
            return CapitaliseFirstChar(value);
        }

        public string LowerCaseFirstChar(string value)
        {
            return value.Substring(0, 1).ToLower() + value.Substring(1);
        }

        public string CapitaliseFirstChar(string value)
        {
            return value.Substring(0, 1).ToUpper() + value.Substring(1);
        }

        private string ConvertIfAllCapitalsToLower(string value)
        {
            if (IsAllUpper(value))
            {
                return value.ToLower();
            }

            return value;
        }

        public string GetBooleanAsString(bool value)
        {
            if (value) return "true";
            return "false";
        }

        private bool IsAllUpper(string input)
        {
            for (var i = 0; i < input.Length; i++)
            {
                if (char.IsLetter(input[i]) && !char.IsUpper(input[i]))
                    return false;
            }
            return true;
        }

        private string UnderscoreToCamelCase(string name)
        {
            if (string.IsNullOrEmpty(name) || !name.Contains("_"))
            {
                return name;
            }
            var array = name.Split('_');
            for (int i = 0; i < array.Length; i++)
            {
                var s = array[i];
                var first = string.Empty;
                var rest = string.Empty;
                if (s.Length > 0)
                {
                    first = s[0].ToString().ToUpperInvariant();
                }
                if (s.Length > 1)
                {
                    rest = s.Substring(1).ToLowerInvariant();
                }
                array[i] = first + rest;
            }
            var newName = string.Join("", array);
            if (newName.Length > 0)
            {
                newName = newName[0].ToString().ToUpperInvariant() + newName.Substring(1);
            }
            else
            {
                newName = name;
            }
            return newName;
        }
    }
}