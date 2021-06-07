using System.Collections.Generic;
using System.Linq;
using Nethereum.Generators.Core;
using Nethereum.Generators.Model;
using Nethereum.Generators.ProtocolBuffers.ABIToProto.CoreProto;

namespace Nethereum.Generators.ProtocolBuffers.ABIToProto.Models
{
    public class FunctionABIToProtoModel : TypeMessageModel
    {
        private readonly FunctionABI _functionAbi;

        public bool HasInputParameters
        {
            get
            {
                if (_functionAbi.InputParameters == null) return false;
                // ReSharper disable once UseMethodAny.0
                return _functionAbi.InputParameters.Count() > 0;
            }
        }

        public bool HasOutputParameters
        {
            get
            {
                if (_functionAbi.OutputParameters == null) return false;
                // ReSharper disable once UseMethodAny.0
                return _functionAbi.OutputParameters.Count() > 0;
            }
        }

        public string FunctionName => CommonGenerators.GeneratePropertyName(_functionAbi.Name, CodeGenLanguage.Proto);

        public FunctionABIToProtoModel(FunctionABI functionAbi, string @namespace) : base(
            @namespace, functionAbi.Name, "Messages")
        {
            _functionAbi = functionAbi;
            CodeGenLanguage = CodeGenLanguage.Proto;
        }

        public IEnumerable<ParameterABI> GetInputParameters() => _functionAbi.InputParameters.Ordered();

        public IEnumerable<ParameterABI> GetOutputParameters() => _functionAbi.OutputParameters.Ordered();

    }
}
