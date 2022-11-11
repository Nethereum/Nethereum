"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.buildContract = void 0;
var n = require('./Nethereum.Generators.DuoCode.js');
var functionAbi = Nethereum.Generators.Model.FunctionABI;
var eventAbi = Nethereum.Generators.Model.EventABI;
var constructorAbi = Nethereum.Generators.Model.ConstructorABI;
var contractAbi = Nethereum.Generators.Model.ContractABI;
var parameterAbi = Nethereum.Generators.Model.ParameterABI;
var structAbi = Nethereum.Generators.Model.StructABI;
var errorAbi = Nethereum.Generators.Model.ErrorABI;
function buildConstructor(item) {
    var constructorItem = new constructorAbi();
    constructorItem.set_InputParameters(buildFunctionParameters(item.inputs));
    return constructorItem;
}
function buildFunction(item, contractAbi) {
    var constant = false;
    if (item.constant !== undefined) {
        constant = item.constant;
    }
    else {
        // for solidity >=0.6.0
        if (item.stateMutability !== undefined && (item.stateMutability === "view" || item.stateMutability === "pure"))
            constant = true;
    }
    var functionItem = new functionAbi(item.name, constant, contractAbi, false);
    functionItem.set_InputParameters(buildFunctionParameters(item.inputs));
    functionItem.set_OutputParameters(buildFunctionParameters(item.outputs));
    return functionItem;
}
function buildEvent(item, contractAbi) {
    var eventItem = new eventAbi(item.name, contractAbi);
    eventItem.set_InputParameters(buildEventParameters(item.inputs));
    return eventItem;
}
function buildError(item, contractAbi) {
    var errorItem = new errorAbi(item.name, contractAbi);
    errorItem.set_InputParameters(buildFunctionParameters(item.inputs));
    return errorItem;
}
function getStructTypeName(item) {
    if (item.internalType !== undefined && item.internalType.startsWith("struct")) {
        var length = 'struct '.length;
        var internalTypeName = item.internalType;
        var structName = internalTypeName.substring(length);
        if (structName.indexOf(".") > -1) {
            structName = structName.substring(structName.lastIndexOf(".") + 1);
        }
        if (structName.indexOf("[") > 0) {
            structName = structName.substring(0, structName.indexOf("["));
        }
        return structName;
    }
    // simple hack until 0.5.8 type name is the same as the parameter name
    return item.name;
}
function buildStructsFromParameters(items) {
    var structs = [];
    if (items !== null && items !== undefined) {
        for (var i = 0, len = items.length; i < len; i++) {
            if (items[i].type.startsWith("tuple")) {
                structs = structs.concat(buildStructsFromTuple(items[i]));
            }
        }
    }
    return structs;
}
function buildStructsFromTuple(item) {
    var structs = [];
    var struct = new structAbi(getStructTypeName(item));
    var parameterOrder = 0;
    var parameters = [];
    for (var x = 0, len = item.components.length; x < len; x++) {
        var component = item.components[x];
        parameterOrder = parameterOrder + 1;
        if (component.type.startsWith("tuple")) {
            var parameter = new parameterAbi.ctor$1(component.type, component.name, parameterOrder, getStructTypeName(component));
            structs = structs.concat(buildStructsFromTuple(component));
        }
        else {
            var parameter = new parameterAbi.ctor$1(component.type, component.name, parameterOrder);
        }
        parameters.push(parameter);
    }
    struct.set_InputParameters(parameters);
    structs.push(struct);
    return structs;
}
function buildFunctionParameters(items) {
    var parameterOrder = 0;
    var parameters = [];
    for (var i = 0, len = items.length; i < len; i++) {
        parameterOrder = parameterOrder + 1;
        if (items[i].type.startsWith("tuple")) {
            var parameter = new parameterAbi.ctor$1(items[i].type, items[i].name, parameterOrder, getStructTypeName(items[i]));
        }
        else {
            var parameter = new parameterAbi.ctor$1(items[i].type, items[i].name, parameterOrder);
        }
        parameters.push(parameter);
    }
    return parameters;
}
function buildEventParameters(items) {
    var parameterOrder = 0;
    var parameters = [];
    for (var i = 0, len = items.length; i < len; i++) {
        parameterOrder = parameterOrder + 1;
        if (items[i].type.startsWith("tuple")) {
            var parameter = new parameterAbi.ctor$1(items[i].type, items[i].name, parameterOrder, getStructTypeName(items[i]));
        }
        else {
            var parameter = new parameterAbi.ctor$1(items[i].type, items[i].name, parameterOrder);
        }
        parameter.set_Indexed(items[i].indexed);
        parameters.push(parameter);
    }
    return parameters;
}
function buildContract(abiStr) {
    var abi = JSON.parse(abiStr);
    var functions = [];
    var events = [];
    var errors = [];
    var structs = [];
    var constructor = new constructorAbi();
    var contract = new contractAbi();
    for (var i = 0, len = abi.length; i < len; i++) {
        if (abi[i].type === "function") {
            var functionItem = buildFunction(abi[i], contract);
            if (functionItem.get_Name() == "nonce") {
                var x = 1;
            }
            if (functionItem.get_Constant() && abi[i].outputs.length == 0) {
            }
            else {
                functions.push(functionItem);
                var temp_3 = buildStructsFromParameters(abi[i].outputs);
                var _loop_1 = function (item) {
                    if (!structs.some(function (x) { return x.get_Name() === item.get_Name(); })) {
                        structs.push(item);
                    }
                };
                for (var _i = 0, temp_1 = temp_3; _i < temp_1.length; _i++) {
                    var item = temp_1[_i];
                    _loop_1(item);
                }
            }
        }
        if (abi[i].type === "event") {
            events.push(buildEvent(abi[i], contract));
        }
        if (abi[i].type === "error") {
            errors.push(buildError(abi[i], contract));
        }
        if (abi[i].type === "constructor") {
            constructor = buildConstructor(abi[i]);
        }
        var temp = buildStructsFromParameters(abi[i].inputs);
        var _loop_2 = function (item) {
            if (!structs.some(function (x) { return x.get_Name() === item.get_Name(); })) {
                structs.push(item);
            }
        };
        for (var _a = 0, temp_2 = temp; _a < temp_2.length; _a++) {
            var item = temp_2[_a];
            _loop_2(item);
        }
    }
    contract.set_Constructor(constructor);
    contract.set_Functions(functions);
    contract.set_Events(events);
    contract.set_Errors(errors);
    contract.set_Structs(structs);
    return contract;
}
exports.buildContract = buildContract;
//# sourceMappingURL=AbiDeserialiser.js.map