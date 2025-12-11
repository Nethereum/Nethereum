using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Nethereum.Consensus.LightClient.Tests.Live
{
    public class LightClientRecordingWriter
    {
        private readonly string _outputDirectory;

        public LightClientRecordingWriter(string outputDirectory)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new ArgumentNullException(nameof(outputDirectory));
            }

            _outputDirectory = outputDirectory;
        }

        public async Task<string> WriteAsync(LightClientRecording recording, CancellationToken cancellationToken = default)
        {
            if (recording == null) throw new ArgumentNullException(nameof(recording));

            Directory.CreateDirectory(_outputDirectory);
            var fileName = $"{recording.CapturedAt:yyyyMMddHHmmss}-slot-{recording.FinalizedSlot}.json";
            var path = Path.Combine(_outputDirectory, fileName);

            var payload = JsonConvert.SerializeObject(recording, Formatting.Indented);
            await File.WriteAllTextAsync(path, payload, cancellationToken).ConfigureAwait(false);

            return path;
        }
    }
}
