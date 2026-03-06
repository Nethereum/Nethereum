// SPDX-License-Identifier: MIT
pragma solidity ^0.8.28;

contract ComplexStructRegistry {
    struct Address_ {
        string street;
        string city;
        string postCode;
        string country;
    }

    struct Person {
        string name;
        uint256 age;
        Address_ addr;
    }

    struct Employee {
        Person person;
        uint256 employeeId;
        string department;
        uint256 salary;
    }

    struct Company {
        string name;
        Employee[] employees;
    }

    Employee[] private _employees;
    Company private _company;
    uint256 private _nextId;

    event EmployeeAdded(uint256 indexed id, Employee employee);
    event CompanyRegistered(string name, uint256 employeeCount);

    function addEmployee(Employee calldata employee) external returns (uint256) {
        uint256 id = _nextId++;
        _employees.push();
        Employee storage stored = _employees[id];
        stored.person.name = employee.person.name;
        stored.person.age = employee.person.age;
        stored.person.addr.street = employee.person.addr.street;
        stored.person.addr.city = employee.person.addr.city;
        stored.person.addr.postCode = employee.person.addr.postCode;
        stored.person.addr.country = employee.person.addr.country;
        stored.employeeId = id;
        stored.department = employee.department;
        stored.salary = employee.salary;
        emit EmployeeAdded(id, stored);
        return id;
    }

    function registerCompany(Company calldata company) external {
        _company.name = company.name;
        delete _company.employees;
        for (uint256 i = 0; i < company.employees.length; i++) {
            _company.employees.push();
            Employee storage e = _company.employees[i];
            e.person.name = company.employees[i].person.name;
            e.person.age = company.employees[i].person.age;
            e.person.addr.street = company.employees[i].person.addr.street;
            e.person.addr.city = company.employees[i].person.addr.city;
            e.person.addr.postCode = company.employees[i].person.addr.postCode;
            e.person.addr.country = company.employees[i].person.addr.country;
            e.employeeId = company.employees[i].employeeId;
            e.department = company.employees[i].department;
            e.salary = company.employees[i].salary;
        }
        emit CompanyRegistered(company.name, company.employees.length);
    }

    function getEmployee(uint256 id) external view returns (Employee memory) {
        require(id < _employees.length, "Employee not found");
        return _employees[id];
    }

    function getAllEmployees() external view returns (Employee[] memory) {
        return _employees;
    }

    function getCompany() external view returns (Company memory) {
        return _company;
    }

    function getEmployeeCount() external view returns (uint256) {
        return _employees.length;
    }

    function getPersonInfo(uint256 id) external view returns (Person memory) {
        require(id < _employees.length, "Employee not found");
        return _employees[id].person;
    }

    function getAddress(uint256 id) external view returns (Address_ memory) {
        require(id < _employees.length, "Employee not found");
        return _employees[id].person.addr;
    }
}
