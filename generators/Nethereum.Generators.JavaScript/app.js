"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.generateBlazorPageService = exports.GeneratorType = exports.generateFilesFromConfigJsonString = exports.generateFilesFromConfigJsonFile = exports.generateFilesFromConfigSetsArray = exports.generateFilesFromGeneratorConfigs = exports.generateUnityRequests = exports.generateMudTables = exports.generateMudService = exports.generateAllClasses = exports.generateNetStandardClassLibrary = void 0;
const fs = require("fs");
const path = require("path");
const fsex = require("fs-extra");
const abiDes = require("./AbiDeserialiser");
const mudParse = require("./MudWorldParser");
var n = require('./Nethereum.Generators.DuoCode.js');
function generateAllClassesInternal(abi, byteCode, contractName, baseNamespace, serviceNamespace, cqsNamespace, dtoNamespace, sharedTypesNamespace, sharedTypes, basePath, pathSeparator, codeGenLang, mudNamespace = null) {
    var contractDes = abiDes.buildContract(abi);
    var classGenerator = new Nethereum.Generators.ContractProjectGenerator(contractDes, contractName, byteCode, baseNamespace, serviceNamespace, cqsNamespace, dtoNamespace, sharedTypesNamespace, sharedTypes, basePath, pathSeparator, codeGenLang);
    classGenerator.set_AddRootNamespaceOnVbProjectsToImportStatements(false);
    if (mudNamespace !== null && mudNamespace !== undefined && mudNamespace !== '') {
        classGenerator.set_MudNamespace(mudNamespace);
    }
    var generatedClases = classGenerator.GenerateAllMessagesFileAndService();
    return outputFiles(generatedClases);
}
function generateMudServiceInternal(abi, byteCode, contractName, baseNamespace, serviceNamespace, cqsNamespace, dtoNamespace, sharedTypesNamespace, sharedTypes, basePath, pathSeparator, codeGenLang, mudNamespace) {
    var contractDes = abiDes.buildContract(abi);
    var classGenerator = new Nethereum.Generators.ContractProjectGenerator(contractDes, contractName, byteCode, baseNamespace, serviceNamespace, cqsNamespace, dtoNamespace, sharedTypesNamespace, sharedTypes, basePath, pathSeparator, codeGenLang);
    classGenerator.set_AddRootNamespaceOnVbProjectsToImportStatements(false);
    var generatedClases = classGenerator.GenerateMudService(mudNamespace);
    return outputFiles([generatedClases]);
}
function generateAllUnityClassesInternal(abi, byteCode, contractName, baseNamespace, serviceNamespace, cqsNamespace, dtoNamespace, sharedTypesNamespace, sharedTypes, basePath, pathSeparator) {
    var contractDes = abiDes.buildContract(abi);
    var classGenerator = new Nethereum.Generators.ContractProjectGenerator(contractDes, contractName, byteCode, baseNamespace, serviceNamespace, cqsNamespace, dtoNamespace, sharedTypesNamespace, sharedTypes, basePath, pathSeparator, 0);
    var generatedClases = classGenerator.GenerateAllUnity();
    return outputFiles(generatedClases);
}
function generateAllMudTablesInternal(json, baseNamespace, namespace, basePath, pathSeparator, codeGenLang, mudNamespace) {
    var tables = mudParse.extractTables(json);
    tables = tables.filter(t => t.get_MudNamespace() === mudNamespace ||
        ((mudNamespace === '' || mudNamespace === undefined || mudNamespace === null) && (t.get_MudNamespace() === null || t.get_MudNamespace() === undefined || mudNamespace === '')));
    var mudTableGenerator = new Nethereum.Generators.MudTablesGenerator(tables, baseNamespace, codeGenLang, basePath, pathSeparator, namespace);
    var generatedTables = mudTableGenerator.GenerateAllTables();
    return outputFiles(generatedTables);
}
function outputFiles(generatedFiles) {
    var files = [];
    for (var i = 0; i < generatedFiles.length; i++) {
        files.push(outputFile(generatedFiles[i]));
    }
    return files;
}
function outputFile(generatedFile) {
    fsex.ensureDirSync(generatedFile.get_OutputFolder());
    var fullPath = path.join(generatedFile.get_OutputFolder(), generatedFile.get_FileName());
    if (fs.existsSync(fullPath)) {
        fs.unlinkSync(fullPath);
    }
    fs.writeFileSync(fullPath, generatedFile.get_GeneratedCode());
    return fullPath;
}
function generateNetStandardClassLibrary(projectName, basePath, codeLang) {
    var projectGenerator = new Nethereum.Generators.NetStandardLibraryGenerator(projectName, codeLang);
    var generatedProject = projectGenerator.GenerateFileContent(basePath);
    return outputFile(generatedProject);
}
exports.generateNetStandardClassLibrary = generateNetStandardClassLibrary;
function generateAllClasses(abi, byteCode, contractName, baseNamespace, sharedTypesNamespace, sharedTypes, basePath, codeGenLang, mudNamespace = null) {
    var serviceNamespace = contractName;
    //Same, we are generating single file
    var cqsNamespace = contractName + ".ContractDefinition";
    var dtoNamespace = contractName + ".ContractDefinition";
    var pathSeparator = path.sep;
    return generateAllClassesInternal(abi, byteCode, contractName, baseNamespace, serviceNamespace, cqsNamespace, dtoNamespace, sharedTypesNamespace, sharedTypes, basePath, pathSeparator, codeGenLang, mudNamespace);
}
exports.generateAllClasses = generateAllClasses;
function generateMudService(abi, byteCode, contractName, baseNamespace, basePath, sharedTypesNamespace, sharedTypes, codeGenLang, mudNamespace) {
    var serviceNamespace = contractName;
    //Same, we are generating single file
    var cqsNamespace = contractName + ".ContractDefinition";
    var dtoNamespace = contractName + ".ContractDefinition";
    var pathSeparator = path.sep;
    return generateMudServiceInternal(abi, byteCode, contractName, baseNamespace, serviceNamespace, cqsNamespace, dtoNamespace, sharedTypesNamespace, sharedTypes, basePath, pathSeparator, codeGenLang, mudNamespace);
}
exports.generateMudService = generateMudService;
function generateMudTables(json, baseNamespace, namespace, basePath, codeGenLang, mudNamespace) {
    var pathSeparator = path.sep;
    return generateAllMudTablesInternal(json, baseNamespace, namespace, basePath, pathSeparator, codeGenLang, mudNamespace);
}
exports.generateMudTables = generateMudTables;
function generateUnityRequests(abi, byteCode, contractName, baseNamespace, sharedTypesNamespace, sharedTypes, basePath) {
    var serviceNamespace = contractName;
    //Same, we are generating single file
    var cqsNamespace = contractName + ".ContractDefinition";
    var dtoNamespace = contractName + ".ContractDefinition";
    var pathSeparator = path.sep;
    return generateAllUnityClassesInternal(abi, byteCode, contractName, baseNamespace, serviceNamespace, cqsNamespace, dtoNamespace, sharedTypesNamespace, sharedTypes, basePath, pathSeparator);
}
exports.generateUnityRequests = generateUnityRequests;
function extractAbiAndBytecode(fileName) {
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
    }
    else {
        compilationOutput = JSON.parse(fs.readFileSync(fileName, 'utf8'));
        abi = JSON.stringify(compilationOutput.abi);
        bytecode = compilationOutput.bytecode.object;
        if (bytecode === undefined) {
            bytecode = compilationOutput.bytecode;
        }
    }
    return { abi, bytecode, contractName };
}
function extractWordFromConfig(configFilePath) {
    const configContent = fs.readFileSync(configFilePath, 'utf8');
    // Use a regex to match the content inside the defineWorld function
    const jsonMatch = configContent.match(/defineWorld\(([\s\S]*?)\);/);
    if (jsonMatch && jsonMatch[1]) {
        const worldConfigString = jsonMatch[1].trim();
        // Safely evaluate the JSON-like content
        const extractWorld = (content) => {
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
function applyDefaults(config) {
    return {
        baseNamespace: config.baseNamespace || "",
        codeGenLang: config.codeGenLang ?? 0,
        basePath: config.basePath,
        sharedTypesNamespace: config.sharedTypesNamespace || "",
        sharedTypes: config.sharedTypes || [],
        generatorType: config.generatorType,
        mudNamespace: config.mudNamespace || ""
    };
}
function generateFilesUsingConfig(generatorConfig, fileName, root) {
    const { baseNamespace, codeGenLang, basePath, generatorType, sharedTypesNamespace, sharedTypes, mudNamespace } = applyDefaults(generatorConfig);
    const absolutePath = path.resolve(root, basePath);
    var files = [];
    if (!fileName.endsWith('mud.config.ts')) {
        const { abi, bytecode, contractName } = extractAbiAndBytecode(fileName);
        switch (generatorType) {
            case GeneratorType.ContractDefinition:
                files = generateAllClasses(abi, bytecode, contractName, baseNamespace, sharedTypesNamespace, sharedTypes, absolutePath, codeGenLang, mudNamespace);
                break;
            case GeneratorType.UnityRequest:
                files = generateUnityRequests(abi, bytecode, contractName, baseNamespace, sharedTypesNamespace, sharedTypes, absolutePath);
                break;
            case GeneratorType.MudExtendedService:
                files = generateMudService(abi, bytecode, contractName, baseNamespace, absolutePath, sharedTypesNamespace, sharedTypes, codeGenLang, mudNamespace);
                break;
            case GeneratorType.NetStandardLibrary:
                files = [generateNetStandardClassLibrary(contractName, absolutePath, codeGenLang)];
                break;
            case GeneratorType.BlazorPageService:
                files = [generateBlazorPageService(abi, contractName, baseNamespace, absolutePath, sharedTypesNamespace, codeGenLang)];
                break;
            default:
                throw new Error("Unknown GeneratorType: " + generatorType);
        }
    }
    else {
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
function generateFilesFromGeneratorConfigs(generatorConfigs, fileName, rootPath = '') {
    var files = [];
    generatorConfigs.forEach(generatorConfig => {
        files = files.concat(generateFilesUsingConfig(generatorConfig, fileName, rootPath));
    });
    return files;
}
exports.generateFilesFromGeneratorConfigs = generateFilesFromGeneratorConfigs;
function generateFilesFromConfigSetsArray(configSetsArray, rootPath) {
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
exports.generateFilesFromConfigSetsArray = generateFilesFromConfigSetsArray;
function generateFilesFromConfigJsonFile(configJsonPath, rootPath) {
    const configSetsArray = JSON.parse(fs.readFileSync(configJsonPath, 'utf8'));
    return generateFilesFromConfigSetsArray(configSetsArray, rootPath);
}
exports.generateFilesFromConfigJsonFile = generateFilesFromConfigJsonFile;
function generateFilesFromConfigJsonString(configJson, rootPath) {
    const configSetsArray = JSON.parse(configJson);
    return generateFilesFromConfigSetsArray(configSetsArray, rootPath);
}
exports.generateFilesFromConfigJsonString = generateFilesFromConfigJsonString;
var GeneratorType;
(function (GeneratorType) {
    GeneratorType["ContractDefinition"] = "ContractDefinition";
    GeneratorType["UnityRequest"] = "UnityRequest";
    GeneratorType["MudExtendedService"] = "MudExtendedService";
    GeneratorType["MudTables"] = "MudTables";
    GeneratorType["NetStandardLibrary"] = "NetStandardLibrary";
    GeneratorType["BlazorPageService"] = "BlazorPageService";
})(GeneratorType || (exports.GeneratorType = GeneratorType = {}));
function generateBlazorPageService(abi, contractName, baseNamespace, absolutePath, sharedTypesNamespace, codeGenLang) {
    var serviceNamespace = contractName;
    //Same, we are generating single file
    var cqsNamespace = contractName + ".ContractDefinition";
    var dtoNamespace = contractName + ".ContractDefinition";
    var pathSeparator = path.sep;
    return generateBlazorPageServiceInternal(abi, contractName, baseNamespace, serviceNamespace, cqsNamespace, dtoNamespace, sharedTypesNamespace, absolutePath, pathSeparator, codeGenLang);
}
exports.generateBlazorPageService = generateBlazorPageService;
function generateBlazorPageServiceInternal(abi, contractName, baseNamespace, serviceNamespace, cqsNamespace, dtoNamespace, sharedTypesNamespace, basePath, pathSeparator, codeGenLang) {
    var contractDes = abiDes.buildContract(abi);
    var classGenerator = new Nethereum.Generators.BlazorPagesGenerator(contractDes, contractName, baseNamespace, serviceNamespace, cqsNamespace, dtoNamespace, sharedTypesNamespace, codeGenLang, basePath, pathSeparator, "");
    var generatedClass = classGenerator.GenerateFile();
    return outputFile(generatedClass);
}
//# sourceMappingURL=app.js.map