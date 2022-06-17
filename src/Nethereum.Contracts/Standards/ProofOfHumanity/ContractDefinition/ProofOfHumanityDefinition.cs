using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Contracts.Standards.ProofOfHumanity.ContractDefinition
{

    public partial class IsRegisteredFunction : IsRegisteredFunctionBase { }

    [Function("isRegistered", "bool")]
    public class IsRegisteredFunctionBase : FunctionMessage
    {
        [Parameter("address", "_submissionID", 1)]
        public virtual string SubmissionID { get; set; }
    }

    public partial class IsRegisteredOutputDTO : IsRegisteredOutputDTOBase { }

    [FunctionOutput]
    public class IsRegisteredOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool", "", 1)]
        public virtual bool IsRegistered { get; set; }
    }


}
