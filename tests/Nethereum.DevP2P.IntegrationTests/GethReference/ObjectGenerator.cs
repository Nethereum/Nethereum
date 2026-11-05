using System;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.DevP2P.IntegrationTests.GethReference
{
    public sealed class ObjectGenerator
    {
        public const int BlobSize = 131072;

        private readonly Random _random;

        public ObjectGenerator(Random random)
        {
            _random = random;
        }

        public byte[] Bytes(int length)
        {
            var bytes = new byte[length];
            _random.NextBytes(bytes);
            return bytes;
        }

        public RlpNode Hash() => RlpNode.Str(Bytes(32));

        public RlpNode Address() => RlpNode.Str(Bytes(20));

        public RlpNode Fixed(int length) => RlpNode.Str(Bytes(length));

        public ulong Number()
        {
            switch (_random.Next(6))
            {
                case 0: return 0;
                case 1: return (ulong)_random.Next(1, 200);
                case 2: return (ulong)_random.Next(200, 70000);
                case 3: return ulong.MaxValue - (ulong)_random.Next(1000);
                case 4: return 1UL << 32 | (ulong)_random.Next(3);
                default: return (ulong)(_random.NextDouble() * 1e15) + 1;
            }
        }

        public RlpNode Int() => RlpNode.Int(Number());

        public RlpNode Wide()
        {
            var length = _random.Next(0, 33);
            var bytes = Bytes(length);
            if (length > 0 && bytes[0] == 0) bytes[0] = 1;
            return RlpNode.Str(bytes);
        }

        public RlpNode Signature()
        {
            var bytes = Bytes(32);
            if (bytes[0] == 0) bytes[0] = 1;
            return RlpNode.Str(bytes);
        }

        public RlpNode Data() => RlpNode.Str(Bytes(_random.Next(0, 5) == 0 ? 0 : _random.Next(1, 200)));

        public RlpNode OptionalTo() => _random.Next(4) == 0 ? RlpNode.Str(Array.Empty<byte>()) : Address();

        public RlpNode AccessList()
        {
            var items = new List<RlpNode>();
            for (var i = _random.Next(0, 4); i > 0; i--)
                items.Add(RlpNode.List(Address(), RlpNode.List(Enumerable.Range(0, _random.Next(0, 4)).Select(_ => Hash()))));
            return RlpNode.List(items);
        }

        public RlpNode YParity() => RlpNode.Int((ulong)_random.Next(0, 2));

        public RlpNode Legacy()
        {
            var v = _random.Next(3) == 0 ? RlpNode.Int((ulong)_random.Next(27, 29)) : RlpNode.Int(35 + 2 * (ulong)_random.Next(1, 100000) + (ulong)_random.Next(0, 2));
            return RlpNode.List(Int(), Int(), Int(), OptionalTo(), Int(), Data(), v, Signature(), Signature());
        }

        public RlpNode Tx2930() => RlpNode.Typed(1, RlpNode.List(Int(), Int(), Int(), Int(), OptionalTo(), Int(), Data(), AccessList(), YParity(), Signature(), Signature()));

        public RlpNode Tx1559() => RlpNode.Typed(2, RlpNode.List(Int(), Int(), Int(), Int(), Int(), OptionalTo(), Int(), Data(), AccessList(), YParity(), Signature(), Signature()));

        public RlpNode BlobTxBody(out int hashes)
        {
            hashes = _random.Next(1, 4);
            return RlpNode.List(Int(), Int(), Int(), Int(), Int(), Address(), Int(), Data(), AccessList(), Int(),
                RlpNode.List(Enumerable.Range(0, hashes).Select(_ => Hash())), YParity(), Signature(), Signature());
        }

        public RlpNode Tx4844() => RlpNode.Typed(3, BlobTxBody(out _));

        public RlpNode Tx4844WithSidecar(int version)
        {
            var body = BlobTxBody(out var hashes);
            var blobs = RlpNode.List(Enumerable.Range(0, hashes).Select(_ => Fixed(BlobSize)));
            var commitments = RlpNode.List(Enumerable.Range(0, hashes).Select(_ => Fixed(48)));
            var proofs = RlpNode.List(Enumerable.Range(0, hashes * (version == 1 ? 128 : 1)).Select(_ => Fixed(48)));
            var items = new List<RlpNode> { body };
            if (version == 1) items.Add(RlpNode.Int(1));
            items.Add(blobs);
            items.Add(commitments);
            items.Add(proofs);
            return RlpNode.Typed(3, RlpNode.List(items));
        }

        public RlpNode Authorisation() =>
            RlpNode.List(Int(), Address(), Int(), YParity(), Signature(), Signature());

        public RlpNode Tx7702() => RlpNode.Typed(4, RlpNode.List(Int(), Int(), Int(), Int(), Int(), Address(), Int(), Data(), AccessList(),
            RlpNode.List(Enumerable.Range(0, _random.Next(1, 4)).Select(_ => Authorisation())), YParity(), Signature(), Signature()));

        public RlpNode AnyTransaction()
        {
            switch (_random.Next(6))
            {
                case 0: return Legacy();
                case 1: return Tx2930();
                case 2: return Tx1559();
                case 3: return Tx4844();
                case 4: return Tx7702();
                default: return Legacy();
            }
        }

        public RlpNode Log() => RlpNode.List(Address(), RlpNode.List(Enumerable.Range(0, _random.Next(0, 5)).Select(_ => Hash())), Data());

        public RlpNode Logs() => RlpNode.List(Enumerable.Range(0, _random.Next(0, 4)).Select(_ => Log()));

        public RlpNode Status()
        {
            switch (_random.Next(3))
            {
                case 0: return RlpNode.Str(new byte[] { 1 });
                case 1: return RlpNode.Str(Array.Empty<byte>());
                default: return Hash();
            }
        }

        public RlpNode ReceiptBody() => RlpNode.List(Status(), Int(), Fixed(256), Logs());

        public RlpNode Receipt(int type) => type == 0 ? ReceiptBody() : RlpNode.Typed((byte)type, ReceiptBody());

        public RlpNode ReceiptEth69() => RlpNode.List(RlpNode.Int((ulong)_random.Next(0, 5)), Status(), Int(), Logs());

        public RlpNode Withdrawal() => RlpNode.List(Int(), Int(), Address(), Int());

        public RlpNode Header(int fields)
        {
            var items = new List<RlpNode>
            {
                Hash(), Hash(), Address(), Hash(), Hash(), Hash(), Fixed(256), Int(), Int(), Int(), Int(), Int(), Data(), Hash(), Fixed(8)
            };
            if (fields > 15) items.Add(Int());
            if (fields > 16) items.Add(Hash());
            if (fields > 17) items.Add(Int());
            if (fields > 18) items.Add(Int());
            if (fields > 19) items.Add(Hash());
            if (fields > 20) items.Add(Hash());
            if (fields > 21) items.Add(Hash());
            if (fields > 22) items.Add(Int());
            return RlpNode.List(items);
        }

        public RlpNode AnyHeader()
        {
            var shapes = new[] { 15, 16, 17, 20, 21, 23 };
            return Header(shapes[_random.Next(shapes.Length)]);
        }

        public RlpNode Body(bool withWithdrawals)
        {
            var items = new List<RlpNode>
            {
                RlpNode.List(Enumerable.Range(0, _random.Next(0, 4)).Select(_ => AnyTransaction())),
                RlpNode.List(Enumerable.Range(0, _random.Next(0, 2)).Select(_ => Header(15)))
            };
            if (withWithdrawals) items.Add(RlpNode.List(Enumerable.Range(0, _random.Next(0, 4)).Select(_ => Withdrawal())));
            return RlpNode.List(items);
        }

        public RlpNode Block(bool withWithdrawals)
        {
            var body = Body(withWithdrawals);
            return RlpNode.List(new[] { withWithdrawals ? Header(17) : Header(15) }.Concat(body.Items));
        }

        public RlpNode AccountAccess()
        {
            var storageChanges = RlpNode.List(Enumerable.Range(0, _random.Next(0, 3)).Select(_ =>
                RlpNode.List(Wide(), RlpNode.List(Enumerable.Range(0, _random.Next(0, 3)).Select(__ => RlpNode.List(RlpNode.Int((ulong)_random.Next(0, 1000)), Wide()))))));
            var reads = RlpNode.List(Enumerable.Range(0, _random.Next(0, 3)).Select(_ => Wide()));
            var balances = RlpNode.List(Enumerable.Range(0, _random.Next(0, 3)).Select(_ => RlpNode.List(RlpNode.Int((ulong)_random.Next(0, 1000)), Wide())));
            var nonces = RlpNode.List(Enumerable.Range(0, _random.Next(0, 3)).Select(_ => RlpNode.List(RlpNode.Int((ulong)_random.Next(0, 1000)), Int())));
            var codes = RlpNode.List(Enumerable.Range(0, _random.Next(0, 3)).Select(_ => RlpNode.List(RlpNode.Int((ulong)_random.Next(0, 1000)), RlpNode.Str(Bytes(_random.Next(0, 40))))));
            return RlpNode.List(Address(), storageChanges, reads, balances, nonces, codes);
        }

        public RlpNode BlockAccessList() => RlpNode.List(Enumerable.Range(0, _random.Next(0, 4)).Select(_ => AccountAccess()));

        public RlpNode Id() => Int();

        public RlpNode Hashes(int max) => RlpNode.List(Enumerable.Range(0, _random.Next(0, max)).Select(_ => Hash()));

        public RlpNode ForkId() => RlpNode.List(Fixed(4), Int());

        public RlpNode HashOrNumber() => _random.Next(2) == 0 ? Hash() : Int();

        public RlpNode ByteStrings(int max, int maxLength) =>
            RlpNode.List(Enumerable.Range(0, _random.Next(0, max)).Select(_ => RlpNode.Str(Bytes(_random.Next(0, maxLength)))));

        public RlpNode SlimAccount() => RlpNode.List(Int(), Wide(),
            _random.Next(2) == 0 ? RlpNode.Str(Array.Empty<byte>()) : Fixed(32),
            _random.Next(2) == 0 ? RlpNode.Str(Array.Empty<byte>()) : Fixed(32));

        public RlpNode TransactionsMessage(Func<RlpNode> transaction) =>
            RlpNode.List(Enumerable.Range(0, _random.Next(0, 5)).Select(_ => transaction()));

        public RlpNode PooledTransactionsMessage(Func<RlpNode> transaction) => RlpNode.List(Id(), TransactionsMessage(transaction));

        public RlpNode BlockHeadersMessage(Func<RlpNode> header) =>
            RlpNode.List(Id(), RlpNode.List(Enumerable.Range(0, _random.Next(0, 5)).Select(_ => header())));

        public RlpNode BlockBodiesMessage(Func<RlpNode> body) =>
            RlpNode.List(Id(), RlpNode.List(Enumerable.Range(0, _random.Next(0, 4)).Select(_ => body())));

        public RlpNode ReceiptsEth69Message()
        {
            return RlpNode.List(Id(), ReceiptBlocks());
        }

        public RlpNode ReceiptsEth70Message()
        {
            return RlpNode.List(Id(), RlpNode.Int((ulong)_random.Next(0, 2)), ReceiptBlocks());
        }

        private RlpNode ReceiptBlocks() =>
            RlpNode.List(Enumerable.Range(0, _random.Next(0, 4)).Select(_ =>
                RlpNode.List(Enumerable.Range(0, _random.Next(0, 4)).Select(__ => ReceiptEth69()))));

        public RlpNode BlockAccessListsMessage() =>
            RlpNode.List(Id(), RlpNode.List(Enumerable.Range(0, _random.Next(0, 4)).Select(_ =>
                _random.Next(3) == 0 ? RlpNode.Str(Array.Empty<byte>()) : BlockAccessList())));

        public RlpNode NewPooledTransactionHashes()
        {
            var count = _random.Next(0, 5);
            return RlpNode.List(RlpNode.Str(Bytes(count)),
                RlpNode.List(Enumerable.Range(0, count).Select(_ => RlpNode.Int((ulong)_random.Next(0, 1 << 20)))),
                RlpNode.List(Enumerable.Range(0, count).Select(_ => Hash())));
        }

        public RlpNode BlockRangeUpdate()
        {
            var earliest = (ulong)_random.Next(0, 1000);
            return RlpNode.List(RlpNode.Int(earliest), RlpNode.Int(earliest + (ulong)_random.Next(0, 1000)), LatestHash());
        }

        private RlpNode LatestHash() => _random.Next(5) == 0 ? RlpNode.Str(new byte[32]) : Hash();

        public RlpNode Status69()
        {
            var earliest = (ulong)_random.Next(0, 1000);
            return RlpNode.List(RlpNode.Int(69), Int(), Hash(), ForkId(), RlpNode.Int(earliest), RlpNode.Int(earliest + (ulong)_random.Next(0, 1000)), LatestHash());
        }

        public RlpNode GetBlockHeaders() => RlpNode.List(Id(), RlpNode.List(HashOrNumber(), Int(), Int(), RlpNode.Int((ulong)_random.Next(0, 2))));

        public RlpNode IdAndHashes() => RlpNode.List(Id(), Hashes(5));

        public RlpNode GetReceipts70() => RlpNode.List(Id(), Int(), Hashes(5));

        public RlpNode GetAccountRange() => RlpNode.List(Id(), Hash(), Hash(), Hash(), Int());

        public RlpNode AccountRange() => RlpNode.List(Id(),
            RlpNode.List(Enumerable.Range(0, _random.Next(0, 4)).Select(_ => RlpNode.List(Hash(), SlimAccount()))), ByteStrings(4, 60));

        public RlpNode GetStorageRanges() => RlpNode.List(Id(), Hash(), Hashes(4), Wide(), Wide(), Int());

        public RlpNode StorageRanges() => RlpNode.List(Id(),
            RlpNode.List(Enumerable.Range(0, _random.Next(0, 3)).Select(_ =>
                RlpNode.List(Enumerable.Range(0, _random.Next(0, 3)).Select(__ => RlpNode.List(Hash(), Data()))))), ByteStrings(4, 60));

        public RlpNode GetByteCodes() => RlpNode.List(Id(), Hashes(5), Int());

        public RlpNode ByteCodes() => RlpNode.List(Id(), ByteStrings(5, 80));

        public RlpNode GetTrieNodes() => RlpNode.List(Id(), Hash(),
            RlpNode.List(Enumerable.Range(0, _random.Next(0, 4)).Select(_ => RlpNode.List(Enumerable.Range(0, _random.Next(1, 4)).Select(__ => RlpNode.Str(Bytes(_random.Next(0, 33))))))), Int());

        public RlpNode TrieNodes() => RlpNode.List(Id(), ByteStrings(5, 80));

        public RlpNode GetAccessLists() => RlpNode.List(Id(), Hashes(5), Int());

        public RlpNode AccessLists() => BlockAccessListsMessage();
    }
}
