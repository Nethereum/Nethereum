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
    basePath: string,
    pathSeparator: string,
    codeGenLang: int
) {
    var contractDes = abiDes.buildContract(abi);
    var classGenerator = new Nethereum.Generators.ContractProjectGenerator(contractDes,
        contractName,
        byteCode,
        baseNamespace,
        serviceNamespace,
        cqsNamespace,
        dtoNamespace,
        basePath,
        pathSeparator,
         codeGenLang);

    classGenerator.set_AddRootNamespaceOnVbProjectsToImportStatements(false);
    var generatedClases = classGenerator.GenerateAllMessagesFileAndService();
    outputFiles(generatedClases);
}

function generateMudServiceInternal(abi: string, byteCode: string,
    contractName: string,
    baseNamespace: string,
    serviceNamespace: string,
    cqsNamespace: string,
    dtoNamespace: string,
    basePath: string,
    pathSeparator: string,
    codeGenLang: int
) {
    var contractDes = abiDes.buildContract(abi);
    var classGenerator = new Nethereum.Generators.ContractProjectGenerator(contractDes,
        contractName,
        byteCode,
        baseNamespace,
        serviceNamespace,
        cqsNamespace,
        dtoNamespace,
        basePath,
        pathSeparator,
        codeGenLang);

    classGenerator.set_AddRootNamespaceOnVbProjectsToImportStatements(false);
    var generatedClases = classGenerator.GenerateMudService();
    outputFiles([generatedClases]);
}



function generateAllUnityClassesInternal(abi: string, byteCode: string,
    contractName: string,
    baseNamespace: string,
    serviceNamespace: string,
    cqsNamespace: string,
    dtoNamespace: string,
    basePath: string,
    pathSeparator: string
) {
    var contractDes = abiDes.buildContract(abi);
    var classGenerator = new Nethereum.Generators.ContractProjectGenerator(contractDes,
        contractName,
        byteCode,
        baseNamespace,
        serviceNamespace,
        cqsNamespace,
        dtoNamespace,
        basePath,
        pathSeparator,
        0);
        var generatedClases = classGenerator.GenerateAllUnity();
        outputFiles(generatedClases);
}

function generateAllMudTablesInternal(json: string, baseNamespace: string,
    namespace: string,
    basePath: string,
    pathSeparator: string,
    codeGenLang: int) {
    var tables = mudParse.extractTables(json);
    var mudTableGenerator =
        new Nethereum.Generators.MudTablesGenerator(tables, baseNamespace, codeGenLang, basePath, pathSeparator, namespace);
    var generatedTables = mudTableGenerator.GenerateAllTables();
    outputFiles(generatedTables);
}

function outputFiles(generatedFiles: Nethereum.Generators.Core.GeneratedFile[]) {
    for (var i = 0; i < generatedFiles.length; i++) {
        outputFile(generatedFiles[i]);
    }
}

function outputFile(generatedFile: Nethereum.Generators.Core.GeneratedFile) {

    fsex.ensureDirSync(generatedFile.get_OutputFolder());
    var fullPath = path.join(generatedFile.get_OutputFolder(), generatedFile.get_FileName());

    if (fs.existsSync(fullPath)) {
        fs.unlinkSync(fullPath);
    }
    fs.writeFileSync(fullPath, generatedFile.get_GeneratedCode());
}

export function  generateNetStandardClassLibrary(projectName: string, basePath: string, codeLang: int) {
    var projectGenerator = new Nethereum.Generators.NetStandardLibraryGenerator(projectName, codeLang);
    var generatedProject = projectGenerator.GenerateFileContent(basePath);
    outputFile(generatedProject);
}

export function generateAllClasses(abi: string, byteCode: string,
    contractName: string,
    baseNamespace: string,
    basePath: string,
    codeGenLang: int
) {

    var serviceNamespace = contractName;
    //Same, we are generating single file
    var cqsNamespace = contractName + ".ContractDefinition";
    var dtoNamespace = contractName + ".ContractDefinition";
    var pathSeparator = path.sep;
    generateAllClassesInternal(abi,
        byteCode,
        contractName,
        baseNamespace,
        serviceNamespace,
        cqsNamespace,
        dtoNamespace,
        basePath,
        pathSeparator,
        codeGenLang);
}

export function generateMudService(abi: string, byteCode: string,
    contractName: string,
    baseNamespace: string,
    basePath: string,
    codeGenLang: int
) {

    var serviceNamespace = contractName;
    //Same, we are generating single file
    var cqsNamespace = contractName + ".ContractDefinition";
    var dtoNamespace = contractName + ".ContractDefinition";
    var pathSeparator = path.sep;
    generateMudServiceInternal(abi,
        byteCode,
        contractName,
        baseNamespace,
        serviceNamespace,
        cqsNamespace,
        dtoNamespace,
        basePath,
        pathSeparator,
        codeGenLang);
}

export function generateMudTables(json: string, baseNamespace: string,
    namespace: string,
    basePath: string,
    codeGenLang: int
) {
    var pathSeparator = path.sep;
    generateAllMudTablesInternal(json, baseNamespace, namespace, basePath, pathSeparator, codeGenLang);
}

export function generateUnityRequests(abi: string, byteCode: string,
    contractName: string,
    baseNamespace: string,
    basePath: string
) {

    var serviceNamespace = contractName;
    //Same, we are generating single file
    var cqsNamespace = contractName + ".ContractDefinition";
    var dtoNamespace = contractName + ".ContractDefinition";
    var pathSeparator = path.sep;
    generateAllUnityClassesInternal(abi,
        byteCode,
        contractName,
        baseNamespace,
        serviceNamespace,
        cqsNamespace,
        dtoNamespace,
        basePath,
        pathSeparator
        );
}

