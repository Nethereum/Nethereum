using Nethereum.ABI;
using Nethereum.Uniswap.UniversalRouter.V4Actions;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.Uniswap.V4
{
    /// <summary>
    /// Builder for encoding V4 position management actions into the format required by PositionManager.modifyLiquidities()
    /// </summary>
    public class V4PositionManagerActionsBuilder
    {
        public List<V4ActionRouterCommand> Commands { get; } = new List<V4ActionRouterCommand>();

        /// <summary>
        /// Add a V4 action command (MintPosition, IncreaseLiquidity, DecreaseLiquidity, BurnPosition, SettlePair, etc.)
        /// </summary>
        public void AddCommand(V4ActionRouterCommand command)
        {
            Commands.Add(command);
        }

        /// <summary>
        /// Get the action bytes (encodePacked equivalent - raw bytes concatenated)
        /// </summary>
        public byte[] GetActionBytes()
        {
            List<byte> result = new List<byte>();
            foreach (var command in Commands)
            {
                result.Add(command.CommandType);
            }
            return result.ToArray();
        }

        /// <summary>
        /// Get the params array (each action's encoded parameters)
        /// </summary>
        public List<byte[]> GetParams()
        {
            return Commands.Select(c => c.GetInputData()).ToList();
        }

        /// <summary>
        /// Encode the unlock data for PositionManager.modifyLiquidities()
        /// This encodes: abi.encode(actions, params) where actions is bytes and params is bytes[]
        /// </summary>
        public byte[] GetUnlockData()
        {
            var actionBytes = GetActionBytes();
            var @params = GetParams();

            var abiEncode = new ABIEncode();
            var actionBytesEncoded = abiEncode.GetABIEncodedPacked(actionBytes);

            var unlockData = abiEncode.GetABIEncoded(
                new ABIValue("bytes", actionBytesEncoded),
                new ABIValue("bytes[]", @params.ToArray())
            );

            return unlockData;
        }

        /// <summary>
        /// Clear all commands
        /// </summary>
        public void Clear()
        {
            Commands.Clear();
        }
    }
}
