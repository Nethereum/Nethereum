namespace Nethereum.Generator.Console.Generators.CQS
{
    public class CQSMessageModel : CommonModel
    {
        public string FunctionName { get; set; }
        public CQSMessageModel(string abi, string byteCode, string functionName,
            string namespaceName = CommonModel.DEFAULT_NAMESPACE) : base(abi, byteCode, namespaceName)
        {
            FunctionName = functionName;
        }

        public string GetFunctionMessageName()
        {
            return GlobalNames.GetFunctionMessageName(FunctionName);
        }
    }
}