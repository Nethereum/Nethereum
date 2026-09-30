using System;
using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.EVM.Execution
{
    /// <summary>
    /// EIP-6110 §Block validity: a log becomes a deposit only when its address is
    /// <c>DEPOSIT_CONTRACT_ADDRESS</c> and its first topic is
    /// <c>DEPOSIT_EVENT_SIGNATURE_HASH</c>. <i>"Beginning with the <c>FORK_BLOCK</c>, each deposit
    /// accumulated in the block MUST appear in the EIP-7685 requests list in the order they appear
    /// in the logs."</i>
    /// </summary>
    public static class DepositRequests
    {
        public const string DepositContractAddress = "0x00000000219ab540356cbb839cbe05303d7705fa";

        public static readonly byte[] DepositEventSignatureHash =
            "0x649bbc62d0e31342afea4e5cd82d4049e7e1ee912fc0889aa790803be39038c5".HexToByteArray();

        private const int EventLength = 576;
        private const int HeadWord = 32;

        private static readonly DepositField Pubkey = new DepositField("pubkey", 0, 160, 48);
        private static readonly DepositField WithdrawalCredentials =
            new DepositField("withdrawal credentials", 32, 256, 32);
        private static readonly DepositField Amount = new DepositField("amount", 64, 320, 8);
        private static readonly DepositField Signature = new DepositField("signature", 96, 384, 96);
        private static readonly DepositField Index = new DepositField("index", 128, 512, 8);

        private static readonly DepositField[] Fields =
            { Pubkey, WithdrawalCredentials, Amount, Signature, Index };

        public static bool IsDepositLog(Log log) =>
            log != null
            && log.Address.IsTheSameAddress(DepositContractAddress)
            && FirstTopicIsTheDepositEvent(log);

        public static byte[] CollectRequestData(IEnumerable<Log> logsInBlockOrder)
        {
            var accumulated = new List<byte>();
            if (logsInBlockOrder == null) return accumulated.ToArray();

            foreach (var log in logsInBlockOrder)
            {
                if (!IsDepositLog(log)) continue;
                accumulated.AddRange(ExtractDepositData(log.Data));
            }

            return accumulated.ToArray();
        }

        public static byte[] ExtractDepositData(byte[] data)
        {
            if (data == null || data.Length != EventLength)
                throw new MalformedDepositLogException("Invalid deposit event data length");

            var deposit = new List<byte>();
            foreach (var field in Fields)
            {
                RefuseUnlessHeadDeclares(data, field);
                RefuseUnlessLengthDeclares(data, field);
                deposit.AddRange(Slice(data, field.Offset + HeadWord, field.Size));
            }

            return deposit.ToArray();
        }

        private static void RefuseUnlessHeadDeclares(byte[] data, DepositField field)
        {
            if (ReadWord(data, field.HeadPosition) != field.Offset)
                throw new MalformedDepositLogException($"Invalid {field.Name} offset in deposit log");
        }

        private static void RefuseUnlessLengthDeclares(byte[] data, DepositField field)
        {
            if (ReadWord(data, field.Offset) != field.Size)
                throw new MalformedDepositLogException($"Invalid {field.Name} size in deposit log");
        }

        private static int ReadWord(byte[] data, int position)
        {
            var lastFour = position + HeadWord - 4;
            for (var i = position; i < lastFour; i++)
                if (data[i] != 0)
                    return int.MaxValue;

            if (data[lastFour] > 0x7f) return int.MaxValue;

            return (data[lastFour] << 24)
                 | (data[lastFour + 1] << 16)
                 | (data[lastFour + 2] << 8)
                 | data[lastFour + 3];
        }

        private static byte[] Slice(byte[] data, int start, int length)
        {
            var slice = new byte[length];
            Array.Copy(data, start, slice, 0, length);
            return slice;
        }

        private static bool FirstTopicIsTheDepositEvent(Log log)
        {
            if (log.Topics == null || log.Topics.Count == 0) return false;

            var topic = log.Topics[0];
            if (topic == null || topic.Length != DepositEventSignatureHash.Length) return false;

            for (var i = 0; i < topic.Length; i++)
                if (topic[i] != DepositEventSignatureHash[i])
                    return false;

            return true;
        }

        private sealed class DepositField
        {
            public DepositField(string name, int headPosition, int offset, int size)
            {
                Name = name;
                HeadPosition = headPosition;
                Offset = offset;
                Size = size;
            }

            public string Name { get; }

            public int HeadPosition { get; }

            public int Offset { get; }

            public int Size { get; }
        }
    }
}
