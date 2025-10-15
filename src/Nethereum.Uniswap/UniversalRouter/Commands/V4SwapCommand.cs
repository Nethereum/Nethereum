using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Collections.Generic;


namespace Nethereum.Uniswap.UniversalRouter.Commands
{
    public class V4SwapCommand: UniversalRouterCommand
    {
        public override byte CommandType { get; set; } = (byte)UniversalRouterCommandType.V4_SWAP;

        [Parameter("bytes", "actions", 1)]
        public virtual byte[] Actions { get; set; }
        [Parameter("bytes[]", "inputs", 2)]
        public virtual List<byte[]> Inputs { get; set; }
    }


}
