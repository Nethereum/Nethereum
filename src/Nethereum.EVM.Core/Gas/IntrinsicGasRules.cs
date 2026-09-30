using System.Collections.Generic;
using Nethereum.EVM.Gas.Intrinsic;

namespace Nethereum.EVM.Gas
{
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
        public IRecipientGasRule Recipient { get; }

        public IAccessListFloorRule AccessListFloor { get; }

        public bool StateGasActive => Recipient != null;

        public IntrinsicGasRules(
            long txBase,
            long txCreate,
            long txDataZero,
            long txDataNonZero,
            IInitCodeGasRule initCode,
            IAccessListGasRule accessList,
            IBlobGasRule blob,
            ICalldataFloorRule floor,
            IRecipientGasRule recipient = null,
            IAccessListFloorRule accessListFloor = null)
        {
            TxBase = txBase;
            TxCreate = txCreate;
            TxDataZero = txDataZero;
            TxDataNonZero = txDataNonZero;
            InitCode = initCode;
            AccessList = accessList;
            Blob = blob;
            Floor = floor;
            Recipient = recipient;
            AccessListFloor = accessListFloor;
        }

        public long CalculateIntrinsicGas(byte[] data, bool isContractCreation, IList<AccessListEntry> accessList,
            bool isSelfTransfer, bool hasValue)
        {
            long gas = TxBase;

            if (isContractCreation)
            {
                gas += Recipient != null ? Recipient.CalculateRecipientGas(true, false, false) : TxCreate;

                if (InitCode != null)
                    gas += InitCode.CalculateGas(data);
            }
            else if (Recipient != null)
            {
                gas += Recipient.CalculateRecipientGas(false, isSelfTransfer, hasValue);
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

        public long CalculateFloorGasLimit(byte[] data, bool isContractCreation,
            bool isSelfTransfer, bool hasValue, IList<AccessListEntry> accessList)
        {
            if (Floor == null) return 0;

            long anchor = Recipient != null
                ? TxBase + Recipient.CalculateRecipientGas(isContractCreation, isSelfTransfer, hasValue)
                : TxBase;

            long floorGas = anchor + Floor.FloorPerTokenGas(data);
            if (AccessListFloor != null)
                floorGas += AccessListFloor.FloorPerTokenGas(accessList);

            return floorGas;
        }

        public long CalculateMinimumGasLimit(byte[] data, bool isContractCreation, IList<AccessListEntry> accessList,
            bool isSelfTransfer, bool hasValue)
        {
            long intrinsic = CalculateIntrinsicGas(data, isContractCreation, accessList, isSelfTransfer, hasValue);
            long floor = CalculateFloorGasLimit(data, isContractCreation, isSelfTransfer, hasValue, accessList);
            return intrinsic > floor ? intrinsic : floor;
        }

        public IntrinsicGasRules WithInitCode(IInitCodeGasRule initCode) =>
            new IntrinsicGasRules(TxBase, TxCreate, TxDataZero, TxDataNonZero,
                initCode, AccessList, Blob, Floor, Recipient, AccessListFloor);

        public IntrinsicGasRules WithAccessList(IAccessListGasRule accessList) =>
            new IntrinsicGasRules(TxBase, TxCreate, TxDataZero, TxDataNonZero,
                InitCode, accessList, Blob, Floor, Recipient, AccessListFloor);

        public IntrinsicGasRules WithBlob(IBlobGasRule blob) =>
            new IntrinsicGasRules(TxBase, TxCreate, TxDataZero, TxDataNonZero,
                InitCode, AccessList, blob, Floor, Recipient, AccessListFloor);

        public IntrinsicGasRules WithFloor(ICalldataFloorRule floor) =>
            new IntrinsicGasRules(TxBase, TxCreate, TxDataZero, TxDataNonZero,
                InitCode, AccessList, Blob, floor, Recipient, AccessListFloor);

        public IntrinsicGasRules WithAccessListFloor(IAccessListFloorRule accessListFloor) =>
            new IntrinsicGasRules(TxBase, TxCreate, TxDataZero, TxDataNonZero,
                InitCode, AccessList, Blob, Floor, Recipient, accessListFloor);

        public IntrinsicGasRules WithRecipient(IRecipientGasRule recipient) =>
            new IntrinsicGasRules(TxBase, TxCreate, TxDataZero, TxDataNonZero,
                InitCode, AccessList, Blob, Floor, recipient, AccessListFloor);
    }
}
