
# Deployment of Contracts to Ethereum

The first step to be able to interact with any contract is to deploy it to the Ethereum chain. 

## The test contract
This is a very simple example of a solidity contract:

```javascript
    contract test { 
               
        uint _multiplier;
               
        function test(uint multiplier){      
             _multiplier = multiplier;
        }
               
        function multiply(uint a) returns(uint d) { 
             return a * _multiplier;
        }
    }
```

The contract named "test" has a  constructor named after the contract (class) and a function multiply.
The function multiply returns the result of the multiplication of a parameter "a" by the value of the "multiplier" provider at time of deployment to the constructor.

### Contract compilation, the Bytecode and the ABI
Before a contract can be deployed it needs to be compiled. Let's quickly see how to do this with Solidity Online or Visual Studio Code

1. Open Visual Studio Code
2. Copy the contract test into a new file and save it as "test.sol"
3. If you don't have the Solidity extension press F1 and type "ext", then search for "solidity" and install it.
4. Now that is installed you can press F1 again type "compile" and 



## Deployment

### Unlocking the account

### The deployment transaction

### Mining it

### The transaction receipt

### Stop mining it

### Verifying it has been deployed

### The whole contract



