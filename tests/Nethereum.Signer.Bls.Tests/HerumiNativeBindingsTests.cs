using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using mcl;
using Nethereum.Signer.Bls.Herumi;
using Xunit;

#nullable enable

namespace Nethereum.Signer.Bls.Tests
{
    public class HerumiNativeBindingsTests
    {
        [SkippableFact]
        public async Task FastAggregate_VerifiesRealSignature()
        {
            SkipIfNativeUnavailable();

            var secrets = new[]
            {
                CreateSecretKey("alpha"),
                CreateSecretKey("beta"),
                CreateSecretKey("gamma")
            };

            var message = CreateMessage("sync-committee-slot-0");
            var aggregateSignature = AggregateSignatures(secrets.Select(sk => sk.Sign(message)).ToArray());

            var publicKeys = secrets.Select(sk => sk.GetPublicKey().Serialize()).ToArray();
            var domain = CreateDomain("lc-fast-aggregate");

            var native = new NativeBls(new HerumiNativeBindings());
            await native.InitializeAsync();

            Assert.True(native.VerifyAggregate(
                aggregateSignature.Serialize(),
                publicKeys,
                new[] { message },
                domain));

            var tampered = (byte[])message.Clone();
            tampered[0] ^= 0xFF;

            Assert.False(native.VerifyAggregate(
                aggregateSignature.Serialize(),
                publicKeys,
                new[] { tampered },
                domain));
        }

        [SkippableFact]
        public async Task AggregateVerify_MultiMessage_RoundTrip()
        {
            SkipIfNativeUnavailable();

            var secrets = new[]
            {
                CreateSecretKey("delta"),
                CreateSecretKey("epsilon")
            };

            var messages = new[]
            {
                CreateMessage("attestation-0"),
                CreateMessage("attestation-1")
            };

            var signatures = secrets
                .Select((sk, index) => sk.Sign(messages[index]))
                .ToArray();

            var aggregateSignature = AggregateSignatures(signatures);
            var publicKeys = secrets.Select(sk => sk.GetPublicKey().Serialize()).ToArray();
            var domain = CreateDomain("lc-aggregate");

            var native = new NativeBls(new HerumiNativeBindings());
            await native.InitializeAsync();

            Assert.True(native.VerifyAggregate(
                aggregateSignature.Serialize(),
                publicKeys,
                messages,
                domain));
        }

        private static void SkipIfNativeUnavailable()
        {
            Skip.IfNot(NativeLibraryLocator.TryEnsure(out var reason), reason);
        }

        private static BLS.SecretKey CreateSecretKey(string label)
        {
            var secretKey = new BLS.SecretKey();
            secretKey.SetHashOf(label);
            return secretKey;
        }

        private static byte[] CreateMessage(string label)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(label));
            if (hash.Length != BLS.MSG_SIZE)
            {
                throw new InvalidOperationException("Unexpected hash size for BLS message.");
            }

            return hash;
        }

        private static byte[] CreateDomain(string label)
        {
            var domain = SHA256.HashData(Encoding.UTF8.GetBytes($"domain:{label}"));
            if (domain.Length != 32)
            {
                throw new InvalidOperationException("Domains must be 32 bytes.");
            }

            return domain;
        }

        private static BLS.Signature AggregateSignatures(BLS.Signature[] signatures)
        {
            if (signatures == null || signatures.Length == 0)
            {
                throw new ArgumentException("At least one signature is required.", nameof(signatures));
            }

            var aggregate = signatures[0];
            for (var i = 1; i < signatures.Length; i++)
            {
                aggregate.Add(signatures[i]);
            }

            return aggregate;
        }

        private static class NativeLibraryLocator
        {
            public static bool TryEnsure(out string reason)
            {
                var rid = GetRuntimeIdentifier();
                if (rid == null)
                {
                    reason = "Herumi native tests are only enabled on Windows/Linux x64 for now.";
                    return false;
                }

                var libraryName = GetLibraryFileName();
                var targetPath = Path.Combine(AppContext.BaseDirectory, libraryName);

                if (File.Exists(targetPath))
                {
                    reason = string.Empty;
                    return true;
                }

                foreach (var candidate in GetCandidatePaths(rid, libraryName))
                {
                    if (!File.Exists(candidate))
                    {
                        continue;
                    }

                    File.Copy(candidate, targetPath, true);
                    reason = string.Empty;
                    return true;
                }

                reason = $"Native BLS library '{libraryName}' not found. Run scripts/build-herumi-bls.(ps1|sh).";
                return false;
            }

            private static string? GetRuntimeIdentifier()
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
                    RuntimeInformation.ProcessArchitecture == Architecture.X64)
                {
                    return "win-x64";
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) &&
                    RuntimeInformation.ProcessArchitecture == Architecture.X64)
                {
                    return "linux-x64";
                }

                return null;
            }

            private static string GetLibraryFileName()
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return "bls_eth.dll";
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return "libbls_eth.so";
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return "libbls_eth.dylib";
                }

                throw new PlatformNotSupportedException("Unsupported OS for Herumi BLS.");
            }

            private static string[] GetCandidatePaths(string rid, string libraryName)
            {
                var candidates = new System.Collections.Generic.List<string>
                {
                    Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native", libraryName)
                };

                var repoCandidate = TryLocateRepoNative(libraryName, rid);
                if (!string.IsNullOrEmpty(repoCandidate))
                {
                    candidates.Add(repoCandidate);
                }

                return candidates.ToArray();
            }

            private static string? TryLocateRepoNative(string libraryName, string rid)
            {
                var root = TryLocateRepoRoot();
                if (root == null)
                {
                    return null;
                }

                return Path.Combine(root, "src", "Nethereum.Signer.Bls.Herumi", "runtimes", rid, "native", libraryName);
            }

            private static string? TryLocateRepoRoot()
            {
                var directory = new DirectoryInfo(AppContext.BaseDirectory);
                while (directory != null)
                {
                    var solution = Path.Combine(directory.FullName, "Nethereum.sln");
                    if (File.Exists(solution))
                    {
                        return directory.FullName;
                    }

                    directory = directory.Parent;
                }

                return null;
            }
        }
    }
}
