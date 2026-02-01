import * as fs from 'fs';
import * as path from 'path';
import * as fsex from 'fs-extra';
import * as abiDes from "./AbiDeserialiser"
import * as mudParse from "./MudWorldParser";

var n = require('./Nethereum.Generators.DuoCode.js');

 function generateAllClassesInternal(abi: string, byteCode: string,
    contractName: string,
    baseNamespace: string,
    serviceNamespace: string,
    cqsNamespace: string,
    dtoNamespace: string,
    sharedTypesNamespace: string,
    sharedTypes: string[],
    basePath: string,
    pathSeparator: string,
    codeGenLang: int,
    mudNamespace: string = null,
    referencedTypesNamespaces: string[] = null,
    structReferencedTypes: string[] = null
 ): string[] {
    var contractDes = abiDes.buildContract(abi);
    var classGenerator = new Nethereum.Generators.ContractProjectGenerator(contractDes,
        contractName,
        byteCode,
        baseNamespace,
        serviceNamespace,
        cqsNamespace,
        dtoNamespace,
        sharedTypesNamespace,
        sharedTypes,
        basePath,
        pathSeparator,
        codeGenLang,
        referencedTypesNamespaces,
        structReferencedTypes);

     classGenerator.set_AddRootNamespaceOnVbProjectsToImportStatements(false);
     if (mudNamespace !== null && mudNamespace !== undefined && mudNamespace !== '') {
         classGenerator.set_MudNamespace(mudNamespace);
     }
    var generatedClases = classGenerator.GenerateAllMessagesFileAndService();
    return outputFiles(generatedClases);
}

function generateMudServiceInternal(abi: string, byteCode: string,
    contractName: string,
    baseNamespace: string,
    serviceNamespace: string,
    cqsNamespace: string,
    dtoNamespace: string,
    sharedTypesNamespace: string,
    sharedTypes: string[],
    basePath: string,
    pathSeparator: string,
    codeGenLang: int,
    mudNamespace: string
):string[] {
    var contractDes = abiDes.buildContract(abi);
    var classGenerator = new Nethereum.Generators.ContractProjectGenerator(contractDes,
        contractName,
        byteCode,
        baseNamespace,
        serviceNamespace,
        cqsNamespace,
        dtoNamespace,
        sharedTypesNamespace,
        sharedTypes,
        basePath,
        pathSeparator,
        codeGenLang
        );

    classGenerator.set_AddRootNamespaceOnVbProjectsToImportStatements(false);
    var generatedClases = classGenerator.GenerateMudService(mudNamespace);
    return outputFiles([generatedClases]);
}


function generateAllUnityClassesInternal(abi: string, byteCode: string,
    contractName: string,
    baseNamespace: string,
    serviceNamespace: string,
    cqsNamespace: string,
    dtoNamespace: string,
    sharedTypesNamespace: string,
    sharedTypes: string[],
    basePath: string,
    pathSeparator: string
):string[] {
    var contractDes = abiDes.buildContract(abi);
    var classGenerator = new Nethereum.Generators.ContractProjectGenerator(contractDes,
        contractName,
        byteCode,
        baseNamespace,
        serviceNamespace,
        cqsNamespace,
        dtoNamespace,
        sharedTypesNamespace,
        sharedTypes,
        basePath,
        pathSeparator,
        0);
        var generatedClases = classGenerator.GenerateAllUnity();
       return outputFiles(generatedClases);
}

function generateAllMudTablesInternal(json: string, baseNamespace: string,
    namespace: string,
    basePath: string,
    pathSeparator: string,
    codeGenLang: int,
    mudNamespace: string)  :string[] {
    var tables = mudParse.extractTables(json);
    tables = tables.filter(t =>
        t.get_MudNamespace() === mudNamespace ||
        ((mudNamespace === '' || mudNamespace === undefined || mudNamespace === null) && (t.get_MudNamespace() === null || t.get_MudNamespace() === undefined || mudNamespace === ''))
    );
    var mudTableGenerator =
        new Nethereum.Generators.MudTablesGenerator(tables, baseNamespace, codeGenLang, basePath, pathSeparator, namespace);
    var generatedTables = mudTableGenerator.GenerateAllTables();
    return outputFiles(generatedTables);
}

function outputFiles(generatedFiles: Nethereum.Generators.Core.GeneratedFile[]) : string[] {
    var files = [];
    for (var i = 0; i < generatedFiles.length; i++) {
        files.push(outputFile(generatedFiles[i]));
    }
    return files;
}

function outputFile(generatedFile: Nethereum.Generators.Core.GeneratedFile): string {

    fsex.ensureDirSync(generatedFile.get_OutputFolder());
    var fullPath = path.join(generatedFile.get_OutputFolder(), generatedFile.get_FileName());

    if (fs.existsSync(fullPath)) {
        fs.unlinkSync(fullPath);
    }
    fs.writeFileSync(fullPath, generatedFile.get_GeneratedCode());
    return fullPath;
}

export function  generateNetStandardClassLibrary(projectName: string, basePath: string, codeLang: int): string {
    var projectGenerator = new Nethereum.Generators.NetStandardLibraryGenerator(projectName, codeLang);
    var generatedProject = projectGenerator.GenerateFileContent(basePath);
   return outputFile(generatedProject);
}

export function generateAllClasses(abi: string, byteCode: string,
    contractName: string,
    baseNamespace: string,
    sharedTypesNamespace: string,
    sharedTypes: string[],
    basePath: string,
    codeGenLang: int,
    mudNamespace: string = null,
    referencedTypesNamespaces: string[] = null,
    structReferencedTypes: string[] = null
) : string[] {

    var serviceNamespace = contractName;
    //Same, we are generating single file
    var cqsNamespace = contractName + ".ContractDefinition";
    var dtoNamespace = contractName + ".ContractDefinition";
    var pathSeparator = path.sep;
    return generateAllClassesInternal(abi,
        byteCode,
        contractName,
        baseNamespace,
        serviceNamespace,
        cqsNamespace,
        dtoNamespace,
        sharedTypesNamespace,
        sharedTypes,
        basePath,
        pathSeparator,
        codeGenLang,
        mudNamespace,
        referencedTypesNamespaces,
        structReferencedTypes);
}

export function generateMudService(abi: string, byteCode: string,
    contractName: string,
    baseNamespace: string,
    basePath: string,
    sharedTypesNamespace: string,
    sharedTypes: string[],
    codeGenLang: int,
    mudNamespace: string
): string[] {

    var serviceNamespace = contractName;
    //Same, we are generating single file
    var cqsNamespace = contractName + ".ContractDefinition";
    var dtoNamespace = contractName + ".ContractDefinition";
    var pathSeparator = path.sep;
    return generateMudServiceInternal(abi,
        byteCode,
        contractName,
        baseNamespace,
        serviceNamespace,
        cqsNamespace,
        dtoNamespace,
        sharedTypesNamespace,
        sharedTypes,
        basePath,
        pathSeparator,
        codeGenLang,
        mudNamespace);
}

export function generateMudTables(json: string, baseNamespace: string,
    namespace: string,
    basePath: string,
    codeGenLang: int,
    mudNamespace: string
) : string[] {
    var pathSeparator = path.sep;
   return generateAllMudTablesInternal(json, baseNamespace, namespace, basePath, pathSeparator, codeGenLang, mudNamespace);
}

export function generateUnityRequests(abi: string, byteCode: string,
    contractName: string,
    baseNamespace: string,
    sharedTypesNamespace: string,
    sharedTypes: string[],
    basePath: string
) : string[] {

    var serviceNamespace = contractName;
    //Same, we are generating single file
    var cqsNamespace = contractName + ".ContractDefinition";
    var dtoNamespace = contractName + ".ContractDefinition";
    var pathSeparator = path.sep;
    return generateAllUnityClassesInternal(abi,
        byteCode,
        contractName,
        baseNamespace,
        serviceNamespace,
        cqsNamespace,
        dtoNamespace,
        sharedTypesNamespace,
        sharedTypes,
        basePath,
        pathSeparator
        );
}

function extractAbiAndBytecode(fileName: string) {
    const outputPathInfo = path.parse(fileName);
    const contractName = outputPathInfo.name;
    let compilationOutput;
    let abi = undefined;
    let bytecode = '0x';
    if (outputPathInfo.ext === '.abi') {
        abi = fs.readFileSync(fileName, 'utf8');
        compilationOutput = { 'abi': abi, 'bytecode': '0x' };
        const binFile = fileName.substr(0, fileName.lastIndexOf('.')) + '.bin';
        if (fs.existsSync(binFile)) {
            bytecode = fs.readFileSync(binFile, 'utf8');
        }
    } else {
        compilationOutput = JSON.parse(fs.readFileSync(fileName, 'utf8'));
        abi = JSON.stringify(compilationOutput.abi);
        bytecode = compilationOutput.bytecode.object;
        if (bytecode === undefined) {
            bytecode = compilationOutput.bytecode;
        }
    }
    return { abi, bytecode, contractName };
}


function extractWordFromConfig(configFilePath: string): string {
    const configContent = fs.readFileSync(configFilePath, 'utf8');

    // Use a regex to match the content inside the defineWorld function
    const jsonMatch = configContent.match(/defineWorld\(([\s\S]*?)\);/);
    if (jsonMatch && jsonMatch[1]) {
        const worldConfigString = jsonMatch[1].trim();

        // Safely evaluate the JSON-like content
        const extractWorld = (content: string) => {
            return (new Function(`return ${content}`))();
        };

        const worldConfig = extractWorld(worldConfigString);

        if ((worldConfig && worldConfig.tables) || (worldConfig && worldConfig.namespaces)) {
            // Convert worldConfig to JSON string
            return JSON.stringify(worldConfig);
        }
    }

    throw new Error("Unable to extract tables from config file");
}

function applyDefaults(config: GeneratorConfig): GeneratorConfig {
    return {
        baseNamespace: config.baseNamespace || "",
        codeGenLang: config.codeGenLang ?? 0,
        basePath: config.basePath,
        sharedTypesNamespace: config.sharedTypesNamespace || "",
        sharedTypes: config.sharedTypes || [],
        generatorType: config.generatorType,
        mudNamespace: config.mudNamespace || "",
        referencedTypesNamespaces: config.referencedTypesNamespaces || [],
        structReferencedTypes: config.structReferencedTypes || []
    };
}

function generateFilesUsingConfig(generatorConfig: GeneratorConfig, fileName: string, root: string) :string[] {
    const { baseNamespace, codeGenLang, basePath, generatorType, sharedTypesNamespace, sharedTypes, mudNamespace, referencedTypesNamespaces, structReferencedTypes } = applyDefaults(generatorConfig);
    const absolutePath = path.resolve(root, basePath);
    var files = [];
    if (!fileName.endsWith('mud.config.ts')) {
        const { abi, bytecode, contractName } = extractAbiAndBytecode(fileName);
        switch (generatorType) {
            case GeneratorType.ContractDefinition:
                files = generateAllClasses(abi, bytecode, contractName, baseNamespace, sharedTypesNamespace, sharedTypes, absolutePath, codeGenLang, mudNamespace, referencedTypesNamespaces, structReferencedTypes);
                break;
            case GeneratorType.UnityRequest:
                files = generateUnityRequests(abi, bytecode, contractName, baseNamespace, sharedTypesNamespace, sharedTypes, absolutePath);
                break;
            case GeneratorType.MudExtendedService:
                files = generateMudService(abi, bytecode, contractName, baseNamespace, absolutePath, sharedTypesNamespace, sharedTypes, codeGenLang, mudNamespace);
                break;
            case GeneratorType.NetStandardLibrary:
                files = [generateNetStandardClassLibrary(contractName, absolutePath, codeGenLang) ];
                break;
            case GeneratorType.BlazorPageService:
                files = [generateBlazorPageService(abi, contractName, baseNamespace, absolutePath, sharedTypesNamespace, codeGenLang)];
                break;
            default:
                throw new Error("Unknown GeneratorType: " + generatorType);
        }
    } else {
        switch (generatorType) {
            case GeneratorType.MudTables:
                const tablesConfig = extractWordFromConfig(fileName);
                files = generateMudTables(tablesConfig, baseNamespace, "", absolutePath, codeGenLang, mudNamespace);
                break;
            default:
                throw new Error("Unknown GeneratorType: " + generatorType);
        }
    }
    return files;
}

export function generateFilesFromGeneratorConfigs(generatorConfigs: GeneratorConfig[], fileName: string, rootPath: string = ''): string[] {
    var files = [];
    generatorConfigs.forEach(generatorConfig => {
        files = files.concat(generateFilesUsingConfig(generatorConfig, fileName, rootPath));
    });
    return files;
}

export function generateFilesFromConfigSetsArray(configSetsArray: GeneratorSetConfig[], rootPath: string): string[] {
    var files = [];
    configSetsArray.forEach(configSet => {
        configSet.paths.forEach(relativePath => {
            const absolutePath = path.resolve(rootPath, relativePath);

            const file = generateFilesFromGeneratorConfigs(configSet.generatorConfigs, absolutePath, rootPath);
            files = files.concat(file);
        });
    });
    return files;
}

export function generateFilesFromConfigJsonFile(configJsonPath: string, rootPath: string) :string[] {
    const configSetsArray = JSON.parse(fs.readFileSync(configJsonPath, 'utf8')) as GeneratorSetConfig[];
    return generateFilesFromConfigSetsArray(configSetsArray, rootPath);
}

export function generateFilesFromConfigJsonString(configJson: string, rootPath: string) : string[]{
    const configSetsArray = JSON.parse(configJson) as GeneratorSetConfig[];
    return generateFilesFromConfigSetsArray(configSetsArray, rootPath);
}

export enum GeneratorType {
    ContractDefinition = "ContractDefinition",
    UnityRequest = "UnityRequest",
    MudExtendedService = "MudExtendedService",
    MudTables = "MudTables",
    NetStandardLibrary = "NetStandardLibrary",
    BlazorPageService = "BlazorPageService",
}

export interface GeneratorConfig {
    baseNamespace: string;
    codeGenLang: int;
    basePath: string;
    sharedTypesNamespace: string;
    sharedTypes: string[];
    generatorType: GeneratorType;
    mudNamespace: string;
    referencedTypesNamespaces: string[];
    structReferencedTypes: string[];
}

export interface GeneratorSetConfig {
    paths: string[];
    default: boolean;
    generatorConfigs: GeneratorConfig[]
}



export function generateBlazorPageService(abi: any, contractName: string, baseNamespace: string, absolutePath: string, sharedTypesNamespace: string, codeGenLang: number): any {
    var serviceNamespace = contractName;
    //Same, we are generating single file
    var cqsNamespace = contractName + ".ContractDefinition";
    var dtoNamespace = contractName + ".ContractDefinition";
    var pathSeparator = path.sep;
    return generateBlazorPageServiceInternal(abi, contractName, baseNamespace, serviceNamespace, cqsNamespace, dtoNamespace, sharedTypesNamespace, absolutePath, pathSeparator, codeGenLang);
}

function generateBlazorPageServiceInternal(abi: string,
    contractName: string,
    baseNamespace: string,
    serviceNamespace: string,
    cqsNamespace: string,
    dtoNamespace: string,
    sharedTypesNamespace: string,
    basePath: string,
    pathSeparator: string,
    codeGenLang: int,
): string {
    var contractDes = abiDes.buildContract(abi);
    var classGenerator = new Nethereum.Generators.BlazorPagesGenerator(
        contractDes,
        contractName,
        baseNamespace,
        serviceNamespace,
        cqsNamespace,
        dtoNamespace,
        sharedTypesNamespace,
        codeGenLang,
        basePath,
        pathSeparator,
        "");


    var generatedClass = classGenerator.GenerateFile();
    return outputFile(generatedClass);
}
