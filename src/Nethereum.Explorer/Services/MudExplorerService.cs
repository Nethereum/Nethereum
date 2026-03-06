using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Mud;
using Nethereum.Mud.Contracts.Store;
using Nethereum.Mud.Contracts.Store.Tables;
using Nethereum.Mud.EncodingDecoding;
using Nethereum.Mud.Repositories.EntityFramework;
using Nethereum.Mud.TableRepository;
using Nethereum.Util;

namespace Nethereum.Explorer.Services;

public class MudExplorerService : IMudExplorerService
{
    private readonly IMudStoreRecordsDbSets? _dbSets;
    private readonly DbContext? _context;
    private readonly IServiceProvider _serviceProvider;
    private readonly ExplorerWeb3Factory _web3Factory;
    private readonly ILogger<MudExplorerService> _logger;
    private readonly ConcurrentDictionary<string, TableSchema> _schemaCache = new();

    public MudExplorerService(IServiceProvider serviceProvider, ExplorerWeb3Factory web3Factory, ILogger<MudExplorerService> logger)
    {
        _serviceProvider = serviceProvider;
        _dbSets = serviceProvider.GetService<IMudStoreRecordsDbSets>();
        _context = _dbSets as DbContext;
        _web3Factory = web3Factory;
        _logger = logger;
    }

    private INormalisedTableQueryService? GetNormalisedQuery() => _serviceProvider.GetService<INormalisedTableQueryService>();

    public bool IsAvailable => _dbSets != null;

    public async Task<bool> HasDataAsync()
    {
        if (_dbSets == null) return false;
        try
        {
            return await _dbSets.StoredRecords.AsNoTracking().AnyAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check MUD store data availability");
            return false;
        }
    }

    public async Task<List<string>> GetWorldAddressesAsync()
    {
        if (_dbSets == null) return new List<string>();

        var addresses = await _dbSets.StoredRecords
            .AsNoTracking()
            .Select(r => r.AddressBytes)
            .Distinct()
            .Take(100)
            .ToListAsync();

        return addresses
            .Where(a => a != null)
            .Select(a => a!.ToHex(prefix: true))
            .ToList();
    }

    public async Task<List<string>> GetTableIdsForWorldAsync(string worldAddress)
    {
        if (_dbSets == null) return new List<string>();

        var addressBytes = worldAddress.HexToByteArray();

        var tableIds = await _dbSets.StoredRecords
            .AsNoTracking()
            .Where(r => r.AddressBytes == addressBytes)
            .Select(r => r.TableIdBytes)
            .Distinct()
            .Take(100)
            .ToListAsync();

        return tableIds
            .Where(t => t != null)
            .Select(t => t!.ToHex(prefix: true))
            .ToList();
    }

    public async Task<List<MudRecordView>> GetRecordsAsync(string worldAddress, string? tableId, int page, int pageSize)
    {
        if (_dbSets == null) return new List<MudRecordView>();
        if (page < 1) page = 1;
        pageSize = ExplorerConstants.ClampPageSize(pageSize);

        var addressBytes = worldAddress.HexToByteArray();

        var query = _dbSets.StoredRecords
            .AsNoTracking()
            .Where(r => r.AddressBytes == addressBytes);

        if (!string.IsNullOrEmpty(tableId))
        {
            var tableIdBytes = tableId.HexToByteArray();
            query = query.Where(r => r.TableIdBytes == tableIdBytes);
        }

        var records = await query
            .OrderByDescending(r => r.BlockNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return records.Select(r => new MudRecordView
        {
            WorldAddress = r.AddressBytes?.ToHex(prefix: true) ?? "",
            TableId = r.TableIdBytes?.ToHex(prefix: true) ?? "",
            Key = r.KeyBytes?.ToHex(prefix: true) ?? "",
            StaticData = r.StaticData?.ToHex(prefix: true) ?? "",
            DynamicData = r.DynamicData?.ToHex(prefix: true) ?? "",
            BlockNumber = r.BlockNumber.ToString(),
            LogIndex = r.LogIndex ?? 0,
            IsDeleted = r.IsDeleted
        }).ToList();
    }

    public async Task<int> GetRecordCountAsync(string worldAddress, string? tableId)
    {
        if (_dbSets == null) return 0;

        var addressBytes = worldAddress.HexToByteArray();

        var query = _dbSets.StoredRecords
            .AsNoTracking()
            .Where(r => r.AddressBytes == addressBytes && !r.IsDeleted);

        if (!string.IsNullOrEmpty(tableId))
        {
            var tableIdBytes = tableId.HexToByteArray();
            query = query.Where(r => r.TableIdBytes == tableIdBytes);
        }

        return await query.CountAsync();
    }

    public async Task<List<MudTableInfo>> GetTablesForWorldAsync(string worldAddress)
    {
        var rpcUrl = _web3Factory.GetRpcUrl();
        if (string.IsNullOrEmpty(rpcUrl)) return new List<MudTableInfo>();
        return await GetTablesForWorldAsync(worldAddress, rpcUrl);
    }

    public async Task<List<MudDecodedRecordView>> GetDecodedRecordsAsync(
        string worldAddress, string tableId, int page, int pageSize)
    {
        var rpcUrl = _web3Factory.GetRpcUrl();
        if (string.IsNullOrEmpty(rpcUrl)) return new List<MudDecodedRecordView>();
        return await GetDecodedRecordsAsync(worldAddress, tableId, rpcUrl, page, pageSize);
    }

    public async Task<TableSchema?> GetTableSchemaAsync(string worldAddress, string tableId)
    {
        var rpcUrl = _web3Factory.GetRpcUrl();
        if (string.IsNullOrEmpty(rpcUrl)) return null;
        return await GetOrFetchSchemaAsync(worldAddress, tableId, rpcUrl);
    }

    public async Task<List<MudTableInfo>> GetTablesForWorldAsync(string worldAddress, string rpcUrl)
    {
        if (_dbSets == null) return new List<MudTableInfo>();

        var addressBytes = worldAddress.HexToByteArray();

        var tableGroups = await _dbSets.StoredRecords
            .AsNoTracking()
            .Where(r => r.AddressBytes == addressBytes && !r.IsDeleted)
            .GroupBy(r => r.TableIdBytes)
            .Select(g => new { TableIdBytes = g.Key, Count = g.Count() })
            .Take(100)
            .ToListAsync();

        var result = new List<MudTableInfo>();

        foreach (var group in tableGroups)
        {
            if (group.TableIdBytes == null) continue;

            var resource = ResourceEncoder.Decode(group.TableIdBytes);
            var resourceType = GetResourceTypeName(resource);

            var tableInfo = new MudTableInfo
            {
                TableId = group.TableIdBytes.ToHex(prefix: true),
                Namespace = resource.Namespace,
                Name = resource.Name,
                ResourceType = resourceType,
                RecordCount = group.Count
            };

            try
            {
                var tableIdHex = group.TableIdBytes.ToHex(prefix: true);
                var schema = await GetOrFetchSchemaAsync(worldAddress, tableIdHex, rpcUrl);
                if (schema != null)
                {
                    tableInfo.KeyFields = schema.SchemaKeys
                        .OrderBy(f => f.Order)
                        .Select(f => new MudFieldInfo { Name = f.Name, AbiType = f.Type, Order = f.Order, IsKey = true })
                        .ToList();
                    tableInfo.ValueFields = schema.SchemaValues
                        .OrderBy(f => f.Order)
                        .Select(f => new MudFieldInfo { Name = f.Name, AbiType = f.Type, Order = f.Order, IsKey = false })
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not fetch schema for table {TableId}", tableInfo.TableId);
            }

            result.Add(tableInfo);
        }

        var normalisedTables = await GetNormalisedTablesAsync(worldAddress);
        if (normalisedTables.Count > 0)
        {
            var prefix = GetNormalisedPrefix(worldAddress);
            foreach (var tableInfo in result)
            {
                var tablePart = string.IsNullOrEmpty(tableInfo.Namespace)
                    ? tableInfo.Name.ToLowerInvariant()
                    : $"{tableInfo.Namespace}_{tableInfo.Name}".ToLowerInvariant();
                var normalisedName = prefix + tablePart;
                if (normalisedTables.Contains(normalisedName))
                {
                    tableInfo.HasNormalisedTable = true;
                    tableInfo.NormalisedTableName = normalisedName;
                }
            }
        }

        return result.OrderBy(t => t.Namespace).ThenBy(t => t.Name).ToList();
    }

    public async Task<List<MudDecodedRecordView>> GetDecodedRecordsAsync(
        string worldAddress, string tableId, string rpcUrl, int page, int pageSize)
    {
        if (_dbSets == null) return new List<MudDecodedRecordView>();
        if (page < 1) page = 1;
        pageSize = ExplorerConstants.ClampPageSize(pageSize);

        var addressBytes = worldAddress.HexToByteArray();
        var tableIdBytes = tableId.HexToByteArray();

        var records = await _dbSets.StoredRecords
            .AsNoTracking()
            .Where(r => r.AddressBytes == addressBytes && r.TableIdBytes == tableIdBytes)
            .OrderByDescending(r => r.BlockNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var schema = await GetOrFetchSchemaAsync(worldAddress, tableId, rpcUrl);
        var resource = ResourceEncoder.Decode(tableIdBytes);

        var result = new List<MudDecodedRecordView>();

        foreach (var record in records)
        {
            var view = new MudDecodedRecordView
            {
                WorldAddress = worldAddress,
                TableId = tableId,
                TableName = string.IsNullOrEmpty(resource.Namespace)
                    ? resource.Name
                    : $"{resource.Namespace}:{resource.Name}",
                BlockNumber = record.BlockNumber?.ToString() ?? "",
                LogIndex = record.LogIndex ?? 0,
                IsDeleted = record.IsDeleted
            };

            if (schema != null && !record.IsDeleted)
            {
                try
                {
                    var encodedRecord = new EncodedTableRecord
                    {
                        TableId = record.TableIdBytes,
                        Key = record.KeyBytes.SplitBytes(),
                        EncodedValues = record
                    };
                    var fieldValues = schema.GetFieldValues(encodedRecord);
                    view.Fields = fieldValues.Select(fv => new MudFieldValueView
                    {
                        Name = fv.Name ?? "",
                        AbiType = fv.Type,
                        Value = FormatFieldValue(fv),
                        IsKey = fv.IsKey
                    }).ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not decode record for table {TableId}", tableId);
                    view.Fields = new List<MudFieldValueView>
                    {
                        new() { Name = "raw_key", Value = record.KeyBytes?.ToHex(prefix: true) ?? "" },
                        new() { Name = "raw_static", Value = record.StaticData?.ToHex(prefix: true) ?? "" },
                        new() { Name = "raw_dynamic", Value = record.DynamicData?.ToHex(prefix: true) ?? "" }
                    };
                }
            }
            else if (!record.IsDeleted)
            {
                view.Fields = new List<MudFieldValueView>
                {
                    new() { Name = "key", Value = record.KeyBytes?.ToHex(prefix: true) ?? "" },
                    new() { Name = "static_data", Value = record.StaticData?.ToHex(prefix: true) ?? "" },
                    new() { Name = "dynamic_data", Value = record.DynamicData?.ToHex(prefix: true) ?? "" }
                };
            }

            result.Add(view);
        }

        return result;
    }

    public async Task<TableSchema?> GetTableSchemaAsync(string worldAddress, string tableId, string rpcUrl)
    {
        return await GetOrFetchSchemaAsync(worldAddress, tableId, rpcUrl);
    }

    public async Task<List<string>> GetNormalisedTablesAsync(string worldAddress)
    {
        var queryService = GetNormalisedQuery();
        if (queryService == null) return new List<string>();
        try
        {
            var prefix = GetNormalisedPrefix(worldAddress);
            return await queryService.GetAvailableTablesAsync(prefix);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get normalised tables for {WorldAddress}", worldAddress);
            return new List<string>();
        }
    }

    public async Task<bool> HasNormalisedTableAsync(string worldAddress, string tableName)
    {
        var tables = await GetNormalisedTablesAsync(worldAddress);
        var prefix = GetNormalisedPrefix(worldAddress);
        var normalisedName = BuildNormalisedTableName(prefix, tableName);
        return tables.Contains(normalisedName);
    }

    public async Task<List<MudNormalisedRecordView>> GetNormalisedRecordsAsync(string tableName, int page, int pageSize)
    {
        var queryService = GetNormalisedQuery();
        if (queryService == null) return new List<MudNormalisedRecordView>();
        if (page < 1) page = 1;
        pageSize = ExplorerConstants.ClampPageSize(pageSize);

        var rows = await queryService.QueryAsync(tableName, null, pageSize, page - 1);
        return rows.Select(row => new MudNormalisedRecordView
        {
            Fields = row.ToDictionary(
                kvp => kvp.Key,
                kvp => FormatFieldValue(kvp.Value))
        }).ToList();
    }

    public async Task<int> GetNormalisedRecordCountAsync(string tableName)
    {
        var queryService = GetNormalisedQuery();
        if (queryService == null) return 0;
        return await queryService.CountAsync(tableName);
    }

    public async Task<List<MudNormalisedRecordView>> QueryNormalisedRecordsAsync(
        string tableName, List<MudQueryCondition> conditions, string? orderByField, bool orderByDesc, int page, int pageSize)
    {
        var queryService = GetNormalisedQuery();
        if (queryService == null) return new List<MudNormalisedRecordView>();
        if (page < 1) page = 1;
        pageSize = ExplorerConstants.ClampPageSize(pageSize);

        var predicate = BuildPredicate(conditions, orderByField, orderByDesc, page - 1, pageSize);
        var rows = await queryService.QueryAsync(tableName, predicate, pageSize, page - 1);
        return rows.Select(row => new MudNormalisedRecordView
        {
            Fields = row.ToDictionary(
                kvp => kvp.Key,
                kvp => FormatFieldValue(kvp.Value))
        }).ToList();
    }

    private static string FormatFieldValue(object? value)
    {
        if (value == null) return "";
        if (value is byte[] bytes) return bytes.ToHex(true);
        return value.ToString() ?? "";
    }

    public async Task<int> CountNormalisedRecordsAsync(string tableName, List<MudQueryCondition> conditions)
    {
        var queryService = GetNormalisedQuery();
        if (queryService == null) return 0;
        var predicate = BuildPredicate(conditions, null, false, 0, 0);
        return await queryService.CountAsync(tableName, predicate);
    }

    public async Task<List<MudColumnInfo>> GetNormalisedTableColumnsAsync(string tableName)
    {
        var queryService = GetNormalisedQuery();
        if (queryService == null) return new List<MudColumnInfo>();
        try
        {
            var columns = await queryService.GetTableColumnsAsync(tableName);
            return columns.Select(c => new MudColumnInfo
            {
                Name = c.Name,
                DataType = c.DataType
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get columns for table {TableName}", tableName);
            return new List<MudColumnInfo>();
        }
    }

    private static readonly HashSet<string> AllowedOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "=", "!=", "<>", ">", "<", ">=", "<=", "LIKE", "ILIKE"
    };

    private static readonly System.Text.RegularExpressions.Regex SafeColumnName =
        new(@"^[a-zA-Z_][a-zA-Z0-9_]*$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static Mud.TableRepository.TablePredicate BuildPredicate(
        List<MudQueryCondition> conditions, string? orderByField, bool orderByDesc, int page, int pageSize)
    {
        var predicate = new Mud.TableRepository.TablePredicate();

        if (conditions != null)
        {
            foreach (var c in conditions)
            {
                if (string.IsNullOrWhiteSpace(c.FieldName) || string.IsNullOrWhiteSpace(c.Value))
                    continue;

                if (!SafeColumnName.IsMatch(c.FieldName))
                    continue;

                var op = c.Operator;
                if (!AllowedOperators.Contains(op))
                    continue;

                var value = c.Value;
                if (string.Equals(op, "LIKE", StringComparison.OrdinalIgnoreCase))
                {
                    op = "ILIKE";
                    if (!value.Contains('%'))
                        value = $"%{value}%";
                }

                predicate.Conditions.Add(new Mud.TableRepository.KeyValueOperator
                {
                    Name = c.FieldName,
                    Key = c.FieldName.ToLowerInvariant(),
                    ComparisonOperator = op,
                    IsValueField = true,
                    RawValue = value,
                    AbiType = MapPostgresTypeToAbiType(c.DataType)
                });
            }
        }

        if (!string.IsNullOrEmpty(orderByField) && SafeColumnName.IsMatch(orderByField))
        {
            predicate.OrderByField = orderByField;
            predicate.OrderByDescending = orderByDesc;
        }

        predicate.Page = page;
        predicate.PageSize = pageSize;

        return predicate;
    }

    private static string MapPostgresTypeToAbiType(string pgType) => pgType?.ToLowerInvariant() switch
    {
        "integer" or "bigint" or "smallint" or "numeric" or "double precision" or "real" => "uint256",
        "boolean" => "bool",
        "bytea" => "bytes",
        _ => "string"
    };

    private static string GetNormalisedPrefix(string worldAddress)
    {
        var addr = worldAddress.Replace("0x", "").ToLowerInvariant();
        var addr6 = addr.Length >= 6 ? addr[..6] : addr;
        return $"w_{addr6}_";
    }

    private static string BuildNormalisedTableName(string prefix, string tableName)
    {
        return prefix + tableName.ToLowerInvariant().Replace(":", "_");
    }

    private async Task<TableSchema?> GetOrFetchSchemaAsync(string worldAddress, string tableId, string rpcUrl)
    {
        var cacheKey = $"{worldAddress}_{tableId}";
        if (_schemaCache.TryGetValue(cacheKey, out var cached))
            return cached;

        try
        {
            var web3 = _web3Factory.GetWeb3();
            if (web3 == null) return null;
            var store = new StoreNamespace(web3, worldAddress);
            var tableIdBytes = tableId.HexToByteArray();
            var tableRecord = await store.Tables.TablesTableService.GetTableRecordAsync(tableIdBytes);
            var schema = tableRecord.GetTableSchema();
            _schemaCache.TryAdd(cacheKey, schema);
            return schema;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to fetch schema for {WorldAddress} table {TableId}", worldAddress, tableId);
            return null;
        }
    }

    private static string FormatFieldValue(FieldValue fv)
    {
        if (fv.Value == null) return "";
        if (fv.Value is byte[] bytes) return bytes.ToHex(prefix: true);
        if (fv.Value is System.Collections.IList list)
        {
            var items = new List<string>();
            foreach (var item in list)
                items.Add(item?.ToString() ?? "");
            return $"[{string.Join(", ", items)}]";
        }
        return fv.Value.ToString() ?? "";
    }

    private static string GetResourceTypeName(Resource resource)
    {
        if (resource.ResourceTypeId == null || resource.ResourceTypeId.Length < 2) return "unknown";
        if (resource.ResourceTypeId[0] == Resource.RESOURCE_TABLE[0] && resource.ResourceTypeId[1] == Resource.RESOURCE_TABLE[1]) return "table";
        if (resource.ResourceTypeId[0] == Resource.RESOURCE_SYSTEM[0] && resource.ResourceTypeId[1] == Resource.RESOURCE_SYSTEM[1]) return "system";
        if (resource.ResourceTypeId[0] == Resource.RESOURCE_NAMESPACE[0] && resource.ResourceTypeId[1] == Resource.RESOURCE_NAMESPACE[1]) return "namespace";
        if (resource.ResourceTypeId[0] == Resource.RESOURCE_OFFCHAIN_TABLE[0] && resource.ResourceTypeId[1] == Resource.RESOURCE_OFFCHAIN_TABLE[1]) return "offchain";
        return "unknown";
    }
}
