using Nethereum.Mud.EncodingDecoding;
using System;

namespace Nethereum.Mud
{


    public abstract class SystemResource: IResource
    {
        public byte[] ResourceTypeId { get; protected set; }
        public string Namespace { get; protected set; } = String.Empty;
     
        public string Name { get; protected set; }

        public string GetSchemaNameTrimmedForResource()
        {
            return ResourceEncoder.TrimNameAsValidSize(Name);
        }

        private byte[] _resourceIdEncoded;
        public byte[] ResourceIdEncoded
        {
            get
            {
                if (_resourceIdEncoded == null)
                {
                     _resourceIdEncoded = ResourceEncoder.EncodeSystem(Namespace, GetSchemaNameTrimmedForResource());
                }
                return _resourceIdEncoded;
            }
        }

        public SystemResource(string name, string nameSpace = null)
        {
            if (nameSpace != null)
            {
                Namespace = nameSpace;
            }
            else
            {
                Namespace = String.Empty;
            }
            Name = name;
            ResourceTypeId = Resource.RESOURCE_SYSTEM;
        }
    }
}
