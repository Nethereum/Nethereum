var n = require('./Nethereum.Generators.DuoCode.js');

var parameterAbiClass = Nethereum.Generators.Model.ParameterABI;
var mudTableClass = Nethereum.Generators.MudTable.MudTable;

interface Table {
    schema: { [key: string]: string };
    key: string[];
}

interface Word {
    tables: { [key: string]: Table };
}

export function extractTables(jsonString: string): Nethereum.Generators.MudTable.MudTable[] {
    const world = JSON.parse(jsonString) as Word;
    const mudTables: Nethereum.Generators.MudTable.MudTable[] = [];
    if (!world || !world.tables) return mudTables;

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
