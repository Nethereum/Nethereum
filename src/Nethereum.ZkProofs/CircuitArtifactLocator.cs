using System;
using System.IO;
using System.Reflection;

namespace Nethereum.ZkProofs
{
    public class CircuitArtifactLocator
    {
        private readonly string _baseDir;

        public CircuitArtifactLocator(string? baseDir = null)
        {
            if (baseDir != null)
            {
                if (!Directory.Exists(baseDir))
                    throw new DirectoryNotFoundException($"Circuit artifacts directory not found: {baseDir}");
                _baseDir = baseDir;
            }
            else
            {
                var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
                _baseDir = Path.Combine(assemblyDir, "circuits");
            }
        }

        public string GetWasmPath(string circuitName)
        {
            return Path.Combine(_baseDir, circuitName, $"{circuitName}.wasm");
        }

        public string GetZkeyPath(string circuitName)
        {
            return Path.Combine(_baseDir, circuitName, $"{circuitName}.zkey");
        }

        public string GetVkPath(string circuitName)
        {
            return Path.Combine(_baseDir, circuitName, $"{circuitName}_vk.json");
        }

        public bool HasArtifacts(string circuitName)
        {
            return File.Exists(GetWasmPath(circuitName)) && File.Exists(GetZkeyPath(circuitName));
        }

        public string BaseDirectory => _baseDir;
    }
}
