using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.TransactionManagers;
using Xunit;

namespace Nethereum.RPC.UnitTests.TransactionManagers
{
    public class EtherTransferGasResolutionTests
    {
        private const long TwentyOneThousand = 21000;
        private const long CostOfCreatingTheRecipient = 204600;
        private const string Recipient = "0x1234567890123456789012345678901234567890";

        [Fact]
        public async Task TransferEther_WhenTheNodeCanEstimate_UsesTheEstimateNotTheDefault()
        {
            var manager = new RecordingTransactionManager(estimate: CostOfCreatingTheRecipient);

            await new EtherTransferService(manager).TransferEtherAsync(Recipient, 1m);

            Assert.Equal(new BigInteger(CostOfCreatingTheRecipient), manager.SentGas);
        }

        /// <summary>
        /// EIP-2780 prices a transfer to an account that already exists at 21,000, and
        /// EIP-8037 charges STATE_BYTES_PER_NEW_ACCOUNT x CPSB on top when the recipient has
        /// to be created. No fixed default covers both, so a default that survives a
        /// successful estimate is the defect this pins.
        /// </summary>
        [Fact]
        public async Task TransferEther_WhenTheNodeCannotEstimate_FallsBackToTheDefaultRatherThanThrowing()
        {
            var manager = new RecordingTransactionManager(estimate: null);

            await new EtherTransferService(manager).TransferEtherAsync(Recipient, 1m);

            Assert.Equal(new BigInteger(TwentyOneThousand), manager.SentGas);
        }

        [Fact]
        public async Task TransferEther_WhenTheCallerSuppliesGas_TheNodeIsNotAsked()
        {
            var manager = new RecordingTransactionManager(estimate: CostOfCreatingTheRecipient);

            await new EtherTransferService(manager).TransferEtherAsync(Recipient, 1m, gas: 50000);

            Assert.Equal(new BigInteger(50000), manager.SentGas);
            Assert.False(manager.EstimateWasCalled);
        }

        private sealed class RecordingTransactionManager : TransactionManagerBase
        {
            private readonly BigInteger? _estimate;

            public RecordingTransactionManager(BigInteger? estimate)
            {
                _estimate = estimate;
                DefaultGas = TwentyOneThousand;
            }

            public BigInteger SentGas { get; private set; }
            public bool EstimateWasCalled { get; private set; }

            public override BigInteger DefaultGas { get; set; }

            public override Task<HexBigInteger> EstimateGasAsync(CallInput callInput)
            {
                EstimateWasCalled = true;
                if (_estimate == null) throw new RpcClientUnknownException("node unavailable");
                return Task.FromResult(new HexBigInteger(_estimate.Value));
            }

            public override Task<string> SendTransactionAsync(TransactionInput transactionInput)
            {
                SentGas = transactionInput.Gas.Value;
                return Task.FromResult("0x0");
            }

            public override Task<string> SignTransactionAsync(TransactionInput transaction) =>
                throw new NotSupportedException();

            public override Task<Authorisation> SignAuthorisationAsync(Authorisation authorisation) =>
                throw new NotSupportedException();
        }
    }
}
