using System;
using System.Collections.Generic;

namespace Nethereum.EVM.SourceInfo
{
    public class SourceMapUtil
    {
        private int GetSourceMapInt32Value(string[] itemInfo, int position, Func<int> previousSourceMapValue)
        {
            if (itemInfo.Length > position)
            {
                var positionValue = itemInfo[position];
                if (string.IsNullOrEmpty(positionValue))
                {
                    return previousSourceMapValue();
                }
                else
                {
                    return Convert.ToInt32(positionValue);
                }
            }
            else
            {
                return previousSourceMapValue();
            }
        }

        private string GetSourceMapStringValue(string[] itemInfo, int position, Func<string> previousSourceMapValue)
        {
            if (itemInfo.Length > position)
            {
                var positionValue = itemInfo[position];
                if (string.IsNullOrEmpty(positionValue))
                {
                    return previousSourceMapValue();
                }
                else
                {
                    return positionValue;
                }
            }
            else
            {
                return previousSourceMapValue();
            }
        }

        /// <summary>
        /// This uncompresses the solidity runtime mappings  Extract using bin-runtime,srcmap-runtime
        /// https://docs.soliditylang.org/en/latest/internals/source_mappings.html
        /// </summary>
        public List<SourceMap> UnCompressSourceMap(string sourceMapsString)
        {
            var splittedMapItems = sourceMapsString.Split(';');
            var sourceMaps = new List<SourceMap>();
            for (int i = 0; i < splittedMapItems.Length; i++)
            {
                SourceMap previousSourceMap;
                if (i == 0)
                {
                    previousSourceMap = new SourceMap() { Position = 0, Length = 0, SourceFile = 0 };
                }
                else
                {
                    previousSourceMap = sourceMaps[i - 1];
                }
                var sourceMap = new SourceMap();
                var itemInfo = splittedMapItems[i].Split(':');

                sourceMap.Position = GetSourceMapInt32Value(itemInfo, 0, () => previousSourceMap.Position);
                sourceMap.Length = GetSourceMapInt32Value(itemInfo, 1, () => previousSourceMap.Length);
                sourceMap.SourceFile = GetSourceMapInt32Value(itemInfo, 2, () => previousSourceMap.SourceFile);
                sourceMap.JumpType = GetSourceMapStringValue(itemInfo, 3, () => previousSourceMap.JumpType);
                sourceMap.ModifierDepth = GetSourceMapInt32Value(itemInfo, 4, () => previousSourceMap.ModifierDepth);
                sourceMaps.Add(sourceMap);
            }

            return sourceMaps;
        }
    }
}