using System;
using RocksDbSharp;

namespace Nethereum.CoreChain.RocksDB.History
{
    public static class RocksProfiles
    {
        private const ulong MB = 1024UL * 1024;
        private const ulong GB = 1024UL * MB;

        public const int BulkL0CompactionTrigger = 64;
        public const int BulkL0SlowdownTrigger = 200;
        public const int BulkL0StopTrigger = 400;

        public const int LiveIndexL0CompactionTrigger = 8;
        public const int LiveIndexL0SlowdownTrigger = 20;
        public const int LiveIndexL0StopTrigger = 36;
        public const long LiveIndexSoftPendingCompactionBytes = 8L * (long)GB;
        public const long LiveIndexHardPendingCompactionBytes = 32L * (long)GB;

        public static Cache CreateSharedCache(long bytes) => Cache.CreateLru((ulong)bytes);

        public static DbOptions Db(int cores, long sharedMemtableBudgetBytes)
        {
            var o = new DbOptions()
                .SetCreateIfMissing(true)
                .SetCreateMissingColumnFamilies(true)
                .SetMaxBackgroundCompactions(cores)
                .SetMaxBackgroundFlushes(2)
                .SetBytesPerSync(1 * MB)
                .SetMaxTotalWalSize(512 * MB)
                .SetDbWriteBufferSize((ulong)sharedMemtableBudgetBytes);

            TryNative(() => Native.Instance.rocksdb_options_set_max_subcompactions(
                o.Handle, (uint)Math.Max(1, Math.Min(4, cores / 2))));
            return o;
        }

        private static ColumnFamilyOptions LeveledIndexCf(
            Cache sharedCache, int l0CompactionTrigger, int l0SlowdownTrigger, int l0StopTrigger,
            ulong softPendingCompactionBytes, ulong hardPendingCompactionBytes)
        {
            var table = new BlockBasedTableOptions()
                .SetBlockCache(sharedCache)
                .SetFilterPolicy(BloomFilterPolicy.Create(10))
                .SetWholeKeyFiltering(true)
                .SetCacheIndexAndFilterBlocks(true)
                .SetPinL0FilterAndIndexBlocksInCache(true)
                .SetBlockSize(16 * 1024);

            var cf = new ColumnFamilyOptions()
                .SetBlockBasedTableFactory(table)
                .SetWriteBufferSize(256 * MB)
                .SetMaxWriteBufferNumber(4)
                .SetTargetFileSizeBase(256 * MB)
                .SetLevelCompactionDynamicLevelBytes(true)
                .SetLevel0FileNumCompactionTrigger(l0CompactionTrigger)
                .SetLevel0SlowdownWritesTrigger(l0SlowdownTrigger)
                .SetLevel0StopWritesTrigger(l0StopTrigger)
                .SetSoftPendingCompactionBytesLimit(softPendingCompactionBytes)
                .SetHardPendingCompactionBytesLimit(hardPendingCompactionBytes)
                .SetCompression(Compression.Lz4);

            TryNative(() => Native.Instance.rocksdb_options_set_bottommost_compression(
                cf.Handle, (int)Compression.Zstd));
            return cf;
        }

        public static ColumnFamilyOptions Bulk(Cache sharedCache)
            => LeveledIndexCf(sharedCache, BulkL0CompactionTrigger, BulkL0SlowdownTrigger, BulkL0StopTrigger,
                128 * GB, 320 * GB);

        public static ColumnFamilyOptions LiveIndex(Cache sharedCache)
            => LeveledIndexCf(sharedCache, LiveIndexL0CompactionTrigger, LiveIndexL0SlowdownTrigger, LiveIndexL0StopTrigger,
                (ulong)LiveIndexSoftPendingCompactionBytes, (ulong)LiveIndexHardPendingCompactionBytes);

        public const int UniversalL0SlowdownTrigger = 200;
        public const int UniversalL0StopTrigger = 400;
        public const ulong UniversalSoftPendingCompactionBytes = 128UL * GB;
        public const ulong UniversalHardPendingCompactionBytes = 320UL * GB;

        public static ColumnFamilyOptions UniversalIndexCf(Cache sharedCache)
            => UniversalIndexCf(sharedCache, UniversalCompactionSettings.BulkDefault);

        public static ColumnFamilyOptions UniversalIndexCf(Cache sharedCache, UniversalCompactionSettings settings)
        {
            var table = new BlockBasedTableOptions()
                .SetBlockCache(sharedCache)
                .SetFilterPolicy(BloomFilterPolicy.Create(10))
                .SetWholeKeyFiltering(true)
                .SetCacheIndexAndFilterBlocks(true)
                .SetPinL0FilterAndIndexBlocksInCache(true)
                .SetBlockSize(16 * 1024);

            if (settings.PartitionedFilters)
            {
                table.SetIndexType(BlockBasedTableIndexType.TwoLevelIndex);
                TryNative(() => Native.Instance.rocksdb_block_based_options_set_partition_filters(table.Handle, true));
            }

            var cf = new ColumnFamilyOptions()
                .SetBlockBasedTableFactory(table)
                .SetWriteBufferSize(settings.WriteBufferSize)
                .SetMaxWriteBufferNumber(4)
                .SetTargetFileSizeBase(settings.TargetFileSizeBase)
                .SetLevel0FileNumCompactionTrigger(settings.Level0FileNumCompactionTrigger)
                .SetLevel0SlowdownWritesTrigger(UniversalL0SlowdownTrigger)
                .SetLevel0StopWritesTrigger(UniversalL0StopTrigger)
                .SetSoftPendingCompactionBytesLimit(UniversalSoftPendingCompactionBytes)
                .SetHardPendingCompactionBytesLimit(UniversalHardPendingCompactionBytes)
                .SetCompression(Compression.Lz4)
                .SetCompactionStyle(Compaction.Universal);

            var universal = Native.Instance.rocksdb_universal_compaction_options_create();
            try
            {
                Native.Instance.rocksdb_universal_compaction_options_set_size_ratio(universal, settings.SizeRatioPercent);
                Native.Instance.rocksdb_universal_compaction_options_set_min_merge_width(universal, settings.MinMergeWidth);
                Native.Instance.rocksdb_universal_compaction_options_set_max_merge_width(universal, settings.MaxMergeWidth);
                Native.Instance.rocksdb_universal_compaction_options_set_max_size_amplification_percent(universal, settings.MaxSizeAmplificationPercent);
                Native.Instance.rocksdb_universal_compaction_options_set_stop_style(universal,
                    (int)(settings.StopStyleSimilarSize ? SizeCompactionStopStyle.Similar : SizeCompactionStopStyle.Total));
                cf.SetUniversalCompactionOptions(universal);
            }
            finally
            {
                Native.Instance.rocksdb_universal_compaction_options_destroy(universal);
            }

            TryNative(() => Native.Instance.rocksdb_options_set_bottommost_compression(
                cf.Handle, (int)Compression.Zstd));

            if (settings.PeriodicCompactionSeconds > 0)
                TryNative(() => Native.Instance.rocksdb_options_set_periodic_compaction_seconds(
                    cf.Handle, settings.PeriodicCompactionSeconds));

            return cf;
        }

        public static ColumnFamilyOptions Control(Cache sharedCache)
        {
            var table = new BlockBasedTableOptions()
                .SetBlockCache(sharedCache)
                .SetFilterPolicy(BloomFilterPolicy.Create(10))
                .SetWholeKeyFiltering(true)
                .SetCacheIndexAndFilterBlocks(true);

            return new ColumnFamilyOptions()
                .SetBlockBasedTableFactory(table)
                .SetWriteBufferSize(64 * MB)
                .SetMaxWriteBufferNumber(2)
                .SetLevelCompactionDynamicLevelBytes(true)
                .SetCompression(Compression.Lz4);
        }

        public static ColumnFamilyOptions IndexSstOptions()
        {
            var table = new BlockBasedTableOptions()
                .SetFilterPolicy(BloomFilterPolicy.Create(10))
                .SetWholeKeyFiltering(true);
            return new ColumnFamilyOptions()
                .SetBlockBasedTableFactory(table)
                .SetCompression(Compression.Lz4);
        }

        public static ColumnFamilyOptions ForHistoryProfile(
            HistoryColumnFamilies.HistoryCfProfile profile, Cache sharedCache)
            => profile switch
            {
                HistoryColumnFamilies.HistoryCfProfile.UniversalIndex => UniversalIndexCf(sharedCache),
                HistoryColumnFamilies.HistoryCfProfile.LiveIndex => LiveIndex(sharedCache),
                HistoryColumnFamilies.HistoryCfProfile.Control => Control(sharedCache),
                _ => Bulk(sharedCache),
            };

        public static ColumnFamilies BuildColumnFamilies(Cache sharedCache)
        {
            var cfs = new ColumnFamilies();
            foreach (var (name, profile) in HistoryColumnFamilies.Catalogue)
                cfs.Add(name, ForHistoryProfile(profile, sharedCache));
            return cfs;
        }


        public static ColumnFamilyOptions ForLive(LiveCfProfile profile, Cache sharedCache, StoragePreset preset)
        {
            switch (profile)
            {
                case LiveCfProfile.RandomPoint: return RandomPoint(sharedCache, preset);
                case LiveCfProfile.LocationSequential: return LocationSequential(sharedCache, preset);
                case LiveCfProfile.SecondaryIndex: return SecondaryIndex(sharedCache, preset);
                case LiveCfProfile.UniversalBulkPoint: return UniversalIndexCf(sharedCache, UniversalCompactionSettings.TrieDefault);
                case LiveCfProfile.HotTiny:
                default: return HotTiny(sharedCache, preset);
            }
        }

        private static ulong BufferBytes(StoragePreset preset, ulong full)
        {
            switch (preset)
            {
                case StoragePreset.AppChainSmall: return Math.Max(8 * MB, full / 8);
                default: return full;
            }
        }

        private static ColumnFamilyOptions WithLivePendingCompactionLimits(this ColumnFamilyOptions cf)
            => cf.SetSoftPendingCompactionBytesLimit(128 * GB)
                 .SetHardPendingCompactionBytesLimit(320 * GB);

        public static ColumnFamilyOptions RandomPoint(Cache sharedCache, StoragePreset preset)
        {
            var table = new BlockBasedTableOptions()
                .SetBlockCache(sharedCache)
                .SetFilterPolicy(BloomFilterPolicy.Create(10))
                .SetWholeKeyFiltering(true)
                .SetCacheIndexAndFilterBlocks(true)
                .SetPinL0FilterAndIndexBlocksInCache(true)
                .SetBlockSize(8 * 1024);

            var cf = new ColumnFamilyOptions()
                .SetBlockBasedTableFactory(table)
                .SetWriteBufferSize(BufferBytes(preset, 128 * MB))
                .SetMaxWriteBufferNumber(preset == StoragePreset.SyncBulk ? 4 : 3)
                .SetTargetFileSizeBase(64 * MB)
                .SetLevelCompactionDynamicLevelBytes(true)
                .SetCompression(Compression.Lz4)
                .WithLivePendingCompactionLimits();

            TryNative(() => Native.Instance.rocksdb_options_set_bottommost_compression(
                cf.Handle, (int)Compression.Zstd));
            return cf;
        }

        public static ColumnFamilyOptions LocationSequential(Cache sharedCache, StoragePreset preset)
        {
            var table = new BlockBasedTableOptions()
                .SetBlockCache(sharedCache)
                .SetFilterPolicy(BloomFilterPolicy.Create(10))
                .SetWholeKeyFiltering(true)
                .SetCacheIndexAndFilterBlocks(true)
                .SetPinL0FilterAndIndexBlocksInCache(true)
                .SetBlockSize(16 * 1024);

            var cf = new ColumnFamilyOptions()
                .SetBlockBasedTableFactory(table)
                .SetWriteBufferSize(BufferBytes(preset, 256 * MB))
                .SetMaxWriteBufferNumber(4)
                .SetTargetFileSizeBase(256 * MB)
                .SetLevelCompactionDynamicLevelBytes(true)
                .SetCompression(Compression.Lz4)
                .WithLivePendingCompactionLimits();

            TryNative(() => Native.Instance.rocksdb_options_set_bottommost_compression(
                cf.Handle, (int)Compression.Zstd));
            return cf;
        }

        public static ColumnFamilyOptions SecondaryIndex(Cache sharedCache, StoragePreset preset)
        {
            var table = new BlockBasedTableOptions()
                .SetBlockCache(sharedCache)
                .SetFilterPolicy(BloomFilterPolicy.Create(10))
                .SetWholeKeyFiltering(true)
                .SetCacheIndexAndFilterBlocks(true);

            return new ColumnFamilyOptions()
                .SetBlockBasedTableFactory(table)
                .SetWriteBufferSize(BufferBytes(preset, 64 * MB))
                .SetMaxWriteBufferNumber(2)
                .SetLevelCompactionDynamicLevelBytes(true)
                .SetCompression(Compression.Lz4)
                .WithLivePendingCompactionLimits();
        }

        public static ColumnFamilyOptions HotTiny(Cache sharedCache, StoragePreset preset)
        {
            var table = new BlockBasedTableOptions()
                .SetBlockCache(sharedCache)
                .SetFilterPolicy(BloomFilterPolicy.Create(10))
                .SetWholeKeyFiltering(true)
                .SetCacheIndexAndFilterBlocks(true)
                .SetPinL0FilterAndIndexBlocksInCache(true);

            return new ColumnFamilyOptions()
                .SetBlockBasedTableFactory(table)
                .SetWriteBufferSize(BufferBytes(preset, 16 * MB))
                .SetMaxWriteBufferNumber(2)
                .SetCompression(Compression.Lz4)
                .WithLivePendingCompactionLimits();
        }


        public static IntPtr CreateAutoTunedRateLimiter(long bytesPerSecond)
        {
            try { return Native.Instance.rocksdb_ratelimiter_create_auto_tuned(bytesPerSecond, 100_000, 10); }
            catch { return IntPtr.Zero; }
        }

        public static void AttachRateLimiter(DbOptions options, IntPtr limiter)
        {
            if (limiter == IntPtr.Zero) return;
            TryNative(() => Native.Instance.rocksdb_options_set_ratelimiter(options.Handle, limiter));
        }

        public static void DestroyRateLimiter(IntPtr limiter)
        {
            if (limiter == IntPtr.Zero) return;
            TryNative(() => Native.Instance.rocksdb_ratelimiter_destroy(limiter));
        }

        private static void TryNative(Action apply)
        {
            try { apply(); } catch { }
        }
    }
}
