using System;
using System.IO;

namespace Nethereum.AppChain.Sequencer.ProducerAuthority
{
    public class FileProducerAuthority : IProducerAuthority
    {
        private readonly string _path;

        public FileProducerAuthority(string path)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
        }

        public string? CurrentProducer()
        {
            try
            {
                var text = File.ReadAllText(_path).Trim();
                return text.Length == 0 ? null : text;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
