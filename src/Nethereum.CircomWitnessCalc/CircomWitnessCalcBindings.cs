using System;
using System.Runtime.InteropServices;

namespace Nethereum.CircomWitnessCalc
{
    public static class CircomWitnessCalcBindings
    {
        public const string LibName = "circom_witnesscalc";

        [StructLayout(LayoutKind.Sequential)]
        public struct GwStatus
        {
            public int Code;
            public IntPtr ErrorMsg;
        }

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int gw_calc_witness(
            string inputs,
            byte[] graph_data, UIntPtr graph_data_len,
            out IntPtr wtns_data, out UIntPtr wtns_len,
            ref GwStatus status);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void gw_free(IntPtr ptr);
    }
}
