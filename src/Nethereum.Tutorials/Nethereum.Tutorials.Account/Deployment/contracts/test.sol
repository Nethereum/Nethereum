contract test {
    int _multiplier;

    function test(int multiplier){
        _multiplier = multiplier;
    }

    function multiply(int val) returns (int d) {
        return val * _multiplier;    
    }
}