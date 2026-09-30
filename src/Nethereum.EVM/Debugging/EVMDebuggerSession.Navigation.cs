using System;
using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.EVM.Debugging
{
    public partial class EVMDebuggerSession
    {
        public List<ProgramTrace> Trace { get; private set; }
        public int CurrentStep { get; private set; }
        public bool CanStepForward => Trace != null && CurrentStep < Trace.Count - 1;
        public bool CanStepBack => Trace != null && CurrentStep > 0;
        public int TotalSteps => Trace?.Count ?? 0;

        public void StepForward()
        {
            if (CanStepForward) CurrentStep++;
        }

        public void StepBack()
        {
            if (CanStepBack) CurrentStep--;
        }

        public void GoToStep(int step)
        {
            if (Trace == null || Trace.Count == 0) return;
            CurrentStep = Math.Max(0, Math.Min(step, Trace.Count - 1));
        }

        public void GoToStart()
        {
            CurrentStep = 0;
        }

        public void GoToEnd()
        {
            if (Trace != null && Trace.Count > 0)
                CurrentStep = Trace.Count - 1;
        }

        public ProgramTrace CurrentTrace => Trace != null && CurrentStep >= 0 && CurrentStep < Trace.Count
            ? Trace[CurrentStep]
            : null;

        public ProgramInstruction CurrentInstruction => CurrentTrace?.Instruction;

        public List<string> CurrentStack => CurrentTrace?.Stack;

        public string CurrentMemory => CurrentTrace?.Memory;

        public Dictionary<string, string> CurrentStorage => CurrentTrace?.Storage;

        public int CurrentDepth => CurrentTrace?.Depth ?? 0;

        public BigInteger CurrentGasCost => CurrentTrace?.GasCost ?? 0;

        public string CurrentCodeAddress => CurrentTrace?.CodeAddress;

        public string CurrentProgramAddress => CurrentTrace?.ProgramAddress;

        private bool IsWithinTrace(int stepIndex) => Trace != null && stepIndex >= 0 && stepIndex < Trace.Count;
    }
}
