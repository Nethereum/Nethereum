"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var fs = require("fs");
var path = require("path");
var fsex = require("fs-extra");
var abiDes = require("./AbiDeserialiser");
var n = require('./Nethereum.Generators.DuoCode.js');
function generateAllClassesInternal(abi, byteCode, contractName, baseNamespace, serviceNamespace, cqsNamespace, dtoNamespace, basePath, pathSeparator, codeGenLang) {
    var contractDes = abiDes.buildContract(abi);
    var classGenerator = new Nethereum.Generators.ContractProjectGenerator(contractDes, contractName, byteCode, baseNamespace, serviceNamespace, cqsNamespace, dtoNamespace, basePath, pathSeparator, codeGenLang);
    var generatedClases = classGenerator.GenerateAllMessagesFileAndService();
    outputFiles(generatedClases);
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
    var serviceNamespace = contractName + ".Service";
    var cqsNamespace = contractName + ".ContractDefinition";
    var dtoNamespace = contractName + ".ContractDefinition";
    var pathSeparator = path.sep;
    generateAllClassesInternal(abi, byteCode, contractName, baseNamespace, serviceNamespace, cqsNamespace, dtoNamespace, basePath, pathSeparator, codeGenLang);
}
exports.generateAllClasses = generateAllClasses;
//# sourceMappingURL=app.js.map