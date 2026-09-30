namespace Nethereum.MainnetChain.Configuration
{
    public static class MainnetChainCliArgs
    {
        public static void Apply(MainnetChainServerConfig config, string[] args)
        {
            if (config is null || args is null) return;

            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                string Next() => i + 1 < args.Length ? args[++i] : null;

                switch (arg)
                {
                    case "-p":
                    case "--port":
                        if (int.TryParse(Next(), out var port)) config.Port = port;
                        break;
                    case "--host":
                        { var h = Next(); if (h != null) config.Host = h; }
                        break;
                    case "-v":
                    case "--verbose":
                        config.Verbose = true;
                        break;
                    case "-d":
                    case "--data-dir":
                        { var d = Next(); if (d != null) config.DataDir = d; }
                        break;
                    case "--snap":
                        config.SnapBootstrap = true;
                        break;
                    case "--no-snap":
                        config.SnapBootstrap = false;
                        break;

                    case "--enable-tx-submission":
                        config.EnableTxSubmission = true;
                        break;
                    case "--wipe-state":
                        config.WipeState = true;
                        break;
                    case "--compact-all":
                        config.CompactAll = true;
                        break;
                    case "--rebuild-state-from-flat":
                        config.RebuildStateFromFlat = true;
                        break;
                    case "--verify-flat":
                        config.VerifyFlat = true;
                        break;
                    case "--verify-flat-sample":
                        if (long.TryParse(Next(), out var sampleCap)) config.VerifyFlatSampleAccountsPerShard = sampleCap;
                        break;
                    case "--trusted-peer":
                        { var t = Next(); if (t != null) config.TrustedPeer = t; }
                        break;
                    case "--node-key-file":
                        { var t = Next(); if (t != null) config.NodeKeyFile = t; }
                        break;
                    case "--listen-port":
                        if (int.TryParse(Next(), out var lp)) config.ListenPort = lp;
                        break;
                    case "--metrics-port":
                        if (int.TryParse(Next(), out var mp)) config.MetricsPort = mp;
                        break;
                    case "--beacon":
                        {
                            var url = Next();
                            if (url != null)
                            {
                                config.LightClient ??= new LightClientConfigSection();
                                config.LightClient.BeaconEndpoint = url;
                            }
                        }
                        break;
                    case "--allow-unverified-consensus":
                        config.AllowUnverifiedConsensus = true;
                        break;
                    case "--flush-cadence":
                        if (int.TryParse(Next(), out var fc)) config.FlushCadenceBlocks = fc;
                        break;
                    case "--checkpoint-every":
                        if (ulong.TryParse(Next(), out var ce)) config.CheckpointEvery = ce;
                        break;
                    case "--cache-size":
                        if (TryParseByteSize(Next(), out var cacheBytes)) config.BlockCacheSize = cacheBytes;
                        break;
                    case "--history-backfill":
                        if (TryParseHistoryBackfillMode(Next(), out var mode)) config.HistoryBackfill = mode;
                        break;
                }
            }
        }

        private static bool TryParseHistoryBackfillMode(string s, out HistoryBackfillMode mode)
        {
            mode = HistoryBackfillMode.DuringStateSync;
            if (string.IsNullOrWhiteSpace(s)) return false;
            switch (s.Trim().ToLowerInvariant())
            {
                case "during":
                case "duringstatesync":
                    mode = HistoryBackfillMode.DuringStateSync;
                    return true;
                case "after":
                case "afterstatesync":
                    mode = HistoryBackfillMode.AfterStateSync;
                    return true;
                case "off":
                case "never":
                    mode = HistoryBackfillMode.Never;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryParseByteSize(string s, out long bytes)
        {
            bytes = 0;
            if (string.IsNullOrWhiteSpace(s)) return false;

            var trimmed = s.Trim();
            var lastChar = char.ToUpperInvariant(trimmed[trimmed.Length - 1]);
            long multiplier = lastChar switch
            {
                'K' => 1024L,
                'M' => 1024L * 1024,
                'G' => 1024L * 1024 * 1024,
                _ => 0L
            };

            var numericPart = multiplier == 0 ? trimmed : trimmed.Substring(0, trimmed.Length - 1);
            if (!long.TryParse(numericPart, out var value)) return false;
            if (value <= 0) return false;

            bytes = multiplier == 0 ? value : value * multiplier;
            return true;
        }
    }
}
