contract test {
    
    mapping (bytes32=>Document[]) public documents;

    struct Document{
        string name;
        string description;
        address sender;
    }

    function StoreDocument(bytes32 key, string name, string description) returns (bool success) {
       var doc = Document(name, description, msg.sender);
       documents[key].push(doc);
       return true;
    }

    
}