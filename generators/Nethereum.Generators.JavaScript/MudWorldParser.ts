var n = require('./Nethereum.Generators.DuoCode.js');

var parameterAbiClass = Nethereum.Generators.Model.ParameterABI;
var mudTableClass = Nethereum.Generators.MudTable.MudTable;


interface Table {
    schema: { [key: string]: string };
    key: string[];
}

interface Namespace {
    tables: { [key: string]: Table };
}

interface World {
    namespace?: string; // Single namespace (old format)
    tables?: { [key: string]: Table }; //Single namespace (old format)
    namespaces?: { [key: string]: Namespace }; //Multiple namespaces (new format)
}

export function extractTables(jsonString: string): Nethereum.Generators.MudTable.MudTable[] {
    const world = JSON.parse(jsonString) as World;
    const mudTables: Nethereum.Generators.MudTable.MudTable[] = [];

    // Check if input format is single namespace or multiple namespaces
    if (world.tables) {
        // Single namespace (old format)
        processNamespace(world.namespace, world.tables, mudTables);
    } else if (world.namespaces) {
        // Multiple namespaces (new format)
        for (const namespaceKey in world.namespaces) {
            const namespace = world.namespaces[namespaceKey];
            processNamespace(namespaceKey, namespace.tables, mudTables);
        }
    }
    return mudTables;
}

function processNamespace(namespace: string, tables: { [key: string]: Table }, mudTables: Nethereum.Generators.MudTable.MudTable[]) {
    for (const tableKey in tables) {
        const table = tables[tableKey];

        const mudTable = new mudTableClass.ctor();
        mudTables.push(mudTable);
        mudTable.set_Name(tableKey);
        mudTable.set_MudNamespace(namespace);

        let schemaOrder = 0;
        let keyOrder = 0;
        let valueParameters: any[] = [];
        let keyParameters: any[] = [];

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
