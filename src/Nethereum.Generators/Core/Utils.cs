namespace Nethereum.Generators.Core
{
    public class Utils
    {
        public string RemoveUnderscorePrefix(string value)
        {
            return value.TrimStart('_');
        }

        public string LowerCaseFirstCharAndRemoveUnderscorePrefix(string value)
        {
            value = RemoveUnderscorePrefix(value);
            return LowerCaseFirstChar(value);
        }

        public string CapitaliseFirstCharAndRemoveUnderscorePrefix(string value)
        {
            value = RemoveUnderscorePrefix(value);
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

        public string GetBooleanAsString(bool value)
        {
            if (value) return "true";
            return "false";
        }
    }
}