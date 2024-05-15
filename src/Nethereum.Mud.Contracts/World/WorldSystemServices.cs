using Nethereum.Web3;
using System.Collections.Generic;
using Nethereum.Mud.Contracts.World.Systems.AccessManagementSystem;
using Nethereum.Mud.Contracts.World.Systems.BalanceTransferSystem;
using Nethereum.Mud.Contracts.World.Systems.BatchCallSystem;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem;
using Nethereum.Mud.Contracts.Core.Systems;

namespace Nethereum.Mud.Contracts.World
{
    public class WorldSystemServices : SystemsServices
    {
        public WorldSystemServices(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
            AccessManagementSystem = new AccessManagementSystemService(web3, contractAddress);
            BalanceTransferSystem = new BalanceTransferSystemService(web3, contractAddress);
            RegistrationSystem = new RegistrationSystemService(web3, contractAddress);
            SystemServices = new List<ISystemService> { AccessManagementSystem, BalanceTransferSystem, BatchCallSystem, RegistrationSystem };
        }

        public AccessManagementSystemService AccessManagementSystem { get; protected set; }
        public BalanceTransferSystemService BalanceTransferSystem { get; protected set; }
        public RegistrationSystemService RegistrationSystem { get; protected set; }
    }
}
