using System;
using System.Collections.Generic;
using System.Text;
using Nethereum.RLP;

namespace Nethereum.Model
{
    public class AccountEncoder
    {
        public static AccountEncoder Current { get; } = new AccountEncoder();

        public byte[] Encode(Account account)
        {
            return RLP.RLP.EncodeDataItemsAsElementOrListAndCombineAsList(new byte[][] {
                account.Nonce.ToBytesForRLPEncoding(),
                account.Balance.ToBytesForRLPEncoding(),
                account.StateRoot,
                account.CodeHash });
        }


        public Account Decode(byte[] rawdata)
        {
            var decodedList = RLP.RLP.Decode(rawdata);
            var decodedElements = (RLPCollection) decodedList;
            var account = new Account();
            account.Nonce = decodedElements[0].RLPData.ToBigIntegerFromRLPDecoded();
            account.Balance = decodedElements[1].RLPData.ToBigIntegerFromRLPDecoded();
            account.StateRoot = decodedElements[2].RLPData;
            account.CodeHash = decodedElements[3].RLPData;
            return account;
        }
    }
}
