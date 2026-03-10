using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.ABI.UnitTests
{
    public class UtilDocExampleTests
    {
        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "keccak-hashing", "Keccak-256 hash a string, bytes, hex input, and return raw bytes")]
        public void ShouldHashWithKeccak256()
        {
            var keccak = Sha3Keccack.Current;

            var hashString = keccak.CalculateHash("hello");
            Assert.Equal("1c8aff950685c2ed4bc3174f3472287b56d9517b9c948127319a09a7a36deac8", hashString);

            var inputBytes = new byte[] { 0x68, 0x65, 0x6c, 0x6c, 0x6f };
            var hashBytes = keccak.CalculateHash(inputBytes);
            Assert.Equal("1c8aff950685c2ed4bc3174f3472287b56d9517b9c948127319a09a7a36deac8", hashBytes.ToHex());

            var hashFromHex = keccak.CalculateHashFromHex("0x68656c6c6f");
            Assert.Equal("1c8aff950685c2ed4bc3174f3472287b56d9517b9c948127319a09a7a36deac8", hashFromHex);

            var hashAsBytes = keccak.CalculateHashAsBytes("hello");
            Assert.Equal(32, hashAsBytes.Length);
            Assert.Equal("1c8aff950685c2ed4bc3174f3472287b56d9517b9c948127319a09a7a36deac8", hashAsBytes.ToHex());
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "address-checksum", "EIP-55 checksum addresses from lowercase and uppercase")]
        public void ShouldCreateChecksumAddresses()
        {
            var addressUtil = AddressUtil.Current;

            var fromLower = addressUtil.ConvertToChecksumAddress("0x5aaeb6053f3e94c9b9a09f33669435e7ef1beaed");
            Assert.Equal("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed", fromLower);

            var fromUpper = addressUtil.ConvertToChecksumAddress("0x5AAEB6053F3E94C9B9A09F33669435E7EF1BEAED");
            Assert.Equal("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed", fromUpper);

            Assert.True(addressUtil.IsChecksumAddress("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed"));
            Assert.False(addressUtil.IsChecksumAddress("0x5aaeb6053f3e94c9b9a09f33669435e7ef1beaed"));
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "address-validation", "Validate address format, length, and compare addresses")]
        public void ShouldValidateAndCompareAddresses()
        {
            var addressUtil = AddressUtil.Current;

            Assert.True(addressUtil.IsValidEthereumAddressHexFormat("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed"));
            Assert.False(addressUtil.IsValidEthereumAddressHexFormat("not-an-address"));

            Assert.True(addressUtil.IsValidAddressLength("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed"));
            Assert.False(addressUtil.IsValidAddressLength("0x1234"));

            Assert.True(addressUtil.AreAddressesTheSame(
                "0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed",
                "0x5AAEB6053F3E94C9B9A09F33669435E7EF1BEAED"));

            Assert.True("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed"
                .IsTheSameAddress("0x5aaeb6053f3e94c9b9a09f33669435e7ef1beaed"));
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "address-empty", "Empty address handling: null, 0x0, ZERO_ADDRESS, extensions")]
        public void ShouldHandleEmptyAddresses()
        {
            var addressUtil = AddressUtil.Current;

            Assert.True(addressUtil.IsAnEmptyAddress(null));
            Assert.True(addressUtil.IsAnEmptyAddress(""));
            Assert.True(addressUtil.IsAnEmptyAddress("0x0"));

            string nullAddr = null;
            Assert.Equal(AddressUtil.AddressEmptyAsHex, addressUtil.AddressValueOrEmpty(nullAddr));

            Assert.True(addressUtil.IsNotAnEmptyAddress("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed"));

            Assert.True("0x0".IsAnEmptyAddress());
            Assert.True("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed".IsNotAnEmptyAddress());

            Assert.Equal("0x0000000000000000000000000000000000000000", AddressUtil.ZERO_ADDRESS);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "unit-conversion", "Convert between Wei, Ether, and Gwei")]
        public void ShouldConvertWeiEtherGwei()
        {
            var convert = UnitConversion.Convert;

            var oneEtherInWei = convert.ToWei(1, UnitConversion.EthUnit.Ether);
            Assert.Equal(BigInteger.Parse("1000000000000000000"), oneEtherInWei);

            var etherValue = convert.FromWei(BigInteger.Parse("1500000000000000000"));
            Assert.Equal(1.5m, etherValue);

            var gweiInWei = convert.ToWei(21, UnitConversion.EthUnit.Gwei);
            Assert.Equal(BigInteger.Parse("21000000000"), gweiInWei);

            var gweiValue = convert.FromWei(BigInteger.Parse("21000000000"), UnitConversion.EthUnit.Gwei);
            Assert.Equal(21m, gweiValue);

            Assert.Equal(oneEtherInWei, UnitConversion.Convert.ToWei(1, UnitConversion.EthUnit.Ether));
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "unit-conversion", "BigDecimal high-precision Wei conversion round-trip")]
        public void ShouldConvertWithBigDecimalPrecision()
        {
            var convert = UnitConversion.Convert;
            var largeWei = BigInteger.Parse("123456789012345678901234567890");

            var bigDecimal = convert.FromWeiToBigDecimal(largeWei, UnitConversion.EthUnit.Ether);
            Assert.NotNull(bigDecimal);

            var backToWei = convert.ToWei(bigDecimal, UnitConversion.EthUnit.Ether);
            Assert.Equal(largeWei, backToWei);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "unit-conversion", "Convert using Finney, Szabo, Kether, and custom decimal places")]
        public void ShouldConvertDifferentUnits()
        {
            var convert = UnitConversion.Convert;

            var finneyInWei = convert.ToWei(1, UnitConversion.EthUnit.Finney);
            Assert.Equal(BigInteger.Parse("1000000000000000"), finneyInWei);

            var szaboInWei = convert.ToWei(1, UnitConversion.EthUnit.Szabo);
            Assert.Equal(BigInteger.Parse("1000000000000"), szaboInWei);

            var ketherInWei = convert.ToWei(1, UnitConversion.EthUnit.Kether);
            Assert.Equal(BigInteger.Parse("1000000000000000000000"), ketherInWei);

            var fromCustom = convert.FromWei(BigInteger.Parse("1000000"), 6);
            Assert.Equal(1m, fromCustom);

            var toCustom = convert.ToWei(1m, 6);
            Assert.Equal(BigInteger.Parse("1000000"), toCustom);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "address-collections", "UniqueAddressList and AddressEqualityComparer for case-insensitive dedup")]
        public void ShouldDeduplicateAddressesInUniqueList()
        {
            var uniqueList = new UniqueAddressList();

            uniqueList.Add("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed");
            uniqueList.Add("0x5AAEB6053F3E94C9B9A09F33669435E7EF1BEAED");
            uniqueList.Add("0x5aaeb6053f3e94c9b9a09f33669435e7ef1beaed");

            Assert.Single(uniqueList);

            var comparer = new AddressEqualityComparer();
            Assert.True(comparer.Equals(
                "0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed",
                "0x5AAEB6053F3E94C9B9A09F33669435E7EF1BEAED"));

            var dict = new Dictionary<string, int>(comparer);
            dict["0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed"] = 100;
            Assert.Equal(100, dict["0x5AAEB6053F3E94C9B9A09F33669435E7EF1BEAED"]);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "address-padding", "Pad short address to 20 bytes and convert bytes to checksum address")]
        public void ShouldPadAndConvertAddresses()
        {
            var addressUtil = AddressUtil.Current;

            var padded = addressUtil.ConvertToValid20ByteAddress("0x1234");
            Assert.Equal("0x0000000000000000000000000000000000001234", padded);

            var paddedNull = addressUtil.ConvertToValid20ByteAddress(null);
            Assert.Equal("0x0000000000000000000000000000000000000000", paddedNull);

            var addressBytes = "5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed".HexToByteArray();
            var checksumFromBytes = addressUtil.ConvertToChecksumAddress(addressBytes);
            Assert.Equal("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed", checksumFromBytes);
        }
    }
}
