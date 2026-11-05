using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public partial class HaltReasonParityTests
    {
        private static Program Execute(EVMSimulator simulator, Program program)
            => simulator.ExecuteWithCallStack(program, traceEnabled: false);

        [Fact]
        public void Given_every_halt_class_When_run_on_the_sync_engine_Then_no_exception_is_raised_at_all()
        {
            var raised = new List<Exception>();
            var testThread = Environment.CurrentManagedThreadId;
            EventHandler<FirstChanceExceptionEventArgs> observe = (_, e) =>
            {
                if (Environment.CurrentManagedThreadId == testThread) raised.Add(e.Exception);
            };
            AppDomain.CurrentDomain.FirstChanceException += observe;
            try
            {
                RunCode(new byte[] { UndefinedByte });
                RunCode(new byte[] { Push1, 0x00, Tload }, fork: HardforkName.Shanghai);
                RunCode(Pushes(1025));
                RunCode(new byte[] { Pop });
                RunCode(new byte[] { Sload });
                RunCode(new byte[] { Push1, 0x01, Jump });
                RunCode(new byte[] { Push1, 0x01, Push1, 0x00, Sstore }, isStatic: true);
                RunCode(new byte[] { Push1, 0x01, Push1, 0x00, Sstore }, gas: 2306);
                RunCode(new byte[] { Push1, 0x01, Push1, 0x01 }, gas: 5);
                RunCode(new byte[] { Push1, 0x01, Push1, 0x00, Push1, 0x00, ReturnDataCopy });
            }
            finally
            {
                AppDomain.CurrentDomain.FirstChanceException -= observe;
            }
            Assert.Empty(raised);
        }

        private sealed class ThrowingStateReader : IStateReader
        {
            private readonly Exception _exception;
            public ThrowingStateReader(Exception exception) => _exception = exception;

            public EvmUInt256 GetBalance(byte[] address) => EvmUInt256.Zero;
            public EvmUInt256 GetBalance(string address) => EvmUInt256.Zero;
            public byte[] GetCode(byte[] address) => new byte[0];
            public byte[] GetCode(string address) => new byte[0];
            public byte[] GetStorageAt(byte[] address, EvmUInt256 position) => throw _exception;
            public byte[] GetStorageAt(string address, EvmUInt256 position) => throw _exception;
            public EvmUInt256 GetTransactionCount(byte[] address) => EvmUInt256.Zero;
            public EvmUInt256 GetTransactionCount(string address) => EvmUInt256.Zero;
            public bool AccountExists(string address) => true;
            public byte[] GetBlockHash(long blockNumber) => new byte[32];
        }
    }
}
