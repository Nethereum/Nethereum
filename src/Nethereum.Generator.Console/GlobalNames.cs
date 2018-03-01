namespace Nethereum.Generator.Console
{
    public class GlobalNames
    {
        public GlobalNames()
        {
            Utils = new Utils();
        }

        public Utils Utils { get; }

        public string GetFunctionMessageName(string functionName)
        {
            return Utils.CapitaliseFirstCharAndRemoveUnderscorePrefix(functionName) + "FunctionMessage";
        }
    }
}