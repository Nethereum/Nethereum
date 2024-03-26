using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.ABI.Model;
using Nethereum.Contracts.ContractHandlers;

namespace Nethereum.Contracts
{
    public abstract class ContractServiceBase : IContractService
    {
        public ContractHandler ContractHandler { get; protected set; }

        public string ContractAddress { get { return ContractHandler.ContractAddress; } }

        public abstract List<Type> GetAllFunctionTypes();


        public List<FunctionABI> GetAllFunctionABIs()
        {
            return GetAllFunctionTypes().Select(x => ABITypedRegistry.GetFunctionABI(x)).ToList();
        }

        public string[] GetAllFunctionSignatures()
        {
            return GetAllFunctionABIs().Select(x => x.Sha3Signature).ToArray();
        }

        public abstract List<Type> GetAllEventTypes();


        public List<EventABI> GetAllEventABIs()
        {
            return GetAllEventTypes().Select(x => ABITypedRegistry.GetEvent(x)).ToList();
        }

        public string[] GetAllEventsSignatures()
        {
            return GetAllEventABIs().Select(x => x.Sha3Signature).ToArray();
        }

        public List<ErrorABI> GetAllErrorABIs()
        {
            return GetAllErrorTypes().Select(x => ABITypedRegistry.GetError(x)).ToList();
        }

        public abstract List<Type> GetAllErrorTypes();


        public string[] GetAllErrorsSignatures()
        {
            return GetAllErrorABIs().Select(x => x.Sha3Signature).ToArray();
        }
    }
}