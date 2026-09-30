using System.Collections.Generic;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.SmartSession.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579.Modules.SmartSession;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Modules.SmartSession
{
    public class SmartSessionConfigGetInitDataTests
    {
        private class SessionArrayDecodeDto
        {
            [Parameter("tuple[]", "sessions", 1)]
            public virtual List<Session> Sessions { get; set; }
        }

        [Fact]
        public void GetInitData_IsInstallModeByteThenAbiEncodedSessionArray()
        {
            var sessionValidator = "0x1234567890123456789012345678901234567890";
            var salt = new byte[32];
            salt[31] = 1;

            var config = new SmartSessionConfig()
                .WithSessionValidator(sessionValidator)
                .WithSalt(salt)
                .WithSudoPolicy("0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

            var initData = config.GetInitData();

            Assert.Equal((byte)SmartSessionMode.UnsafeEnable, initData[0]);

            var sessionsEncoded = initData[1..];
            var decoded = (SessionArrayDecodeDto)new ParameterDecoder()
                .DecodeAttributes(sessionsEncoded, typeof(SessionArrayDecodeDto));

            Assert.Single(decoded.Sessions);
            var session = decoded.Sessions[0];
            Assert.Equal(sessionValidator, session.SessionValidator, ignoreCase: true);
            Assert.Equal(salt, session.Salt);
            Assert.Single(session.UserOpPolicies);
            Assert.Equal("0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", session.UserOpPolicies[0].Policy, ignoreCase: true);
        }

        [Fact]
        public void GetInitData_WithExplicitInstallMode_EncodesThatModeByte()
        {
            var config = new SmartSessionConfig()
                .WithSessionValidator("0x1234567890123456789012345678901234567890")
                .WithSalt(new byte[32])
                .WithInstallMode(SmartSessionMode.Enable);

            var initData = config.GetInitData();

            Assert.Equal((byte)SmartSessionMode.Enable, initData[0]);
        }
    }
}
