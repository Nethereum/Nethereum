#if !EVM_ZISK
using System.Threading;

namespace Nethereum.EVM
{
    internal sealed class ProgramCodeAnalysisEntry
    {
        private const long ArrayHeaderBytes = 24;

        private readonly object _gate = new object();
        private byte[] _pushDataBitmap;
        private ProgramCodeAnalysis _standard;
        private ProgramCodeAnalysis _eip8024;
        private long _estimatedBytes;

        public long EstimatedBytes => Interlocked.Read(ref _estimatedBytes);

        public byte[] GetOrBuildPushDataBitmap(byte[] byteCode, out bool built)
        {
            lock (_gate)
            {
                built = _pushDataBitmap == null;
                if (built)
                {
                    _pushDataBitmap = BytecodeDecoder.BuildPushDataBitmap(byteCode);
                    Interlocked.Add(ref _estimatedBytes, ArrayHeaderBytes + _pushDataBitmap.Length);
                }
                return _pushDataBitmap;
            }
        }

        public ProgramCodeAnalysis GetOrBuildInstructions(byte[] byteCode, bool eip8024Enabled, out bool built)
        {
            lock (_gate)
            {
                var existing = eip8024Enabled ? _eip8024 : _standard;
                built = existing == null;
                if (!built) return existing;

                var analysis = ProgramCodeAnalysis.Build(byteCode, eip8024Enabled);
                if (eip8024Enabled) _eip8024 = analysis; else _standard = analysis;
                Interlocked.Add(ref _estimatedBytes, analysis.EstimatedBytes);
                return analysis;
            }
        }
    }
}
#endif
