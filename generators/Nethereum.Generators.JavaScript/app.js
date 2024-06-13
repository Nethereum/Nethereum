"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.generateUnityRequests = exports.generateMudTables = exports.generateMudService = exports.generateAllClasses = exports.generateNetStandardClassLibrary = void 0;
const fs = require("fs");
const path = require("path");
const fsex = require("fs-extra");
const abiDes = require("./AbiDeserialiser");
const mudParse = require("./MudWorldParser");
var n = require('./Nethereum.Generators.DuoCode.js');
function generateAllClassesInternal(abi, byteCode, contractName, baseNamespace, serviceNamespace, cqsNamespace, dtoNamespace, basePath, pathSeparator, codeGenLang) {
    var contractDes = abiDes.buildContract(abi);
    var classGenerator = new Nethereum.Generators.ContractProjectGenerator(contractDes, contractName, byteCode, baseNamespace, serviceNamespace, cqsNamespace, dtoNamespace, basePath, pathSeparator, codeGenLang);
    classGenerator.set_AddRootNamespaceOnVbProjectsToImportStatements(false);
    var generatedClases = classGenerator.GenerateAllMessagesFileAndService();
    outputFiles(generatedClases);
}
function generateMudServiceInternal(abi, byteCode, contractName, baseNamespace, serviceNamespace, cqsNamespace, dtoNamespace, basePath, pathSeparator, codeGenLang) {
    var contractDes = abiDes.buildContract(abi);
    var classGenerator = new Nethereum.Generators.ContractProjectGenerator(contractDes, contractName, byteCode, baseNamespace, serviceNamespace, cqsNamespace, dtoNamespace, basePath, pathSeparator, codeGenLang);
    classGenerator.set_AddRootNamespaceOnVbProjectsToImportStatements(false);
    var generatedClases = classGenerator.GenerateMudService();
    outputFiles([generatedClases]);
}
function generateAllUnityClassesInternal(abi, byteCode, contractName, baseNamespace, serviceNamespace, cqsNamespace, dtoNamespace, basePath, pathSeparator) {
    var contractDes = abiDes.buildContract(abi);
    var classGenerator = new Nethereum.Generators.ContractProjectGenerator(contractDes, contractName, byteCode, baseNamespace, serviceNamespace, cqsNamespace, dtoNamespace, basePath, pathSeparator, 0);
    var generatedClases = classGenerator.GenerateAllUnity();
    outputFiles(generatedClases);
}
function generateAllMudTablesInternal(json, baseNamespace, namespace, basePath, pathSeparator, codeGenLang) {
    var tables = mudParse.extractTables(json);
    var mudTableGenerator = new Nethereum.Generators.MudTablesGenerator(tables, baseNamespace, codeGenLang, basePath, pathSeparator, namespace);
    var generatedTables = mudTableGenerator.GenerateAllTables();
    outputFiles(generatedTables);
}
function outputFiles(generatedFiles) {
    for (var i = 0; i < generatedFiles.length; i++) {
        outputFile(generatedFiles[i]);
    }
}
function outputFile(generatedFile) {
    fsex.ensureDirSync(generatedFile.get_OutputFolder());
    var fullPath = path.join(generatedFile.get_OutputFolder(), generatedFile.get_FileName());
    if (fs.existsSync(fullPath)) {
        fs.unlinkSync(fullPath);
    }
    fs.writeFileSync(fullPath, generatedFile.get_GeneratedCode());
}
function generateNetStandardClassLibrary(projectName, basePath, codeLang) {
    var projectGenerator = new Nethereum.Generators.NetStandardLibraryGenerator(projectName, codeLang);
    var generatedProject = projectGenerator.GenerateFileContent(basePath);
    outputFile(generatedProject);
}
exports.generateNetStandardClassLibrary = generateNetStandardClassLibrary;
function generateAllClasses(abi, byteCode, contractName, baseNamespace, basePath, codeGenLang) {
    var serviceNamespace = contractName;
    //Same, we are generating single file
    var cqsNamespace = contractName + ".ContractDefinition";
    var dtoNamespace = contractName + ".ContractDefinition";
    var pathSeparator = path.sep;
    generateAllClassesInternal(abi, byteCode, contractName, baseNamespace, serviceNamespace, cqsNamespace, dtoNamespace, basePath, pathSeparator, codeGenLang);
}
exports.generateAllClasses = generateAllClasses;
function generateMudService(abi, byteCode, contractName, baseNamespace, basePath, codeGenLang) {
    var serviceNamespace = contractName;
    //Same, we are generating single file
    var cqsNamespace = contractName + ".ContractDefinition";
    var dtoNamespace = contractName + ".ContractDefinition";
    var pathSeparator = path.sep;
    generateMudServiceInternal(abi, byteCode, contractName, baseNamespace, serviceNamespace, cqsNamespace, dtoNamespace, basePath, pathSeparator, codeGenLang);
}
exports.generateMudService = generateMudService;
function generateMudTables(json, baseNamespace, namespace, basePath, codeGenLang) {
    var pathSeparator = path.sep;
    generateAllMudTablesInternal(json, baseNamespace, namespace, basePath, pathSeparator, codeGenLang);
}
exports.generateMudTables = generateMudTables;
function generateUnityRequests(abi, byteCode, contractName, baseNamespace, basePath) {
    var serviceNamespace = contractName;
    //Same, we are generating single file
    var cqsNamespace = contractName + ".ContractDefinition";
    var dtoNamespace = contractName + ".ContractDefinition";
    var pathSeparator = path.sep;
    generateAllUnityClassesInternal(abi, byteCode, contractName, baseNamespace, serviceNamespace, cqsNamespace, dtoNamespace, basePath, pathSeparator);
}
exports.generateUnityRequests = generateUnityRequests;
//# sourceMappingURL=app.js.map