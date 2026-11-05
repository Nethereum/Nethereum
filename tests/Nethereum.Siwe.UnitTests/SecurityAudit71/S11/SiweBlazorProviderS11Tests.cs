using System;
using System.Threading.Tasks;
using Nethereum.Blazor.Siwe;
using Nethereum.Signer;
using Nethereum.Siwe.Authentication;
using Nethereum.Siwe.Core;
using Nethereum.Siwe.Model;
using Nethereum.UI;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Siwe.UnitTests.SecurityAudit71.S11
{
    public class SiweBlazorProviderS11Tests
    {
        private const string VictimKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
        private const string AttackerKey = "0x8da4ef21b864d2cc526dbdb2a120bd2874c36c9d0a1fb7f8c63d7f7a8b41de8f";
        private static readonly string VictimAddress = "0x12890d2cce102216644c59daE5baed380d84830c".ConvertToEthereumChecksumAddress();

        public class LoginSiweMessage : SiweMessage
        {
            public LoginSiweMessage()
            {
                Domain = "login.xyz";
                Uri = "https://login.xyz";
                ChainId = "1";
            }
        }

        private sealed class MemoryTokenService : IAccessTokenService
        {
            public string Token { get; set; }
            public Task<string> GetAccessTokenAsync() => Task.FromResult(Token);
            public Task SetAccessTokenAsync(string tokenValue) { Token = tokenValue; return Task.CompletedTask; }
            public Task RemoveAccessTokenAsync() { Token = null; return Task.CompletedTask; }
        }

        private sealed class UserService : IUserService<User>
        {
            public Task<User> GetUserAsync(string address) =>
                Task.FromResult(new User { EthereumAddress = address, UserName = address });
        }

        private sealed class SigningHost : IEthereumHostProvider
        {
            private readonly string _address;
            private readonly string _signingKey;

            public SigningHost(string address, string signingKey)
            {
                _address = address;
                _signingKey = signingKey;
            }

            public string Name => "test";
            public bool Available => true;
            public string SelectedAccount => _address;
            public long SelectedNetworkChainId => 1;
            public bool Enabled => true;
            public bool MultipleWalletsProvider => false;
            public bool MultipleWalletSelected => false;
            public event Func<string, Task> SelectedAccountChanged { add { } remove { } }
            public event Func<long, Task> NetworkChanged { add { } remove { } }
            public event Func<bool, Task> AvailabilityChanged { add { } remove { } }
            public event Func<bool, Task> EnabledChanged { add { } remove { } }
            public Task<bool> CheckProviderAvailabilityAsync() => Task.FromResult(true);
            public Task<Nethereum.Web3.IWeb3> GetWeb3Async() => throw new NotSupportedException();
            public Task<string> EnableProviderAsync() => Task.FromResult(_address);
            public Task<string> GetProviderSelectedAccountAsync() => Task.FromResult(_address);
            public Task<string> SignMessageAsync(string message) =>
                Task.FromResult(new EthereumMessageSigner().EncodeUTF8AndSign(message, new EthECKey(_signingKey)));
        }

        private sealed class Fixture
        {
            public InMemorySessionNonceStorage Storage { get; } = new InMemorySessionNonceStorage();
            public MemoryTokenService Tokens { get; } = new MemoryTokenService();
            public SiweAuthenticationServerStateProvider<User, LoginSiweMessage> Provider { get; }

            public Fixture(string signingKey)
            {
                var hosts = new SelectedEthereumHostProviderService();
                hosts.SetSelectedEthereumHostProvider(new SigningHost(VictimAddress, signingKey)).GetAwaiter().GetResult();
                var authenticator = new NethereumSiweAuthenticatorService(hosts, Storage);
                Provider = new SiweAuthenticationServerStateProvider<User, LoginSiweMessage>(
                    authenticator, Tokens, hosts, new UserService(), Storage);
            }

            public async Task<bool> IsAuthenticated()
            {
                var state = await Provider.GetAuthenticationStateAsync();
                return state.User.IsInRole("SiweAuthenticated");
            }
        }

        [Fact]
        public async Task Given_AChallengeIssuedForAnAddressWhoseSignatureFails_When_TheChallengeTextIsPresentedAsTheToken_Then_TheProviderDoesNotAuthenticate()
        {
            var fixture = new Fixture(AttackerKey);

            await Assert.ThrowsAnyAsync<Exception>(() => fixture.Provider.AuthenticateAsync(VictimAddress));
            Assert.Null(fixture.Tokens.Token);

            var challenge = new SiweMessage { Domain = "login.xyz", Address = VictimAddress, Uri = "https://login.xyz", ChainId = "1" };
            challenge.SetExpirationTime(DateTime.UtcNow.AddMinutes(10));
            var challengeText = new SiweMessageService(fixture.Storage).BuildMessageToSign(challenge);
            fixture.Tokens.Token = challengeText;

            Assert.False(await fixture.IsAuthenticated());

            fixture.Tokens.Token = "0x" + new string('1', 130) + "\n" + challengeText;
            Assert.False(await fixture.IsAuthenticated());
        }

        [Fact]
        public async Task Given_ASuccessfulSignature_When_TheProviderIsAsked_Then_TheUserIsAuthenticatedUntilLogOut()
        {
            var fixture = new Fixture(VictimKey);

            await fixture.Provider.AuthenticateAsync(VictimAddress);

            Assert.True(await fixture.IsAuthenticated());

            await fixture.Provider.LogOutUserAsync();

            Assert.False(await fixture.IsAuthenticated());
        }

        [Fact]
        public async Task Given_ATokenWithoutASignature_When_TheProviderIsAsked_Then_TheUserIsNotAuthenticated()
        {
            var fixture = new Fixture(VictimKey);
            await fixture.Provider.AuthenticateAsync(VictimAddress);
            var token = fixture.Tokens.Token;

            fixture.Tokens.Token = token.Substring(token.IndexOf('\n') + 1);

            Assert.False(await fixture.IsAuthenticated());
        }

        [Fact]
        public async Task Given_ATokenSignedByAnotherKey_When_TheProviderIsAsked_Then_TheUserIsNotAuthenticated()
        {
            var fixture = new Fixture(VictimKey);
            await fixture.Provider.AuthenticateAsync(VictimAddress);
            var token = fixture.Tokens.Token;
            var text = token.Substring(token.IndexOf('\n') + 1);

            fixture.Tokens.Token = new EthereumMessageSigner().EncodeUTF8AndSign(text, new EthECKey(AttackerKey)) + "\n" + text;

            Assert.False(await fixture.IsAuthenticated());
        }
    }
}
