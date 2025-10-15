using Nethereum.Uniswap.UniversalRouter.Commands;
using Nethereum.Uniswap.UniversalRouter.V4Actions;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.Uniswap.UniversalRouter
{
    public class UniversalRouterV4ActionsBuilder
    {
        public List<V4ActionRouterCommand> Commands { get; } = new List<V4ActionRouterCommand>();
        public void AddCommand(V4ActionRouterCommand command)
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

        public V4SwapCommand GetV4SwapCommand()
        {
            return new V4SwapCommand
            {
                Actions = GetCommands(),
                Inputs = GetInputData()
            };
        }
    }
}
