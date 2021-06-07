using System.Collections.Generic;
using System.Linq;
using Nethereum.Generators.Core;
using Nethereum.Generators.Model;
using Nethereum.Generators.ProtocolBuffers.ABIToProto.CoreProto;

namespace Nethereum.Generators.ProtocolBuffers.ABIToProto.Models
{
    public class EventABIToProtoModel : TypeMessageModel
    {
        private readonly EventABI _eventABI;

        public bool HasInputParameters
        {
            get
            {
                if (_eventABI.InputParameters == null) return false;
                // ReSharper disable once UseMethodAny.0
                return _eventABI.InputParameters.Count() > 0;
            }
        }

        public string EventName => CommonGenerators.GeneratePropertyName(_eventABI.Name, CodeGenLanguage.Proto);

        public EventABIToProtoModel(EventABI eventABI, string @namespace) : base(
            @namespace, eventABI.Name, "EventMessage")
        {
            _eventABI = eventABI;
            CodeGenLanguage = CodeGenLanguage.Proto;
        }

        public IEnumerable<ParameterABI> GetInputParameters() => _eventABI.InputParameters.Ordered();
    }
}
