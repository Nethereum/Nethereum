"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var n = require('./Nethereum.Generators.DuoCode.js');
var functionAbi = Nethereum.Generators.Model.FunctionABI;
var eventAbi = Nethereum.Generators.Model.EventABI;
var constructorAbi = Nethereum.Generators.Model.ConstructorABI;
var contractAbi = Nethereum.Generators.Model.ContractABI;
var parameterAbi = Nethereum.Generators.Model.ParameterABI;
var structAbi = Nethereum.Generators.Model.StructABI;
function buildConstructor(item) {
    var constructorItem = new constructorAbi();
    constructorItem.set_InputParameters(buildFunctionParameters(item.inputs));
    return constructorItem;
}
function buildFunction(item) {
    var functionItem = new functionAbi(item.name, item.constant, false);
    functionItem.set_InputParameters(buildFunctionParameters(item.inputs));
    functionItem.set_OutputParameters(buildFunctionParameters(item.outputs));
    return functionItem;
}
function buildEvent(item) {
    var eventItem = new eventAbi(item.name);
    eventItem.set_InputParameters(buildEventParameters(item.inputs));
    return eventItem;
}
function buildStructsFromParameters(items) {
    var structs = [];
    for (var i = 0, len = items.length; i < len; i++) {
        if (items[i].type.startsWith("tuple")) {
            structs = structs.concat(buildStructsFromTuple(items[i]));
        }
    }
    return structs;
}
function buildStructsFromTuple(item) {
    var structs = [];
    var struct = new structAbi(item.name);
    var parameterOrder = 0;
    var parameters = [];
    for (var x = 0, len = item.components.length; x < len; x++) {
        var component = item.components[x];
        parameterOrder = parameterOrder + 1;
        if (component.type.startsWith("tuple")) {
            // simple hack until 0.5.8 type name is the same as the parameter name
            var parameter = new parameterAbi.ctor$1(component.type, component.name, parameterOrder, component.name);
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
            // simple hack until 0.5.8 type name is the same as the parameter name
            var parameter = new parameterAbi.ctor$1(items[i].type, items[i].name, parameterOrder, items[i].name);
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
            // simple hack until 0.5.8 type name is the same as the parameter name
            var parameter = new parameterAbi.ctor$1(items[i].type, items[i].name, parameterOrder, items[i].name);
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
    var structs = [];
    var constructor = new constructorAbi();
    for (var i = 0, len = abi.length; i < len; i++) {
        if (abi[i].type === "function") {
            functions.push(buildFunction(abi[i]));
            var temp = buildStructsFromParameters(abi[i].outputs);
            var _loop_1 = function (item) {
                if (!structs.some(function (x) { return x.get_Name() === item.get_Name(); })) {
                    structs.push(item);
                }
            };
            for (var _i = 0, temp_1 = temp; _i < temp_1.length; _i++) {
                var item = temp_1[_i];
                _loop_1(item);
            }
        }
        if (abi[i].type === "event") {
            events.push(buildEvent(abi[i]));
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
    var contract = new contractAbi();
    contract.set_Constructor(constructor);
    contract.set_Functions(functions);
    contract.set_Events(events);
    contract.set_Structs(structs);
    return contract;
}
exports.buildContract = buildContract;
//# sourceMappingURL=AbiDeserialiser.js.map