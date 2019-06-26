using System;
using System.Globalization;
using System.Threading;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.Util.UnitTests
{
    public class AddressUtilTests
    {
        [Fact]
        public void ShouldCompareAddressesCorrectly()
        {
            var address1 = "0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed";
            var address2 = "0x5aaeb6053F3E94C9b9A09f33669435E7Ef1BeAed";

            Assert.True(address1.IsTheSameAddress(address2));

            var address3 = "0x5aaeb6053F3E94C9b9A09f33669435E7Ef1BeAex";

            Assert.False(address1.IsTheSameAddress(address3));


            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("da-DK");
            var address4 = "0xc25aeeacaa3f110612086febfb423fb34cb9952c";
            var address5 = "0xc25AEEaCaA3f110612086fEbfb423fb34cB9952C";

            Assert.True(address4.IsTheSameAddress(address5));

            Assert.False(string.Equals(address4.EnsureHexPrefix(), address5.EnsureHexPrefix(), StringComparison.CurrentCultureIgnoreCase));

            Assert.False(address4.IsTheSameAddress(null));
            string address6 = null;

            //strange behaviour, but still permitted.
            Assert.True(address6.IsTheSameAddress(null));
        }

        public string ToChecksumAddress(string address)
        {
            return new AddressUtil().ConvertToChecksumAddress(address);
        }

        [Fact]
        public virtual void ShouldValidateAddressHexFormat()
        {
            var address1 = "0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed";
            var address2 = "0x5aaeb6053F3E94C9b9A09f33669435E7Ef1BeAed";
            Assert.True(address1.IsValidEthereumAddressHexFormat());
            Assert.True(address2.IsValidEthereumAddressHexFormat());

            var address3 = "5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed";
            Assert.False(address3.IsValidEthereumAddressHexFormat());
            //length
            var address4 = "0x5aAeb6053F3E94C9b9A09f33669435E7Ef1Be";
            Assert.False(address4.IsValidEthereumAddressHexFormat());
            //non alpha
            //length
            var address5 = "0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeA'#";
            Assert.False(address5.IsValidEthereumAddressHexFormat());


            var address6 = "0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAeZ";
            Assert.False(address6.IsValidEthereumAddressHexFormat());
        }

        [Fact]
        public virtual void ShouldCheckIsCheckSumAddress()
        {
            var address1 = "0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed";
            var address1F = "0x5aaeb6053F3E94C9b9A09f33669435E7Ef1BeAed";
            var address2 = "0xfB6916095ca1df60bB79Ce92cE3Ea74c37c5d359";
            var address2F = "0xfb6916095ca1df60bB79Ce92cE3Ea74c37c5d359";
            var address3 = "0xdbF03B407c01E7cD3CBea99509d93f8DDDC8C6FB";
            var address4 = "0xD1220A0cf47c7B9Be7A2E6BA89F429762e7b9aDb";
            var addressUtil = new AddressUtil();
            Assert.True(addressUtil.IsChecksumAddress(address1));
            Assert.False(addressUtil.IsChecksumAddress(address1F));
            Assert.True(addressUtil.IsChecksumAddress(address2));
            Assert.False(addressUtil.IsChecksumAddress(address2F));
            Assert.True(addressUtil.IsChecksumAddress(address3));
            Assert.True(addressUtil.IsChecksumAddress(address4));
        }

        [Fact]
        public virtual void ShouldCreateACheckSumAddress()
        {
            var address1 = "0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed";
            var address2 = "0xfB6916095ca1df60bB79Ce92cE3Ea74c37c5d359";
            var address3 = "0xdbF03B407c01E7cD3CBea99509d93f8DDDC8C6FB";
            var address4 = "0xD1220A0cf47c7B9Be7A2E6BA89F429762e7b9aDb";
            Assert.Equal(address1, ToChecksumAddress(address1.ToUpper()));
            Assert.Equal(address2, ToChecksumAddress(address2.ToUpper()));
            Assert.Equal(address3, ToChecksumAddress(address3.ToUpper()));
            Assert.Equal(address4, ToChecksumAddress(address4.ToUpper()));
        }

        public const string Address1 = "0x7009b29f2094457d3dba62d1953efea58176ba27";
        public const string Address2 = "0x1009b29f2094457d3dba62d1953efea58176ba27";
        public const string LowerCaseAddress1 = "0x7009b29f2094457d3dba62d1953efea58176ba27";
        public const string UpperCaseAddress1 = "0x7009B29F2094457D3DBA62D1953EFEA58176BA27";

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(AddressUtil.AddressEmptyAsHex)]
        public void IsAnEmptyAddress_When_Address_Is_Empty_Returns_True(string address)
        {
            Assert.True(address.IsAnEmptyAddress());
        }

        [Theory]
        [InlineData(Address1)]
        public void IsNotAnEmptyAddress_When_Address_Is_Not_Empty_Returns_True(string address)
        {
            Assert.True(address.IsNotAnEmptyAddress());
        }

        [Theory]
        [InlineData(AddressUtil.AddressEmptyAsHex, AddressUtil.AddressEmptyAsHex)]
        [InlineData(null, AddressUtil.AddressEmptyAsHex)]
        [InlineData("", null)]
        [InlineData(AddressUtil.AddressEmptyAsHex, "")]
        [InlineData(" ", " ")]
        [InlineData(LowerCaseAddress1, LowerCaseAddress1)]
        [InlineData(UpperCaseAddress1, LowerCaseAddress1)]
        [InlineData(LowerCaseAddress1, UpperCaseAddress1)]
        [InlineData(UpperCaseAddress1, UpperCaseAddress1)]
        public void EqualsAddress_When_Addresses_Are_Equal_Returns_True(string address1, string address2)
        {
            Assert.True(address1.IsTheSameAddress(address2));
            Assert.True(address2.IsTheSameAddress(address1));
        }

        [Theory]
        [InlineData(null, Address1)]
        [InlineData(AddressUtil.AddressEmptyAsHex, Address1)]
        [InlineData(Address1, Address2)]
        public void EqualsAddress_When_Addresses_Are_Not_Equal_Returns_False(string address1, string address2)
        {
            Assert.False(address1.IsTheSameAddress(address2));
            Assert.False(address2.IsTheSameAddress(address1));
        }

        [Theory]
        [InlineData(Address1)]
        [InlineData(AddressUtil.AddressEmptyAsHex)]
        public void AddressOrEmpty_When_The_Address_Is_Not_Empty_Returns_The_Address(string address)
        {
            Assert.Equal(address, address.AddressValueOrEmpty());
        }

        [Theory]
        [InlineData(AddressUtil.AddressEmptyAsHex)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public void AddressOrEmpty_When_The_Address_Is_Empty_Returns_An_Empty_Address(string address)
        {
            Assert.Equal(AddressUtil.AddressEmptyAsHex, address.AddressValueOrEmpty());
        }

       
    }
}