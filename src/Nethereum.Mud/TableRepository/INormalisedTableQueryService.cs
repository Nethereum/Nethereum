using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.Mud.TableRepository
{
    public interface INormalisedTableQueryService
    {
        Task<List<Dictionary<string, object>>> QueryAsync(string tableName, TablePredicate predicate = null, int pageSize = 100, int page = 0);
        Task<int> CountAsync(string tableName, TablePredicate predicate = null);
        Task<List<string>> GetAvailableTablesAsync(string prefix = "w_");
        Task<List<NormalisedColumnInfo>> GetTableColumnsAsync(string tableName);
    }

    public class NormalisedColumnInfo
    {
        public string Name { get; set; } = "";
        public string DataType { get; set; } = "";
        public int OrdinalPosition { get; set; }
    }
}
