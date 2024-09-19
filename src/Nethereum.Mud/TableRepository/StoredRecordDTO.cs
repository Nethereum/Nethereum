using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.Mud.TableRepository
{
    public class StoredRecordDTO
    {
        public string TableId { get; set; }
        public string Key { get; set; }
        public string Key0 { get; set; }
        public string Key1 { get; set; }
        public string Key2 { get; set; }
        public string Key3 { get; set; }
        public string Address { get; set; }
        public string BlockNumber { get; set; }
        public int? LogIndex { get; set; }
        public bool IsDeleted { get; set; }
        public string StaticData { get; set; }
        public string DynamicData { get; set; }
        public string EncodedLengths { get; set; }
    }

}
