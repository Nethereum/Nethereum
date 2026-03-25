using System;
using System.Runtime.InteropServices;

namespace Nethereum.CircomWitnessCalc
{
    public static class WitnessCalculator
    {
        public static byte[] CalculateWitness(byte[] graphData, string inputsJson)
        {
            if (graphData == null || graphData.Length == 0)
                throw new ArgumentException("Circuit graph data must not be empty.", nameof(graphData));
            if (string.IsNullOrEmpty(inputsJson))
                throw new ArgumentException("Inputs JSON must not be empty.", nameof(inputsJson));

            var status = new CircomWitnessCalcBindings.GwStatus();
            IntPtr wtnsData;
            UIntPtr wtnsLen;

            var result = CircomWitnessCalcBindings.gw_calc_witness(
                inputsJson,
                graphData, (UIntPtr)graphData.Length,
                out wtnsData, out wtnsLen,
                ref status);

            if (result != 0)
            {
                var errorMsg = "Witness calculation failed";
                if (status.ErrorMsg != IntPtr.Zero)
                {
                    errorMsg = Marshal.PtrToStringAnsi(status.ErrorMsg) ?? errorMsg;
                    CircomWitnessCalcBindings.gw_free(status.ErrorMsg);
                }
                throw new CircomWitnessCalcException(errorMsg);
            }

            if (wtnsData == IntPtr.Zero || (ulong)wtnsLen == 0)
                throw new CircomWitnessCalcException("Witness calculation returned empty result");

            try
            {
                var length = checked((int)(ulong)wtnsLen);
                var witnessBytes = new byte[length];
                Marshal.Copy(wtnsData, witnessBytes, 0, length);
                return witnessBytes;
            }
            finally
            {
                CircomWitnessCalcBindings.gw_free(wtnsData);
            }
        }
    }
}
