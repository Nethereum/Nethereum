using System;
using System.IO;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    internal sealed class ErrorCapture : IDisposable
    {
        private readonly TextWriter _original = Console.Error;
        private readonly StringWriter _buffer = new StringWriter();
        private readonly TextWriter _synchronized;

        internal ErrorCapture()
        {
            _synchronized = TextWriter.Synchronized(_buffer);
            Console.SetError(_synchronized);
        }

        internal string Text { get { lock (_synchronized) return _buffer.ToString(); } }

        public void Dispose() => Console.SetError(_original);
    }
}
