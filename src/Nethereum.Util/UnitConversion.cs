using System;
using System.Numerics;

namespace Nethereum.Util
{
    public class UnitConversion
    {
        public enum EthUnit
        {
            Wei,
            Kwei,
            Ada,
            Femtoether,
            Mwei,
            Babbage,
            Picoether,
            Gwei,
            Shannon,
            Nanoether,
            Nano,
            Szabo,
            Microether,
            Micro,
            Finney,
            Milliether,
            Milli,
            Ether,
            Kether,
            Grand,
            Einstein,
            Mether,
            Gether,
            Tether
        }

        private static UnitConversion convert;

        public static UnitConversion Convert
        {
            get
            {
                if (convert == null) convert = new UnitConversion();
                return convert;
            }
        }

        public decimal FromWei(BigInteger value, BigInteger toUnit)
        {
            return (decimal) value/(decimal) toUnit;
        }

        public decimal FromWei(BigInteger value, EthUnit toUnit = EthUnit.Ether)
        {
            return FromWei(value, GetEthUnitValue(toUnit));
        }

        public decimal FromWei(BigInteger value, int decimalPlacesToUnit)
        {
            return FromWei(value, BigInteger.Pow(10, decimalPlacesToUnit));
        }

        public BigInteger GetEthUnitValue(EthUnit ethUnit)
        {
            switch (ethUnit)
            {
                case EthUnit.Wei:
                    return BigInteger.Parse("1");
                case EthUnit.Kwei:
                    return BigInteger.Parse("1000");
                case EthUnit.Ada:
                    return BigInteger.Parse("1000");
                case EthUnit.Femtoether:
                    return BigInteger.Parse("1000");
                case EthUnit.Mwei:
                    return BigInteger.Parse("1000000");
                case EthUnit.Babbage:
                    return BigInteger.Parse("1000000");
                case EthUnit.Picoether:
                    return BigInteger.Parse("1000000");
                case EthUnit.Gwei:
                    return BigInteger.Parse("1000000000");
                case EthUnit.Shannon:
                    return BigInteger.Parse("1000000000");
                case EthUnit.Nanoether:
                    return BigInteger.Parse("1000000000");
                case EthUnit.Nano:
                    return BigInteger.Parse("1000000000");
                case EthUnit.Szabo:
                    return BigInteger.Parse("1000000000000");
                case EthUnit.Microether:
                    return BigInteger.Parse("1000000000000");
                case EthUnit.Micro:
                    return BigInteger.Parse("1000000000000");
                case EthUnit.Finney:
                    return BigInteger.Parse("1000000000000000");
                case EthUnit.Milliether:
                    return BigInteger.Parse("1000000000000000");
                case EthUnit.Milli:
                    return BigInteger.Parse("1000000000000000");
                case EthUnit.Ether:
                    return BigInteger.Parse("1000000000000000000");
                case EthUnit.Kether:
                    return BigInteger.Parse("1000000000000000000000");
                case EthUnit.Grand:
                    return BigInteger.Parse("1000000000000000000000");
                case EthUnit.Einstein:
                    return BigInteger.Parse("1000000000000000000000");
                case EthUnit.Mether:
                    return BigInteger.Parse("1000000000000000000000000");
                case EthUnit.Gether:
                    return BigInteger.Parse("1000000000000000000000000000");
                case EthUnit.Tether:
                    return BigInteger.Parse("1000000000000000000000000000000");
            }
            throw new NotImplementedException();
        }

        public BigInteger ToWei(decimal amount, BigInteger fromUnit)
        {
            var decimalPlaces = CalculateNumberOfDecimalPlaces(amount);
            if (decimalPlaces == 0) return BigInteger.Multiply(new BigInteger(amount), fromUnit);

            var decimalConversionUnit = System.Convert.ToInt64(Math.Pow(10, decimalPlaces));

            var amountFromDec = new BigInteger(amount*decimalConversionUnit);
            var unitFromDec = BigInteger.Divide(fromUnit, decimalConversionUnit);
            return amountFromDec*unitFromDec;
        }

        public BigInteger ToWei(decimal amount, EthUnit fromUnit = EthUnit.Ether)
        {
            return ToWei(amount, GetEthUnitValue(fromUnit));
        }

        public BigInteger ToWei(decimal amount, int decimalPlacesFromUnit)
        {
            return ToWei(amount, BigInteger.Pow(10, decimalPlacesFromUnit));
        }

        public BigInteger ToWei(BigInteger value, EthUnit fromUnit = EthUnit.Ether)
        {
            return value*GetEthUnitValue(fromUnit);
        }

        public BigInteger ToWei(int value, EthUnit fromUnit = EthUnit.Ether)
        {
            return ToWei(new BigInteger(value), fromUnit);
        }

        public BigInteger ToWei(double value, EthUnit fromUnit = EthUnit.Ether)
        {
            return ToWei(System.Convert.ToDecimal(value), fromUnit);
        }

        public BigInteger ToWei(float value, EthUnit fromUnit = EthUnit.Ether)
        {
            return ToWei(System.Convert.ToDecimal(value), fromUnit);
        }

        public BigInteger ToWei(long value, EthUnit fromUnit = EthUnit.Ether)
        {
            return ToWei(new BigInteger(value), fromUnit);
        }

        public BigInteger ToWei(string value, EthUnit fromUnit = EthUnit.Ether)
        {
            return ToWei(decimal.Parse(value), fromUnit);
        }

        private int CalculateNumberOfDecimalPlaces(double value, int currentNumberOfDecimals = 0)
        {
            return CalculateNumberOfDecimalPlaces(System.Convert.ToDecimal(value), currentNumberOfDecimals);
        }

        private int CalculateNumberOfDecimalPlaces(float value, int currentNumberOfDecimals = 0)
        {
            return CalculateNumberOfDecimalPlaces(System.Convert.ToDecimal(value), currentNumberOfDecimals);
        }

        private int CalculateNumberOfDecimalPlaces(decimal value, int currentNumberOfDecimals = 0)
        {
            var multiplied = value*System.Convert.ToInt64(Math.Pow(10, currentNumberOfDecimals));
            if (Math.Round(multiplied) == multiplied)
                return currentNumberOfDecimals;
            return CalculateNumberOfDecimalPlaces(value, currentNumberOfDecimals + 1);
        }
    }
}