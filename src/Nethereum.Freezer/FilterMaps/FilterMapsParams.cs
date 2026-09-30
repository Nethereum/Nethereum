using System;

namespace Nethereum.Freezer.FilterMaps
{
    public readonly struct FilterMapsParams
    {
        public int LogMapHeight { get; }
        public int LogMapWidth { get; }
        public int LogMapsPerEpoch { get; }
        public int LogValuesPerMap { get; }
        public int BaseRowGroupSize { get; }
        public int BaseRowLengthRatio { get; }
        public int LogLayerDiff { get; }

        public FilterMapsParams(
            int logMapHeight,
            int logMapWidth,
            int logMapsPerEpoch,
            int logValuesPerMap,
            int baseRowGroupSize,
            int baseRowLengthRatio,
            int logLayerDiff)
        {
            LogMapHeight = logMapHeight;
            LogMapWidth = logMapWidth;
            LogMapsPerEpoch = logMapsPerEpoch;
            LogValuesPerMap = logValuesPerMap;
            BaseRowGroupSize = baseRowGroupSize;
            BaseRowLengthRatio = baseRowLengthRatio;
            LogLayerDiff = logLayerDiff;
        }

        public static FilterMapsParams Default { get; } = new FilterMapsParams(
            logMapHeight: 16,
            logMapWidth: 24,
            logMapsPerEpoch: 10,
            logValuesPerMap: 16,
            baseRowGroupSize: 32,
            baseRowLengthRatio: 8,
            logLayerDiff: 4);

        public int MapHeight => 1 << LogMapHeight;
        public int MapWidth => 1 << LogMapWidth;
        public int MapsPerEpoch => 1 << LogMapsPerEpoch;
        public int ValuesPerMap => 1 << LogValuesPerMap;
        public int BaseRowLength => (ValuesPerMap * BaseRowLengthRatio) >> LogMapHeight;

        public int MaxRowLength(int layer)
        {
            var saturatingShift = Math.Min(layer * LogLayerDiff, LogMapsPerEpoch);
            return BaseRowLength << saturatingShift;
        }

        public void Sanitize()
        {
            if (LogMapWidth % 8 != 0)
            {
                throw new ArgumentException($"invalid configuration: logMapWidth ({LogMapWidth}) must be a multiple of 8");
            }

            if (BaseRowGroupSize == 0 || (BaseRowGroupSize & (BaseRowGroupSize - 1)) != 0)
            {
                throw new ArgumentException($"invalid configuration: baseRowGroupSize ({BaseRowGroupSize}) must be a power of 2");
            }

            if (MapsPerEpoch % BaseRowGroupSize != 0)
            {
                throw new ArgumentException($"invalid configuration: mapsPerEpoch ({MapsPerEpoch}) must be a multiple of baseRowGroupSize ({BaseRowGroupSize})");
            }
        }
    }
}
