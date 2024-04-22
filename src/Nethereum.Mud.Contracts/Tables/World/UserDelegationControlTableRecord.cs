using Nethereum.ABI.FunctionEncoding.Attributes;
using static Nethereum.Mud.Contracts.Tables.World.UserDelegationControlTableRecord;

namespace Nethereum.Mud.Contracts.Tables.World
{
    public class UserDelegationControlTableRecord : TableRecord<UserDelegationControlKey, UserDelegationControlValue>
    {
        public UserDelegationControlTableRecord() : base("world", "UserDelegationControl")
        {
        }

        public class UserDelegationControlKey
        {
            [Parameter("address", "delegator", 1)]
            public string Delegator { get; set; }
            [Parameter("address", "delegatee", 2)]
            public string Delegatee { get; set; }
        }

        public class UserDelegationControlValue
        {
            [Parameter("bytes32", "delegationControlId", 1)]
            public byte[] DelegationControlId { get; set; }
        }
    }

}


