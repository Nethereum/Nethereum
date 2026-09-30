using System;
using System.IO;
using Nethereum.Documentation;
using Nethereum.Signer;

namespace Nethereum.DevP2P.NodeDb
{
    public static class NodeKeyStore
    {
        [NethereumDocExample(DocSection.DevP2P, "devp2p", "NodeKeyStore.LoadOrCreate — persistent node identity key")]
        public static EthECKey LoadOrCreate(string path, Action<string>? log = null)
        {
            log ??= _ => { };
            return LoadExistingKey(path, log) ?? GenerateAndPersistKey(path, log);
        }

        private static EthECKey? LoadExistingKey(string path, Action<string> log)
        {
            if (!File.Exists(path)) return null;

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Node key file {path} exists but could not be read ({ex.GetType().Name}: {ex.Message}). " +
                    "Refusing to overwrite it — remove or fix the file, or point NodeKeyFile elsewhere, then retry.",
                    ex);
            }
            if (bytes.Length != 32)
                throw new InvalidOperationException(
                    $"Node key file {path} is {bytes.Length} bytes, expected exactly 32 (this is NOT geth's " +
                    "nodekey format, which is 64 hex characters of text). Refusing to overwrite an existing " +
                    "file of unexpected shape — remove it or point NodeKeyFile elsewhere, then retry.");
            try
            {
                var key = new EthECKey(bytes, true);
                log($"Node key loaded from {path}.");
                return key;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Node key file {path} is 32 bytes but not a valid private key ({ex.GetType().Name}: {ex.Message}). " +
                    "Refusing to overwrite it — remove or fix the file, or point NodeKeyFile elsewhere, then retry.",
                    ex);
            }
        }

        private static EthECKey GenerateAndPersistKey(string path, Action<string> log)
        {
            var newKey = EthECKey.GenerateKey();
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                AtomicFile.WriteAllBytes(path, newKey.GetPrivateKeyAsBytes(), UnixFileMode.UserRead | UnixFileMode.UserWrite);
                log($"Node key generated and saved to {path}.");
            }
            catch (Exception ex)
            {
                log($"Node key save to {path} failed ({ex.GetType().Name}: {ex.Message}); using an ephemeral key for this run.");
            }
            return newKey;
        }
    }
}
