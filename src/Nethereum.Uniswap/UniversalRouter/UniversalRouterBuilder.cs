using Nethereum.Uniswap.UniversalRouter.Commands;
using Nethereum.Uniswap.UniversalRouter.ContractDefinition;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Nethereum.Uniswap.UniversalRouter
{
    public class UniversalRouterBuilder
    {
        public List<UniversalRouterCommand> Commands { get; } = new List<UniversalRouterCommand>();
        public void AddCommand(UniversalRouterCommand command)
        {
            Commands.Add(command);
        }

        public byte[] GetCommands()
        {
            List<byte> result = new List<byte>();
            foreach (var command in Commands)
            {
                 result.Add(command.GetFullCommandType());
            }
            return result.ToArray();
        }

        public List<byte[]> GetInputData()
        {
            return Commands.Select(c => c.GetInputData()).ToList();
        }

        public ExecuteFunction GetExecuteFunction(BigInteger nativeAmount)
        {
            return new ExecuteFunction()
            {
                Commands = GetCommands(),
                Inputs = GetInputData(),
                AmountToSend = nativeAmount
            };
        }

        public ExecuteFunction GetExecuteFunction()
        {
            return GetExecuteFunction(0);
        }
    }
}
