using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nethereum.Freezer.FilterMaps;
using Xunit;

namespace Nethereum.Freezer.UnitTests.FilterMaps
{
    public class LogValueHasherTests
    {
        public sealed class Vector
        {
            [JsonPropertyName("valueKind")] public string ValueKind { get; set; } = "";
            [JsonPropertyName("input")] public string Input { get; set; } = "";
            [JsonPropertyName("value")] public string Value { get; set; } = "";
            [JsonPropertyName("mapIndex")] public long MapIndex { get; set; }
            [JsonPropertyName("layer")] public int Layer { get; set; }
            [JsonPropertyName("lvIndex")] public long LvIndex { get; set; }
            [JsonPropertyName("maskedMapIndex")] public long MaskedMapIndex { get; set; }
            [JsonPropertyName("rowIndex")] public int RowIndex { get; set; }
            [JsonPropertyName("columnIndex")] public int ColumnIndex { get; set; }
        }

        private static readonly FilterMapsParams Params = FilterMapsParams.Default;

        public static IEnumerable<object[]> GethVectors()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "filtermaps-vectors.json");
            var json = File.ReadAllText(path);
            var vectors = JsonSerializer.Deserialize<List<Vector>>(json) ?? new List<Vector>();
            foreach (var v in vectors)
            {
                yield return new object[] { v };
            }
        }

        [Theory]
        [MemberData(nameof(GethVectors))]
        public void Given_GethVectors_When_RowIndexAndColumnIndex_Then_ReproduceByteForByte(Vector v)
        {
            var value = Convert.FromHexString(v.Value);
            var input = Convert.FromHexString(v.Input);

            var computedValue = v.ValueKind == "address"
                ? LogValueHasher.AddressValue(input)
                : LogValueHasher.TopicValue(input);
            Assert.Equal(v.Value, Convert.ToHexString(computedValue).ToLowerInvariant());

            var maskedMapIndex = LogValueHasher.MaskedMapIndex(v.MapIndex, v.Layer, Params);
            Assert.Equal(v.MaskedMapIndex, maskedMapIndex);

            var rowIndex = LogValueHasher.RowIndex(v.MapIndex, v.Layer, value, Params);
            Assert.Equal(v.RowIndex, rowIndex);

            var columnIndex = LogValueHasher.ColumnIndex(v.LvIndex, value, Params);
            Assert.Equal(v.ColumnIndex, columnIndex);
        }

        [Fact]
        public void Given_Layer0Vector_When_MaskedMapIndex_Then_ConstantPerEpoch()
        {
            var maskedForMapIndex5 = LogValueHasher.MaskedMapIndex(5, 0, Params);
            var maskedForMapIndex1000 = LogValueHasher.MaskedMapIndex(1000, 0, Params);

            Assert.Equal(maskedForMapIndex5, maskedForMapIndex1000);

            var value = Convert.FromHexString("e12f08743344c0eab7afaa22bb71e2725f7e13dece6ac2d23711054ee787b6c2");
            var rowIndexForMapIndex5 = LogValueHasher.RowIndex(5, 0, value, Params);
            var rowIndexForMapIndex1000 = LogValueHasher.RowIndex(1000, 0, value, Params);

            Assert.Equal(rowIndexForMapIndex5, rowIndexForMapIndex1000);
        }

        [Fact]
        public void Given_HighBitHashVector_When_ColumnIndex_Then_MatchesGeth()
        {
            var value = Convert.FromHexString("e12f08743344c0eab7afaa22bb71e2725f7e13dece6ac2d23711054ee787b6c2");

            var columnIndex = LogValueHasher.ColumnIndex(65535, value, Params);

            Assert.Equal(16777077, columnIndex);
        }
    }
}
