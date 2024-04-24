using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding.Attributes;   

namespace Nethereum.Mud.Contracts.StoreEvents
{
    public partial class StoreDeleteRecordEventDTO : StoreDeleteRecordEventDTOBase { }

    [Event("Store_DeleteRecord")]
    public class StoreDeleteRecordEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "tableId", 1, true)]
        public virtual byte[] TableId { get; set; }
        [Parameter("bytes32[]", "keyTuple", 2, false)]
        public virtual List<byte[]> KeyTuple { get; set; }
    }

    public partial class StoreSpliceStaticDataEventDTO : StoreSpliceStaticDataEventDTOBase { }

    [Event("Store_SpliceStaticData")]
    public class StoreSpliceStaticDataEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "tableId", 1, true)]
        public virtual byte[] TableId { get; set; }
        [Parameter("bytes32[]", "keyTuple", 2, false)]
        public virtual List<byte[]> KeyTuple { get; set; }
        [Parameter("uint48", "start", 3, false)]
        public virtual ulong Start { get; set; }
        [Parameter("bytes", "data", 4, false)]
        public virtual byte[] Data { get; set; }
    }

    public partial class StoreSetRecordEventDTO : StoreSetRecordEventDTOBase { }

    [Event("Store_SetRecord")]
    public class StoreSetRecordEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "tableId", 1, true)]
        public virtual byte[] TableId { get; set; }
        [Parameter("bytes32[]", "keyTuple", 2, false)]
        public virtual List<byte[]> KeyTuple { get; set; }
        [Parameter("bytes", "staticData", 3, false)]
        public virtual byte[] StaticData { get; set; }
        [Parameter("bytes32", "encodedLengths", 4, false)]
        public virtual byte[] EncodedLengths { get; set; }
        [Parameter("bytes", "dynamicData", 5, false)]
        public virtual byte[] DynamicData { get; set; }
    }

    
    public partial class StoreSpliceDynamicDataEventDTO : StoreSpliceDynamicDataEventDTOBase { }
    [Event("Store_SpliceDynamicData")]
    public class StoreSpliceDynamicDataEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "tableId", 1, true)]
        public virtual byte[] TableId { get; set; }
        [Parameter("bytes32[]", "keyTuple", 2, false)]
        public virtual List<byte[]> KeyTuple { get; set; }
        [Parameter("uint8", "dynamicFieldIndex", 3, false)]
        public virtual byte DynamicFieldIndex { get; set; }
        [Parameter("uint48", "start", 4, false)]
        public virtual ulong Start { get; set; }
        [Parameter("uint40", "deleteCount", 5, false)]
        public virtual ulong DeleteCount { get; set; }
        [Parameter("bytes32", "encodedLengths", 6, false)]
        public virtual byte[] EncodedLengths { get; set; }
        [Parameter("bytes", "data", 7, false)]
        public virtual byte[] Data { get; set; }
    }


}
