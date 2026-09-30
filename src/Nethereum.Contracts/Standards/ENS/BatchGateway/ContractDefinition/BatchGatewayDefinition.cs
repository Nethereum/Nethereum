using System.Collections.Generic;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Contracts.Standards.ENS.BatchGateway.ContractDefinition
{
    /// <summary>
    /// ENSIP-21 Batch Gateway Offchain Lookup Protocol (BGOLP) wire format:
    /// query((address sender, string[] urls, bytes data)[] requests) -> (bool[] failures, bytes[] responses).
    /// The Universal Resolver bundles multiple EIP-3668 reads into a single query; an ENSIP-21 aware client
    /// can decode it and perform each inner read locally (the x-batch-gateway:true sentinel).
    /// </summary>
    public partial class BatchGatewayRequest : BatchGatewayRequestBase { }

    public class BatchGatewayRequestBase
    {
        [Parameter("address", "sender", 1)]
        public virtual string Sender { get; set; }
        [Parameter("string[]", "urls", 2)]
        public virtual List<string> Urls { get; set; }
        [Parameter("bytes", "data", 3)]
        public virtual byte[] Data { get; set; }
    }

    public partial class QueryFunction : QueryFunctionBase { }

    [Function("query", typeof(QueryOutputDTO))]
    public class QueryFunctionBase : FunctionMessage
    {
        [Parameter("tuple[]", "requests", 1)]
        public virtual List<BatchGatewayRequest> Requests { get; set; }
    }

    public partial class QueryOutputDTO : QueryOutputDTOBase { }

    [FunctionOutput]
    public class QueryOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool[]", "failures", 1)]
        public virtual List<bool> Failures { get; set; }
        [Parameter("bytes[]", "responses", 2)]
        public virtual List<byte[]> Responses { get; set; }
    }
}
