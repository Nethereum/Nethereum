using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Linq;


namespace Nethereum.Uniswap.UniversalRouter.Commands
{
    public abstract class UniversalRouterCommand
    {
        public abstract byte CommandType { get; set; }

        public virtual byte[] GetInputData()
        {
           return new ParametersEncoder().EncodeParametersFromTypeAttributes(GetType(), this);
        }

        public virtual void DecodeInputData(byte[] data)
        {
            var properties = PropertiesExtractor.GetPropertiesWithParameterAttribute(GetType());
            new ParameterDecoder().DecodeAttributes(data, this, properties.ToArray());
        }

        public virtual byte GetFullCommandType()
        {
            return CommandType;
        }
    }
    
}
