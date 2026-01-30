using System;
using System.IO;
using System.Security.Cryptography;

namespace Nethereum.EVM.Precompiles.Kzg
{
    public class CkzgOperations : IKzgOperations, IDisposable
    {
        private static readonly object InitLock = new object();
        private static bool _initialized;
        private static IntPtr _trustedSetup;
        private bool _disposed;

        public const byte VERSIONED_HASH_VERSION_KZG = 0x01;

        public bool IsInitialized => _initialized;

        public CkzgOperations()
        {
        }

        public CkzgOperations(string trustedSetupPath)
        {
            Initialize(trustedSetupPath);
        }

        public static void Initialize(string trustedSetupPath)
        {
            if (_initialized) return;
            lock (InitLock)
            {
                if (_initialized) return;

                if (!File.Exists(trustedSetupPath))
                    throw new FileNotFoundException($"KZG trusted setup file not found: {trustedSetupPath}");

                try
                {
                    _trustedSetup = Ckzg.Ckzg.LoadTrustedSetup(trustedSetupPath);
                    _initialized = true;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to load KZG trusted setup: {ex.Message}", ex);
                }
            }
        }

        public static void InitializeFromEmbeddedSetup()
        {
            if (_initialized) return;
            lock (InitLock)
            {
                if (_initialized) return;

                var tempPath = Path.Combine(Path.GetTempPath(), "kzg_trusted_setup.txt");

                try
                {
                    var assembly = typeof(CkzgOperations).Assembly;
                    var resourceName = "Nethereum.EVM.Precompiles.Kzg.trusted_setup.txt";

                    using (var stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        if (stream == null)
                            throw new InvalidOperationException($"Embedded trusted setup not found: {resourceName}");

                        using (var fileStream = File.Create(tempPath))
                        {
                            stream.CopyTo(fileStream);
                        }
                    }

                    _trustedSetup = Ckzg.Ckzg.LoadTrustedSetup(tempPath);
                    _initialized = true;
                }
                finally
                {
                    if (File.Exists(tempPath))
                    {
                        try { File.Delete(tempPath); } catch { }
                    }
                }
            }
        }

        public bool VerifyKzgProof(byte[] commitment, byte[] z, byte[] y, byte[] proof)
        {
            EnsureInitialized();

            if (commitment == null || commitment.Length != 48)
                throw new ArgumentException("Commitment must be 48 bytes");
            if (z == null || z.Length != 32)
                throw new ArgumentException("z must be 32 bytes");
            if (y == null || y.Length != 32)
                throw new ArgumentException("y must be 32 bytes");
            if (proof == null || proof.Length != 48)
                throw new ArgumentException("Proof must be 48 bytes");

            try
            {
                return Ckzg.Ckzg.VerifyKzgProof(commitment, z, y, proof, _trustedSetup);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"KZG proof verification failed: {ex.Message}", ex);
            }
        }

        public byte[] ComputeVersionedHash(byte[] commitment)
        {
            if (commitment == null || commitment.Length != 48)
                throw new ArgumentException("Commitment must be 48 bytes");

            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(commitment);
                hash[0] = VERSIONED_HASH_VERSION_KZG;
                return hash;
            }
        }

        private void EnsureInitialized()
        {
            if (!_initialized)
                throw new InvalidOperationException("KZG trusted setup not initialized. Call Initialize() or InitializeFromEmbeddedSetup() first.");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing && _initialized && _trustedSetup != IntPtr.Zero)
            {
                try
                {
                    Ckzg.Ckzg.FreeTrustedSetup(_trustedSetup);
                }
                catch { }
                _trustedSetup = IntPtr.Zero;
                _initialized = false;
            }

            _disposed = true;
        }

        ~CkzgOperations()
        {
            Dispose(false);
        }
    }
}
