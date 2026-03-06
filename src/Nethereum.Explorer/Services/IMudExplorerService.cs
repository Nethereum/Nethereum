using Nethereum.Mud;

namespace Nethereum.Explorer.Services;

public interface IMudExplorerService
{
    bool IsAvailable { get; }
    Task<bool> HasDataAsync();
    Task<List<string>> GetWorldAddressesAsync();
    Task<List<string>> GetTableIdsForWorldAsync(string worldAddress);
    Task<List<MudRecordView>> GetRecordsAsync(string worldAddress, string? tableId, int page, int pageSize);
    Task<int> GetRecordCountAsync(string worldAddress, string? tableId);
    Task<List<MudTableInfo>> GetTablesForWorldAsync(string worldAddress);
    Task<List<MudTableInfo>> GetTablesForWorldAsync(string worldAddress, string rpcUrl);
    Task<List<MudDecodedRecordView>> GetDecodedRecordsAsync(string worldAddress, string tableId, int page, int pageSize);
    Task<List<MudDecodedRecordView>> GetDecodedRecordsAsync(string worldAddress, string tableId, string rpcUrl, int page, int pageSize);
    Task<TableSchema?> GetTableSchemaAsync(string worldAddress, string tableId);
    Task<TableSchema?> GetTableSchemaAsync(string worldAddress, string tableId, string rpcUrl);
    Task<List<string>> GetNormalisedTablesAsync(string worldAddress);
    Task<bool> HasNormalisedTableAsync(string worldAddress, string tableName);
    Task<List<MudNormalisedRecordView>> GetNormalisedRecordsAsync(string tableName, int page, int pageSize);
    Task<int> GetNormalisedRecordCountAsync(string tableName);
    Task<List<MudNormalisedRecordView>> QueryNormalisedRecordsAsync(
        string tableName, List<MudQueryCondition> conditions, string? orderByField, bool orderByDesc, int page, int pageSize);
    Task<int> CountNormalisedRecordsAsync(string tableName, List<MudQueryCondition> conditions);
    Task<List<MudColumnInfo>> GetNormalisedTableColumnsAsync(string tableName);
}

public class MudRecordView
{
    public string WorldAddress { get; set; } = "";
    public string TableId { get; set; } = "";
    public string Key { get; set; } = "";
    public string StaticData { get; set; } = "";
    public string DynamicData { get; set; } = "";
    public string BlockNumber { get; set; } = "";
    public int LogIndex { get; set; }
    public bool IsDeleted { get; set; }
}

public class MudTableInfo
{
    public string TableId { get; set; } = "";
    public string Namespace { get; set; } = "";
    public string Name { get; set; } = "";
    public string ResourceType { get; set; } = "";
    public int RecordCount { get; set; }
    public List<MudFieldInfo> KeyFields { get; set; } = new();
    public List<MudFieldInfo> ValueFields { get; set; } = new();
    public bool HasNormalisedTable { get; set; }
    public string NormalisedTableName { get; set; } = "";
}

public class MudFieldInfo
{
    public string Name { get; set; } = "";
    public string AbiType { get; set; } = "";
    public int Order { get; set; }
    public bool IsKey { get; set; }
}

public class MudDecodedRecordView
{
    public string WorldAddress { get; set; } = "";
    public string TableId { get; set; } = "";
    public string TableName { get; set; } = "";
    public List<MudFieldValueView> Fields { get; set; } = new();
    public string BlockNumber { get; set; } = "";
    public int LogIndex { get; set; }
    public bool IsDeleted { get; set; }
}

public class MudFieldValueView
{
    public string Name { get; set; } = "";
    public string AbiType { get; set; } = "";
    public string Value { get; set; } = "";
    public bool IsKey { get; set; }
}

public class MudNormalisedRecordView
{
    public Dictionary<string, string> Fields { get; set; } = new();
}

public class MudQueryCondition
{
    public string FieldName { get; set; } = "";
    public string Operator { get; set; } = "=";
    public string Value { get; set; } = "";
    public string DataType { get; set; } = "";
}

public class MudColumnInfo
{
    public string Name { get; set; } = "";
    public string DataType { get; set; } = "";
}
