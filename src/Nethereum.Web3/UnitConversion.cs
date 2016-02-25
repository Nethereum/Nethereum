using System;
using System.Numerics;

namespace Nethereum.Web3
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

        
        public BigInteger FromWei(BigInteger value, EthUnit fromUnit = EthUnit.Ether)
        {
            return value/GetEthUnitValue(fromUnit);
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
            return ToWei(new BigInteger(value), fromUnit);
        }

        public BigInteger ToWei(float value, EthUnit fromUnit = EthUnit.Ether)
        {
            return ToWei(new BigInteger(value), fromUnit);
        }

        public BigInteger ToWei(long value, EthUnit fromUnit = EthUnit.Ether)
        {
            return ToWei(new BigInteger(value), fromUnit);
        }

        public BigInteger ToWei(decimal value, EthUnit fromUnit = EthUnit.Ether)
        {
            return ToWei(new BigInteger(value), fromUnit);
        }

        public BigInteger ToWei(string value, EthUnit fromUnit = EthUnit.Ether)
        {
            return ToWei(BigInteger.Parse(value), fromUnit);
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
    }
}