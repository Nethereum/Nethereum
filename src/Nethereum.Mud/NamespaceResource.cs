using Nethereum.Mud.EncodingDecoding;

namespace Nethereum.Mud
{
    public abstract class NamespaceResource : IResource
    {       
        public byte[] ResourceTypeId { get; protected set; }
        public string Namespace { get; protected set; }
        public string Name { get; } = null;

        public string GetNamespaceNameTrimmedForResource()
        {
            if(Namespace == null)
            {
                return string.Empty;
            }
            return ResourceEncoder.TrimNamespaceNameAsValidSize(Namespace);
        }

        private byte[] _resourceIdEncoded;
        public byte[] ResourceIdEncoded
        {
            get
            {
                if (_resourceIdEncoded == null)
                {
                    _resourceIdEncoded = ResourceEncoder.EncodeNamesapce(GetNamespaceNameTrimmedForResource());
                }
                return _resourceIdEncoded;
            }
        }

        public NamespaceResource(string nameSpace)
        {
            Namespace = nameSpace;
            ResourceTypeId = Resource.RESOURCE_NAMESPACE;
        }
    }
}
