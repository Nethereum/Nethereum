using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.SmartSession.ContractDefinition;
using Nethereum.ABI;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.AccountAbstraction.ERC7579.Modules.SmartSession
{
    public class SmartSessionConfig : ModuleConfigBase
    {
        public override BigInteger ModuleTypeId => ERC7579ModuleTypes.TYPE_VALIDATOR;

        public string SessionValidator { get; set; }
        public byte[] SessionValidatorInitData { get; set; } = Array.Empty<byte>();
        public byte[] Salt { get; set; }
        public List<PolicyData> UserOpPolicies { get; private set; } = new List<PolicyData>();
        public ERC7739Data Erc7739Policies { get; set; } = new ERC7739Data
        {
            AllowedERC7739Content = new List<ERC7739Context>(),
            Erc1271Policies = new List<PolicyData>()
        };
        public List<ActionData> Actions { get; private set; } = new List<ActionData>();
        public bool PermitERC4337Paymaster { get; set; } = false;

        public SmartSessionConfig WithSessionValidator(string validatorAddress)
        {
            SessionValidator = validatorAddress;
            return this;
        }

        public SmartSessionConfig WithSessionValidatorInitData(byte[] initData)
        {
            SessionValidatorInitData = initData;
            return this;
        }

        public SmartSessionConfig WithSessionValidatorInitData(string ownerAddress)
        {
            SessionValidatorInitData = ownerAddress.HexToByteArray();
            return this;
        }

        public SmartSessionConfig WithSalt(byte[] salt)
        {
            Salt = salt;
            return this;
        }

        public SmartSessionConfig WithSalt(BigInteger salt)
        {
            Salt = new IntType("uint256").Encode(salt);
            return this;
        }

        public SmartSessionConfig WithUserOpPolicy(string policyAddress, byte[] initData = null)
        {
            UserOpPolicies.Add(new PolicyData
            {
                Policy = policyAddress,
                InitData = initData ?? Array.Empty<byte>()
            });
            return this;
        }

        public SmartSessionConfig WithSudoPolicy(string sudoPolicyAddress)
        {
            return WithUserOpPolicy(sudoPolicyAddress, Array.Empty<byte>());
        }

        public SmartSessionConfig WithAction(ActionData action)
        {
            Actions.Add(action);
            return this;
        }

        public SmartSessionConfig WithAction(string targetAddress, byte[] functionSelector, params PolicyData[] policies)
        {
            Actions.Add(new ActionData
            {
                ActionTarget = targetAddress,
                ActionTargetSelector = functionSelector,
                ActionPolicies = new List<PolicyData>(policies)
            });
            return this;
        }

        public SmartSessionConfig WithERC20TransferAction(
            string tokenAddress,
            string spendingLimitPolicyAddress,
            byte[] spendingLimitInitData)
        {
            var transferSelector = "0xa9059cbb".HexToByteArray();
            Actions.Add(new ActionData
            {
                ActionTarget = tokenAddress,
                ActionTargetSelector = transferSelector,
                ActionPolicies = new List<PolicyData>
                {
                    new PolicyData
                    {
                        Policy = spendingLimitPolicyAddress,
                        InitData = spendingLimitInitData
                    }
                }
            });
            return this;
        }

        public SmartSessionConfig WithERC1271Policy(string policyAddress, byte[] initData = null)
        {
            Erc7739Policies.Erc1271Policies.Add(new PolicyData
            {
                Policy = policyAddress,
                InitData = initData ?? Array.Empty<byte>()
            });
            return this;
        }

        public SmartSessionConfig WithERC7739Context(byte[] appDomainSeparator, List<string> contentNames)
        {
            Erc7739Policies.AllowedERC7739Content.Add(new ERC7739Context
            {
                AppDomainSeparator = appDomainSeparator,
                ContentNames = contentNames ?? new List<string>()
            });
            return this;
        }

        public SmartSessionConfig WithPaymasterPermission(bool permit = true)
        {
            PermitERC4337Paymaster = permit;
            return this;
        }

        public Session ToSession()
        {
            Validate();

            return new Session
            {
                SessionValidator = SessionValidator,
                SessionValidatorInitData = SessionValidatorInitData,
                Salt = Salt,
                UserOpPolicies = UserOpPolicies,
                Erc7739Policies = Erc7739Policies,
                Actions = Actions,
                PermitERC4337Paymaster = PermitERC4337Paymaster
            };
        }

        public override byte[] GetInitData()
        {
            var session = ToSession();
            var abiEncoder = new ABIEncode();
            return abiEncoder.GetABIEncoded(
                new ABIValue("address", session.SessionValidator),
                new ABIValue("bytes", session.SessionValidatorInitData),
                new ABIValue("bytes32", session.Salt),
                new ABIValue("bool", session.PermitERC4337Paymaster)
            );
        }

        private void Validate()
        {
            if (string.IsNullOrEmpty(SessionValidator))
                throw new InvalidOperationException("SessionValidator address is required");

            if (Salt == null || Salt.Length != 32)
                throw new InvalidOperationException("Salt must be 32 bytes");
        }

        public static SmartSessionConfig Create(
            string moduleAddress,
            string sessionValidatorAddress,
            byte[] salt)
        {
            return new SmartSessionConfig
            {
                ModuleAddress = moduleAddress,
                SessionValidator = sessionValidatorAddress,
                Salt = salt
            };
        }

        public static SmartSessionConfig CreateWithOwner(
            string moduleAddress,
            string sessionValidatorAddress,
            string ownerAddress,
            byte[] salt)
        {
            return new SmartSessionConfig
            {
                ModuleAddress = moduleAddress,
                SessionValidator = sessionValidatorAddress,
                SessionValidatorInitData = ownerAddress.HexToByteArray(),
                Salt = salt
            };
        }
    }
}
