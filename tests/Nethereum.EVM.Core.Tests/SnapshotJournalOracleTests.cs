using System;
using System.Collections.Generic;
using System.Text;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Types;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public partial class SnapshotJournalOracleTests
    {
        private static readonly string[] AddressHex =
        {
            "0x0000000000000000000000000000000000000a01",
            "0x0000000000000000000000000000000000000a02",
            "0x0000000000000000000000000000000000000a03",
            "0x0000000000000000000000000000000000000a04",
            "0x0000000000000000000000000000000000000a05",
            "0x0000000000000000000000000000000000000003"
        };

        private static readonly EvmAddress[] Addresses = BuildAddresses();

        private static readonly EvmUInt256[] Keys =
        {
            EvmUInt256.Zero, 1, 2, 3, 4, 5, 6, 255
        };

        private static EvmAddress[] BuildAddresses()
        {
            var result = new EvmAddress[AddressHex.Length];
            for (var i = 0; i < result.Length; i++) result[i] = EvmAddress.FromHex(AddressHex[i]);
            return result;
        }

        public static IEnumerable<object[]> Seeds()
        {
            for (var seed = 1; seed <= 400; seed++) yield return new object[] { seed };
        }

        [Theory]
        [MemberData(nameof(Seeds))]
        public void Given_RandomMutationsAndNestedSnapshots_When_RevertedAtRandomDepths_Then_TheObservableStateEqualsTheCopyTakenAtTheSnapshot(int seed)
        {
            var random = new Random(seed);
            var state = NewState();
            var open = new List<OpenSnapshot>();
            var expectedNextId = 0;

            for (var step = 0; step < 160; step++)
            {
                var pick = random.Next(100);
                if (pick < 68)
                {
                    Mutate(random, state);
                }
                else if (pick < 80)
                {
                    var dump = Dump(state);
                    var held = Hold(state);
                    var id = state.TakeSnapshot();
                    Assert.Equal(expectedNextId++, id);
                    open.Add(new OpenSnapshot(id, dump, held));
                }
                else if (pick < 92)
                {
                    if (open.Count == 0) continue;
                    RevertAndCompare(random, state, open, seed, step);
                }
                else if (pick < 97)
                {
                    if (open.Count == 0) continue;
                    var index = random.Next(open.Count);
                    state.CommitSnapshot(open[index].Id);
                    open.RemoveAt(index);
                }
                else
                {
                    if (open.Count == 0) continue;
                    var index = random.Next(open.Count);
                    state.DiscardSnapshot(open[index].Id);
                    open.RemoveAt(index);
                }
            }

            while (open.Count > 0)
            {
                RevertAndCompare(random, state, open, seed, -1);
                open.RemoveAt(open.Count - 1);
            }
        }

        private static void RevertAndCompare(Random random, ExecutionStateService state, List<OpenSnapshot> open, int seed, int step)
        {
            var index = random.Next(open.Count);
            var target = open[index];
            var survivorsBefore = SurvivorDump(state, target.Held);
            var txTouchedBefore = TxTouched(state);

            state.RevertToSnapshot(target.Id);

            var where = "seed " + seed + " step " + step + " revert to " + target.Id;
            Assert.Equal(target.Dump, Dump(state));
            AssertHeld(state, target.Held, where);
            Assert.Equal(survivorsBefore, SurvivorDump(state, target.Held));
            Assert.Equal(txTouchedBefore, TxTouched(state));
            open.RemoveRange(index + 1, open.Count - index - 1);
        }

        [Fact]
        public void Given_AFinishedStateWhoseJournalWasRecorded_When_ANextStateRevertsItsOwnSnapshot_Then_OnlyTheNextStatesWritesAreUndone()
        {
            var finished = NewState();
            var finishedSnapshot = finished.TakeSnapshot();
            finished.SetNonce(Addresses[0], 41);
            finished.CreditBalance(Addresses[1], 9);
            finished.MarkAddressAsWarm(Addresses[2]);
            finished.CommitSnapshot(finishedSnapshot);
            var finishedDump = Dump(finished);

            var next = NewState();
            var before = Dump(next);
            var snapshot = next.TakeSnapshot();
            next.SetNonce(Addresses[3], 7);
            next.RevertToSnapshot(snapshot);

            Assert.Equal(before, Dump(next));
            Assert.Equal(finishedDump, Dump(finished));
        }

        [Fact]
        public void Given_TwoStatesRecordingAtTheSameTime_When_BothRevert_Then_EachRestoresOnlyItsOwnWrites()
        {
            var first = NewState();
            var second = NewState();
            var firstBefore = Dump(first);
            var secondBefore = Dump(second);
            var firstSnapshot = first.TakeSnapshot();
            var secondSnapshot = second.TakeSnapshot();
            first.SetNonce(Addresses[0], 5);
            second.SetNonce(Addresses[1], 6);
            first.CreditBalance(Addresses[2], 3);
            second.CreditBalance(Addresses[3], 4);

            first.RevertToSnapshot(firstSnapshot);
            second.RevertToSnapshot(secondSnapshot);

            Assert.Equal(firstBefore, Dump(first));
            Assert.Equal(secondBefore, Dump(second));
        }

        [Fact]
        public void Given_AnInnerSnapshotCommitted_When_TheOuterSnapshotIsReverted_Then_TheInnerWritesAreUndoneToo()
        {
            var state = NewState();
            var address = Addresses[0];
            state.CreditBalance(address, 5);
            var before = Dump(state);

            var outer = state.TakeSnapshot();
            state.CreditBalance(address, 7);
            state.SetNonce(address, 9);
            var inner = state.TakeSnapshot();
            state.SaveToStorage(address, Keys[1], Word(0x42));
            state.MarkAddressAsWarm(Addresses[1]);
            state.CommitSnapshot(inner);

            state.RevertToSnapshot(outer);

            Assert.Equal(before, Dump(state));
        }

        [Fact]
        public void Given_AnInnerSnapshotCommitted_When_TheOuterSnapshotIsReverted_Then_TheCommittedIdIsUnknownAfterwards()
        {
            var state = NewState();
            var outer = state.TakeSnapshot();
            var inner = state.TakeSnapshot();
            state.CommitSnapshot(inner);
            state.RevertToSnapshot(outer);

            RevertUnknown(state, inner);
        }

        [Fact]
        public void Given_AnUnknownSnapshotId_When_Reverted_Then_EveryOpenSnapshotIsDroppedAndNothingIsRestored()
        {
            var state = NewState();
            var address = Addresses[0];
            var first = state.TakeSnapshot();
            state.CreditBalance(address, 11);
            var second = state.TakeSnapshot();
            state.SetNonce(address, 3);
            var mutated = Dump(state);

            RevertUnknown(state, 9999);

            Assert.Equal(mutated, Dump(state));
            RevertUnknown(state, second);
            RevertUnknown(state, first);
            Assert.Equal(mutated, Dump(state));
        }

        [Fact]
        public void Given_AnUnknownSnapshotId_When_Committed_Then_NothingHappensAndOpenSnapshotsStayValid()
        {
            var state = NewState();
            var id = state.TakeSnapshot();
            var before = Dump(state);
            state.CreditBalance(Addresses[0], 1);
            var mutated = Dump(state);

            state.CommitSnapshot(9999);
            state.DiscardSnapshot(9999);

            Assert.Equal(mutated, Dump(state));
            state.RevertToSnapshot(id);
            Assert.Equal(before, Dump(state));
        }

        [Fact]
        public void Given_ANegativeSnapshotId_When_RevertedOrCommitted_Then_ItThrows()
        {
            var state = NewState();
            state.TakeSnapshot();

            Assert.Throws<InvalidOperationException>(() => state.RevertToSnapshot(-1));
            Assert.Throws<InvalidOperationException>(() => state.CommitSnapshot(-1));
            Assert.Throws<InvalidOperationException>(() => state.DiscardSnapshot(-1));
        }

        [Fact]
        public void Given_ASnapshotReverted_When_ItIsRevertedAgainAfterNewWrites_Then_ItRestoresTheSameStateAgain()
        {
            var state = NewState();
            var address = Addresses[0];
            state.CreditBalance(address, 5);
            var before = Dump(state);
            var id = state.TakeSnapshot();

            state.CreditBalance(address, 1);
            state.SaveToStorage(address, Keys[2], Word(1));
            state.RevertToSnapshot(id);
            Assert.Equal(before, Dump(state));

            state.DebitBalance(address, 2);
            state.SetNonce(address, 4);
            state.MarkAddressAsWarm(Addresses[3]);
            state.RevertToSnapshot(id);
            Assert.Equal(before, Dump(state));
        }

        [Fact]
        public void Given_AnOuterSnapshotReverted_When_ANewerSnapshotWasOpen_Then_TheNewerIdIsUnknownAfterwards()
        {
            var state = NewState();
            var outer = state.TakeSnapshot();
            var inner = state.TakeSnapshot();
            state.CreditBalance(Addresses[0], 1);

            state.RevertToSnapshot(outer);

            RevertUnknown(state, inner);
        }

        [Fact]
        public void Given_ARipemdTouchInsideARevertedFrame_When_Reverted_Then_TheTxWideTouchSurvivesButTheAccountVanishes()
        {
            var state = NewState();
            var ripemd = Addresses[5];
            var id = state.TakeSnapshot();

            var account = FillBalanceNonceAndCode(state, ripemd);
            account.IsTouched = true;
            state.MarkTxGloballyTouched(ripemd);

            state.RevertToSnapshot(id);

            Assert.False(state.AccountsState.ContainsKey(ripemd));
            Assert.Contains(ripemd, state.TxGloballyTouchedAddresses);
        }

        [Fact]
        public void Given_ATouchOnAnAccountThatExistedBeforeTheSnapshot_When_Reverted_Then_TheTouchIsRolledBack()
        {
            var state = NewState();
            var address = Addresses[1];
            var account = FillBalanceNonceAndCode(state, address);
            var id = state.TakeSnapshot();

            account.IsTouched = true;
            state.RevertToSnapshot(id);

            Assert.False(account.IsTouched);
            Assert.Same(account, state.AccountsState[address]);
        }

        [Fact]
        public void Given_AStorageSlotLoadedInsideARevertedFrame_When_Reverted_Then_TheSlotIsNoLongerCachedButItsOriginalValueIsKept()
        {
            var state = NewState();
            var address = Addresses[2];
            state.CreateOrGetAccountExecutionState(address);
            var id = state.TakeSnapshot();

            FillStorage(state, address, Keys[1]);
            Assert.True(state.AccountsState[address].Storage.ContainsKey(Keys[1]));

            state.RevertToSnapshot(id);

            Assert.False(state.AccountsState[address].Storage.ContainsKey(Keys[1]));
            Assert.True(state.AccountsState[address].OriginalStorageValues.ContainsKey(Keys[1]));
        }

        [Fact]
        public void Given_AnAccountMaterialisedInsideARevertedFrame_When_Reverted_Then_ItIsRemovedFromTheAccountsState()
        {
            var state = NewState();
            var id = state.TakeSnapshot();

            state.CreditBalance(Addresses[4], 3);
            Assert.True(state.AccountsState.ContainsKey(Addresses[4]));

            state.RevertToSnapshot(id);

            Assert.Empty(state.AccountsState);
        }

        [Fact]
        public void Given_ATransientWriteInsideARevertedFrame_When_Reverted_Then_TheTransientStorageIsAsItWasIncludingTheOuterEntries()
        {
            var state = NewState();
            Tstore(state, Addresses[0], Keys[1], Word(1));
            var before = Dump(state);
            var id = state.TakeSnapshot();

            Tstore(state, Addresses[0], Keys[1], Word(2));
            Tstore(state, Addresses[0], Keys[2], Word(3));
            Tstore(state, Addresses[3], Keys[4], Word(4));
            state.RevertToSnapshot(id);

            Assert.Equal(before, Dump(state));
            Assert.False(state.TransientStorage.ContainsKey(Addresses[3]));
        }

        [Fact]
        public void Given_SnapshotsTaken_When_TheFirstTakeHappens_Then_IdsStartAtZeroAndIncrease()
        {
            var state = NewState();

            Assert.Equal(0, state.TakeSnapshot());
            Assert.Equal(1, state.TakeSnapshot());
            state.CommitSnapshot(1);
            Assert.Equal(2, state.TakeSnapshot());
        }

        private static void Mutate(Random random, ExecutionStateService state)
        {
            var address = Addresses[random.Next(Addresses.Length)];
            var key = Keys[random.Next(Keys.Length)];
            switch (random.Next(28))
            {
                case 0: state.CreditBalance(address, RandomNumber(random)); break;
                case 1: state.DebitBalance(address, RandomNumber(random)); break;
                case 2: state.SetNonce(address, RandomNumber(random)); break;
                case 3: state.SaveCode(address, RandomCode(random)); break;
                case 4: state.SaveToStorage(address, key, RandomWord(random, true)); break;
                case 5: FillStorage(state, address, key); break;
                case 6: FillCode(state, address); break;
                case 7: FillNonce(state, address); break;
                case 8: FillBalance(state, address); break;
                case 9: state.MarkAddressAsWarm(address); break;
                case 10: state.CreateOrGetAccountExecutionState(address).MarkStorageKeyAsWarm(key); break;
                case 11: Tstore(state, address, key, RandomWord(random, false)); break;
                case 12: state.MarkSelfDestructed(address); break;
                case 13: state.CreateOrGetAccountExecutionState(address); break;
                case 14: state.CreateOrGetAccountExecutionState(address).IsTouched = random.Next(2) == 0; break;
                case 15: state.PrepareNewContractAccount(address, (ulong)random.Next(3)); break;
                case 16: state.DeleteAccount(address); break;
                case 17: state.ClearAccountPreservingBalance(address); break;
                case 18: state.SetInitialChainBalance(address, RandomNumber(random)); break;
                case 19: state.CreateOrGetAccountExecutionState(address).Nonce = random.Next(4) == 0 ? (EvmUInt256?)null : RandomNumber(random); break;
                case 20: state.CreateOrGetAccountExecutionState(address).Code = RandomCode(random); break;
                case 21: state.CreateOrGetAccountExecutionState(address).Balance.CreditExecutionBalance(RandomNumber(random)); break;
                case 22: state.CreateOrGetAccountExecutionState(address).Balance.DebitExecutionBalance(RandomNumber(random)); break;
                case 23: state.SetPreStateStorage(address, key, RandomWord(random, true)); break;
                case 24: state.CreateOrGetAccountExecutionState(address).IsNewContract = random.Next(2) == 0; break;
                case 25: state.MarkTxGloballyTouched(address); break;
                case 26: SetUntrackedFlags(random, state.CreateOrGetAccountExecutionState(address)); break;
                default: state.CreateOrGetAccountExecutionState(address).Balance.SetExecutionBalance(random.Next(3) == 0 ? (EvmUInt256?)null : RandomNumber(random)); break;
            }
        }

        private static void SetUntrackedFlags(Random random, AccountExecutionState account)
        {
            account.WasInPreState = random.Next(2) == 0;
            account.WasMaterialisedByCallFrame = random.Next(2) == 0;
        }

        private static EvmUInt256 RandomNumber(Random random) => (ulong)random.Next(0, 6);

        private static byte[] Word(byte last)
        {
            var word = new byte[32];
            word[31] = last;
            return word;
        }

        private static byte[] RandomWord(Random random, bool mayBeNull)
        {
            var kind = random.Next(12);
            if (mayBeNull && kind == 0) return null;
            if (kind == 1) return new byte[32];
            if (kind == 2) return new byte[] { (byte)random.Next(256) };
            var word = new byte[32];
            random.NextBytes(word);
            return word;
        }

        private static byte[] RandomCode(Random random)
        {
            var kind = random.Next(8);
            if (kind == 0) return null;
            if (kind == 1) return new byte[0];
            var code = new byte[random.Next(1, 40)];
            random.NextBytes(code);
            return code;
        }

        private static void Tstore(ExecutionStateService state, EvmAddress address, EvmUInt256 key, byte[] value)
        {
            var callContext = new EvmCallContext { From = AddressHex[0], To = address.ToHexLower() };
            var programContext = new ProgramContext(callContext, state);
            var program = new Program(new byte[] { 0x00 }, programContext);
            program.StackPush(value);
            program.StackPush(key);
            new EvmBlockchainCurrentContractContextExecution().TStore(program);
        }

        private sealed class HeldAccount
        {
            public EvmAddress Address;
            public AccountExecutionState Account;
            public Dictionary<EvmUInt256, byte[]> Storage;
        }

        private sealed class OpenSnapshot
        {
            public OpenSnapshot(int id, string dump, List<HeldAccount> held)
            {
                Id = id;
                Dump = dump;
                Held = held;
            }

            public int Id { get; }
            public string Dump { get; }
            public List<HeldAccount> Held { get; }
        }

        private static List<HeldAccount> Hold(ExecutionStateService state)
        {
            var held = new List<HeldAccount>();
            foreach (var kvp in state.AccountsState)
                held.Add(new HeldAccount { Address = kvp.Key, Account = kvp.Value, Storage = kvp.Value.Storage });
            return held;
        }

        private static void AssertHeld(ExecutionStateService state, List<HeldAccount> held, string where)
        {
            foreach (var entry in held)
            {
                Assert.True(state.AccountsState.TryGetValue(entry.Address, out var account), where + " lost account " + entry.Address);
                Assert.True(ReferenceEquals(entry.Account, account), where + " replaced account object " + entry.Address);
                Assert.True(ReferenceEquals(entry.Storage, account.Storage), where + " replaced storage dictionary " + entry.Address);
                Assert.True(ReferenceEquals(account, account.Balance.Owner), where + " balance owner " + entry.Address);
            }
        }

        private static string SurvivorDump(ExecutionStateService state, List<HeldAccount> held)
        {
            var sb = new StringBuilder();
            foreach (var entry in held)
            {
                if (!state.AccountsState.TryGetValue(entry.Address, out var account)) continue;
                sb.Append(entry.Address.ToHexLower()).Append("|reset=").Append(account.IsStorageReset)
                  .Append("|pre=").Append(account.WasInPreState)
                  .Append("|frame=").Append(account.WasMaterialisedByCallFrame).Append("|orig=");
                foreach (var original in account.OriginalStorageValues)
                    sb.Append(original.Key).Append('=').Append(Hex(original.Value)).Append(',');
                sb.Append('\n');
            }
            return sb.ToString();
        }

        private static string TxTouched(ExecutionStateService state)
        {
            var sb = new StringBuilder();
            foreach (var address in state.TxGloballyTouchedAddresses) sb.Append(address.ToHexLower()).Append(',');
            return sb.ToString();
        }

        private static string Dump(ExecutionStateService state)
        {
            var sb = new StringBuilder();
            foreach (var kvp in state.AccountsState)
            {
                var account = kvp.Value;
                sb.Append("account ").Append(kvp.Key.ToHexLower()).Append('|').Append(account.Address)
                  .Append("|exec=").Append(Number(account.Balance.ExecutionBalance))
                  .Append("|initial=").Append(Number(account.Balance.InitialChainBalance))
                  .Append("|nonce=").Append(Number(account.Nonce))
                  .Append("|code=").Append(Hex(account.Code))
                  .Append("|new=").Append(account.IsNewContract)
                  .Append("|touched=").Append(account.IsTouched)
                  .Append("|removed=").Append(account.IsRemoved).Append('\n');
                sb.Append("  storage");
                foreach (var slot in account.Storage) sb.Append(' ').Append(slot.Key).Append('=').Append(Hex(slot.Value));
                sb.Append("\n  warmkeys");
                foreach (var warm in account.WarmStorageKeys) sb.Append(' ').Append(warm);
                sb.Append('\n');
            }

            foreach (var address in Addresses)
            {
                sb.Append("warm ").Append(address.ToHexLower()).Append('=').Append(state.AddressIsWarm(address))
                  .Append(" selfdestructed=").Append(state.HasSelfDestructed(address)).Append('\n');
            }

            foreach (var outer in state.TransientStorage)
            {
                sb.Append("transient ").Append(outer.Key.ToHexLower());
                foreach (var inner in outer.Value) sb.Append(' ').Append(inner.Key).Append('=').Append(Hex(inner.Value));
                sb.Append('\n');
            }
            return sb.ToString();
        }

        private static class DeterministicChain
        {
            public static EvmUInt256 Balance(byte[] address) => (ulong)(1000 + address[19]);

            public static EvmUInt256 Nonce(byte[] address) => (ulong)address[19];

            public static byte[] Code(byte[] address)
            {
                if (address[19] % 2 == 0) return new byte[0];
                var code = new byte[address[19]];
                for (var i = 0; i < code.Length; i++) code[i] = (byte)(i + 1);
                return code;
            }

            public static byte[] Storage(byte[] address, EvmUInt256 position)
            {
                var word = new byte[32];
                var low = (byte)(position.U0 & 0xff);
                if (low % 2 == 1) word[31] = (byte)(low + address[19]);
                return word;
            }
        }

        private static string Number(EvmUInt256? value) => value.HasValue ? Hex(value.Value.ToBigEndian()) : "null";

        private static string Hex(byte[] bytes) => bytes == null ? "null" : "0x" + BitConverter.ToString(bytes).Replace("-", "");
    }
}
