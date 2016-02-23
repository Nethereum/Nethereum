
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using System.Threading.Tasks;
    using edjCase.JsonRpc.Client;
    using Nethereum.ABI;
    using Nethereum.ABI.Util;
    using Nethereum.RPC.Eth.Transactions;

namespace Nethereum.Web3
{
    public class Web3
    {
        public RpcClient Client { get; private set; }

        public Web3(string url = @"http://localhost:8545/")
        {
            IntialiseRpcClient(url);
            Eth = new Eth(Client);
            Shh = new Shh(Client);
            Net = new Net(Client);
        }

        public Eth Eth { get; private set; }
        public Shh Shh { get; private set; }

        public Net Net { get; private set; }

        private void IntialiseRpcClient(string url)
        {
            this.Client = new RpcClient(new Uri(url));
        }

        public string Sha3(string value)
        {
            return new Sha3Keccack().CalculateHash(value);
        }
    }

    public class UnitConversion
    {
        public enum EthUnit
        {
            wei,
            kwei,
            ada,
            femtoether,
            mwei,
            babbage,
            picoether,
            gwei,
            shannon,
            nanoether,
            nano,
            szabo,
            microether,
            micro,
            finney,
            milliether,
            milli,
            ether,
            kether,
            grand,
            einstein,
            mether,
            gether,
            tether

        }

        public BigInteger ConvertTo(BigInteger value, EthUnit fromUnit, EthUnit toUnit)
        {
            throw new NotImplementedException();
        }

        public BigInteger GetEthUnitValue(EthUnit ethUnit)
        {

            switch (ethUnit)
            {
                case EthUnit.wei:
                    return BigInteger.Parse("1");
                case EthUnit.kwei:
                    return BigInteger.Parse("1000");
                case EthUnit.ada:
                    return BigInteger.Parse("1000");
                case EthUnit.femtoether:
                    return BigInteger.Parse("1000");
                case EthUnit.mwei:
                    return BigInteger.Parse("1000000");
                case EthUnit.babbage:
                    return BigInteger.Parse("1000000");
                case EthUnit.picoether:
                    return BigInteger.Parse("1000000");
                case EthUnit.gwei:
                    return BigInteger.Parse("1000000000");
                case EthUnit.shannon:
                    return BigInteger.Parse("1000000000");
                case EthUnit.nanoether:
                    return BigInteger.Parse("1000000000");
                case EthUnit.nano:
                    return BigInteger.Parse("1000000000");
                case EthUnit.szabo:
                    return BigInteger.Parse("1000000000000");
                case EthUnit.microether:
                    return BigInteger.Parse("1000000000000");
                case EthUnit.micro:
                    return BigInteger.Parse("1000000000000");
                case EthUnit.finney:
                    return BigInteger.Parse("1000000000000000");
                case EthUnit.milliether:
                    return BigInteger.Parse("1000000000000000");
                case EthUnit.milli:
                    return BigInteger.Parse("1000000000000000");
                case EthUnit.ether:
                    return BigInteger.Parse("1000000000000000000");
                case EthUnit.kether:
                    return BigInteger.Parse("1000000000000000000000");
                case EthUnit.grand:
                    return BigInteger.Parse("1000000000000000000000");
                case EthUnit.einstein:
                    return BigInteger.Parse("1000000000000000000000");
                case EthUnit.mether:
                    return BigInteger.Parse("1000000000000000000000000");
                case EthUnit.gether:
                    return BigInteger.Parse("1000000000000000000000000000");
                case EthUnit.tether:
                    return BigInteger.Parse("1000000000000000000000000000000");

                  

            }
            throw new NotImplementedException();

        }
    }
}

/*
  /**
 * Takes a number of wei and converts it to any other ether unit.
 *
 * Possible units are:
 *   SI Short   SI Full        Effigy       Other
 * - kwei       femtoether     ada
 * - mwei       picoether      babbage
 * - gwei       nanoether      shannon      nano
 * - --         microether     szabo        micro
 * - --         milliether     finney       milli
 * - ether      --             --
 * - kether                    einstein     grand
 * - mether
 * - gether
 * - tether
 *
 * @method fromWei
 * @param {Number|String} number can be a number, number string or a HEX of a decimal
 * @param {String} unit the unit to convert to, default ether
 * @return {String|Object} When given a BigNumber object it returns one as well, otherwise a number
*/
 //   var fromWei = function(number, unit) {
 //   var returnValue = toBigNumber(number).dividedBy(getValueOfUnit(unit));

   // return isBigNumber(number) ? returnValue : returnValue.toString(10);
//};

/**
 * Takes a number of a unit and converts it to wei.
 *
 * Possible units are:
 *   SI Short   SI Full        Effigy       Other
 * - kwei       femtoether     ada
 * - mwei       picoether      babbage
 * - gwei       nanoether      shannon      nano
 * - --         microether     szabo        micro
 * - --         milliether     finney       milli
 * - ether      --             --
 * - kether                    einstein     grand
 * - mether
 * - gether
 * - tether
 *
 * @method toWei
 * @param {Number|String|BigNumber} number can be a number, number string or a HEX of a decimal
 * @param {String} unit the unit to convert from, default ether
 * @return {String|Object} When given a BigNumber object it returns one as well, otherwise a number
*/
//var toWei = function(number, unit) {
    //var returnValue = toBigNumber(number).times(getValueOfUnit(unit));

  //  return isBigNumber(number) ? returnValue : returnValue.toString(10);
//};
//};*/

