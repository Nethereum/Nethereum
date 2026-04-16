using System.Collections.Generic;
using Nethereum.EVM.Gas.Intrinsic;

namespace Nethereum.EVM.Gas
{
    /// <summary>
    /// Immutable composition object holding the per-fork intrinsic
    /// transaction gas rules. Fixed constants (<see cref="TxBase"/>,
    /// <see cref="TxCreate"/>, <see cref="TxDataZero"/>,
    /// <see cref="TxDataNonZero"/>) are captured as <c>long</c> fields;
    /// fork-variant behaviour is expressed via four nullable rule
    /// slots (<see cref="InitCode"/>, <see cref="AccessList"/>,
    /// <see cref="Blob"/>, <see cref="Floor"/>). A null slot means the
    /// rule is not active at this fork, and callers read the null
    /// directly instead of consulting an EIP-enable flag.
    ///
    /// Fork bundles are built by composition, not class inheritance —
    /// <see cref="IntrinsicGasRuleSets.Prague"/> is literally
    /// <c>Cancun.WithFloor(Eip7623CalldataFloorRule.Instance)</c>, and
    /// each <c>.WithXxx(...)</c> call returns a new bundle while
    /// leaving the original unchanged. Same template as
    /// <c>PrecompileGasCalculators</c>.
    ///
    /// All public method signatures use plain reference types and
    /// primitives — no <c>System.ValueTuple</c>, so the surface
    /// compiles cleanly for the net451/net461 target frameworks that
    /// <c>Nethereum.EVM</c> source-links against.
    /// </summary>
    public sealed class IntrinsicGasRules
    {
        public long TxBase { get; }
        public long TxCreate { get; }
        public long TxDataZero { get; }
        public long TxDataNonZero { get; }

        public IInitCodeGasRule InitCode { get; }
        public IAccessListGasRule AccessList { get; }
        public IBlobGasRule Blob { get; }
        public ICalldataFloorRule Floor { get; }

        public IntrinsicGasRules(
            long txBase,
            long txCreate,
            long txDataZero,
            long txDataNonZero,
            IInitCodeGasRule initCode,
            IAccessListGasRule accessList,
            IBlobGasRule blob,
            ICalldataFloorRule floor)
        {
            TxBase = txBase;
            TxCreate = txCreate;
            TxDataZero = txDataZero;
            TxDataNonZero = txDataNonZero;
            InitCode = initCode;
            AccessList = accessList;
            Blob = blob;
            Floor = floor;
        }

        /// <summary>
        /// Validation-time intrinsic gas cost for a transaction:
        /// <c>TxBase + (isCreate ? TxCreate + InitCode?.CalculateGas(data) : 0)
        /// + Σ data byte cost + AccessList?.CalculateGas(accessList)</c>.
        /// Matches the legacy <c>IntrinsicGasCalculator.CalculateIntrinsicGas</c>;
        /// the null-rule slots collapse to no-ops.
        /// </summary>
        public long CalculateIntrinsicGas(byte[] data, bool isContractCreation, IList<AccessListEntry> accessList)
        {
            long gas = TxBase;

            if (isContractCreation)
            {
                gas += TxCreate;

                if (InitCode != null)
                    gas += InitCode.CalculateGas(data);
            }

            if (data != null && data.Length > 0)
            {
                foreach (var b in data)
                {
                    gas += b == 0 ? TxDataZero : TxDataNonZero;
                }
            }

            if (AccessList != null)
                gas += AccessList.CalculateGas(accessList);

            return gas;
        }

        /// <summary>
        /// The EIP-7623 calldata floor for a transaction, or 0 when
        /// no floor rule is installed (pre-Prague). When
        /// <paramref name="isContractCreation"/> is true the
        /// <see cref="TxCreate"/> adder is included — matching the
        /// validation-time floor. Finalisation callers pass
        /// <c>isContractCreation: false</c> to get the raw
        /// <c>TxBase + FloorPerToken × tokens</c> floor.
        /// </summary>
        public long CalculateFloorGasLimit(byte[] data, bool isContractCreation)
        {
            if (Floor == null) return 0;

            long floor = Floor.CalculateFloor(data);
            if (isContractCreation)
                floor += TxCreate;
            return floor;
        }

        /// <summary>
        /// Returns a new bundle with <see cref="InitCode"/> replaced by
        /// <paramref name="initCode"/> (which may be null to remove the
        /// rule). All other slots are preserved by reference.
        /// </summary>
        public IntrinsicGasRules WithInitCode(IInitCodeGasRule initCode) =>
            new IntrinsicGasRules(TxBase, TxCreate, TxDataZero, TxDataNonZero,
                initCode, AccessList, Blob, Floor);

        /// <summary>
        /// Returns a new bundle with <see cref="AccessList"/> replaced by
        /// <paramref name="accessList"/>.
        /// </summary>
        public IntrinsicGasRules WithAccessList(IAccessListGasRule accessList) =>
            new IntrinsicGasRules(TxBase, TxCreate, TxDataZero, TxDataNonZero,
                InitCode, accessList, Blob, Floor);

        /// <summary>
        /// Returns a new bundle with <see cref="Blob"/> replaced by
        /// <paramref name="blob"/>.
        /// </summary>
        public IntrinsicGasRules WithBlob(IBlobGasRule blob) =>
            new IntrinsicGasRules(TxBase, TxCreate, TxDataZero, TxDataNonZero,
                InitCode, AccessList, blob, Floor);

        /// <summary>
        /// Returns a new bundle with <see cref="Floor"/> replaced by
        /// <paramref name="floor"/>.
        /// </summary>
        public IntrinsicGasRules WithFloor(ICalldataFloorRule floor) =>
            new IntrinsicGasRules(TxBase, TxCreate, TxDataZero, TxDataNonZero,
                InitCode, AccessList, Blob, floor);
    }
}
