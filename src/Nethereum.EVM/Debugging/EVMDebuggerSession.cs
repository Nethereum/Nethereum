using Nethereum.ABI.ABIRepository;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.EVM.Debugging
{
    public partial class EVMDebuggerSession
    {
        public EVMDebuggerSession(IABIInfoStorage abiStorage)
        {
            _abiStorage = abiStorage ?? throw new ArgumentNullException(nameof(abiStorage));
        }

        public void LoadFromProgram(Program executedProgram, BigInteger chainId)
        {
            if (executedProgram == null) throw new ArgumentNullException(nameof(executedProgram));

            StartSessionAtFirstStep(executedProgram.Trace, chainId);
            ResetDebugInfoCaches();
            LoadDebugInfoForTracedContracts();
        }

        public async Task LoadFromProgramAsync(Program executedProgram, BigInteger chainId)
        {
            if (executedProgram == null) throw new ArgumentNullException(nameof(executedProgram));

            StartSessionAtFirstStep(executedProgram.Trace, chainId);
            ResetDebugInfoCaches();
            await LoadDebugInfoForTracedContractsAsync();
        }

        public void LoadFromTrace(List<ProgramTrace> trace, BigInteger chainId)
        {
            if (trace == null) throw new ArgumentNullException(nameof(trace));

            StartSessionAtFirstStep(trace, chainId);
            ResetDebugInfoCachesExceptFunctionMaps();
            LoadDebugInfoForTracedContracts();
        }

        public async Task LoadFromTraceAsync(List<ProgramTrace> trace, BigInteger chainId)
        {
            if (trace == null) throw new ArgumentNullException(nameof(trace));

            StartSessionAtFirstStep(trace, chainId);
            ResetDebugInfoCaches();
            await LoadDebugInfoForTracedContractsAsync();
        }

        private void StartSessionAtFirstStep(List<ProgramTrace> trace, BigInteger chainId)
        {
            Trace = trace;
            CurrentStep = 0;
            _chainId = chainId;
        }
    }
}
