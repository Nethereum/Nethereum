"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.extractTables = void 0;
var n = require('./Nethereum.Generators.DuoCode.js');
var parameterAbiClass = Nethereum.Generators.Model.ParameterABI;
var mudTableClass = Nethereum.Generators.MudTable.MudTable;
function extractTables(jsonString) {
    const world = JSON.parse(jsonString);
    const mudTables = [];
    // Check if input format is single namespace or multiple namespaces
    if (world.tables) {
        // Single namespace (old format)
        processNamespace(world.namespace, world.tables, mudTables);
    }
    else if (world.namespaces) {
        // Multiple namespaces (new format)
        for (const namespaceKey in world.namespaces) {
            const namespace = world.namespaces[namespaceKey];
            processNamespace(namespaceKey, namespace.tables, mudTables);
        }
    }
    return mudTables;
}
exports.extractTables = extractTables;
function processNamespace(namespace, tables, mudTables) {
    for (const tableKey in tables) {
        const table = tables[tableKey];
        const mudTable = new mudTableClass.ctor();
        mudTables.push(mudTable);
        mudTable.set_Name(tableKey);
        mudTable.set_MudNamespace(namespace);
        let schemaOrder = 0;
        let keyOrder = 0;
        let valueParameters = [];
        let keyParameters = [];
        for (const schemaKey in table.schema) {
            if (!table.key.includes(schemaKey)) {
                schemaOrder++;
                var parameter = new parameterAbiClass.ctor$1(table.schema[schemaKey], schemaKey, schemaOrder);
                valueParameters.push(parameter);
            }
        }
        mudTable.set_ValueSchema(valueParameters);
        for (const key of table.key) {
            const type = table.schema[key];
            keyOrder++;
            var parameter = new parameterAbiClass.ctor$1(type, key, keyOrder);
            keyParameters.push(parameter);
        }
        mudTable.set_Keys(keyParameters);
    }
}
//# sourceMappingURL=MudWorldParser.js.map