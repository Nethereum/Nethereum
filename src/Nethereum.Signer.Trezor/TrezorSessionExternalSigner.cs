using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using hw.trezor.messages;
using Nethereum.Signer.Trezor.Abstractions;
using Nethereum.Signer.Trezor.Internal;
using Trezor.Net;
using Trezor.Net.Manager;

namespace Nethereum.Signer.Trezor
{
    /// <summary>
    /// External signer variant that surfaces InitializeAsync to coordinate end-to-end workflows.
    /// </summary>
    public class TrezorSessionExternalSigner : TrezorExternalSigner, ITrezorSession
    {
        private readonly TrezorManagerBase<MessageType> _manager;

        public TrezorSessionExternalSigner(TrezorManagerBase<MessageType> manager, uint index, string? knownAddress = null, ILogger<TrezorExternalSigner>? logger = null)
            : base(manager, index, knownAddress, logger)
        {
            _manager = manager;
        }

        public TrezorSessionExternalSigner(TrezorManagerBase<MessageType> manager, string customPath, uint index, string? knownAddress = null, ILogger<TrezorExternalSigner>? logger = null)
            : base(manager, customPath, index, knownAddress, logger)
        {
            _manager = manager;
        }

        public Task InitializeAsync()
        {
            return _manager.InitializeAsync();
        }
    }
}
