"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.extractTables = void 0;
var n = require('./Nethereum.Generators.DuoCode.js');
var parameterAbiClass = Nethereum.Generators.Model.ParameterABI;
var mudTableClass = Nethereum.Generators.MudTable.MudTable;
function extractTables(jsonString) {
    const world = JSON.parse(jsonString);
    const mudTables = [];
    if (!world || !world.tables)
        return mudTables;
    for (const tableKey in world.tables) {
        const table = world.tables[tableKey];
        const mudTable = new mudTableClass.ctor();
        mudTables.push(mudTable);
        mudTable.set_Name(tableKey);
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
    return mudTables;
}
exports.extractTables = extractTables;
//# sourceMappingURL=MudWorldParser.js.map