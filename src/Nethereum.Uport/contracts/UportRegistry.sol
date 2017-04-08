pragma solidity 0.4.9;

contract UportRegistry{
  uint public version;
  address public previousPublishedVersion;
  mapping(bytes32 => mapping(address => mapping(address => bytes32))) public registry;

  function UportRegistry(address _previousPublishedVersion) {
    version = 3;
    previousPublishedVersion = _previousPublishedVersion;
  }

  event Set(
    bytes32 indexed registrationIdentifier,
    address indexed issuer,
    address indexed subject,
    uint updatedAt);

  //create or update
  function set(bytes32 registrationIdentifier, address subject, bytes32 value){
      Set(registrationIdentifier, msg.sender, subject, now);
      registry[registrationIdentifier][msg.sender][subject] = value;
  }

  function get(bytes32 registrationIdentifier, address issuer, address subject) constant returns(bytes32){
      return registry[registrationIdentifier][issuer][subject];
  }
}
