pragma solidity ^0.4.4;


/*

Temporary Hash Registrar 
========================

This is a simplified version of a hash registrar. It is purporsefully limited:
names cannot be six letters or shorter, new auctions will stop after 4 years
and all ether still locked after 8 years will become unreachable.

The plan is to test the basic features and then move to a new contract in at most
2 years, when some sort of renewal mechanism will be enabled.
*/


import './interface.sol';


/**
 * @title Deed to hold ether in exchange for ownership of a node
 * @dev The deed can be controlled only by the registrar and can only send ether back to the owner.
 */
contract Deed {
    address public registrar;
    address constant burn = 0xdead;
    uint public creationDate;
    address public owner;
    event OwnerChanged(address newOwner);
    event DeedClosed();
    bool active;


    modifier onlyRegistrar {
        if (msg.sender != registrar) throw;
        _;
    }

    modifier onlyActive {
        if (!active) throw;
        _;
    }

    function Deed() {
        registrar = msg.sender;
        creationDate = now;
        active = true;
    }
        
    function setOwner(address newOwner) onlyRegistrar {
        owner = newOwner;
        OwnerChanged(newOwner);
    }

    function setRegistrar(address newRegistrar) onlyRegistrar {
        registrar = newRegistrar;
    }
    
    function setBalance(uint newValue) onlyRegistrar onlyActive payable {
        // Check if it has enough balance to set the value
        if (this.balance < newValue) throw;
        // Send the difference to the owner
        if (!owner.send(this.balance - newValue)) throw;
    }

    /**
     * @dev Close a deed and refund a specified fraction of the bid value
     * @param refundRatio The amount*1/1000 to refund
     */
    function closeDeed(uint refundRatio) onlyRegistrar onlyActive {
        active = false;            
        if (! burn.send(((1000 - refundRatio) * this.balance)/1000)) throw;
        DeedClosed();
        destroyDeed();
    }    

    /**
     * @dev Close a deed and refund a specified fraction of the bid value
     */
    function destroyDeed() {
        if (active) throw;
        if(owner.send(this.balance)) 
            selfdestruct(burn);
        else throw;
    }

    // The default function just receives an amount
    function () payable {}
}

/**
 * @title Registrar
 * @dev The registrar handles the auction process for each subnode of the node it owns.
 */
contract Registrar {
    AbstractENS public ens;
    bytes32 public rootNode;

    mapping (bytes32 => entry) public entries;
    mapping (bytes32 => Deed) public sealedBids;
    
    enum Mode { Open, Auction, Owned, Forbidden }
    uint32 constant auctionLength = 7 days;
    uint32 constant revealPeriod = 24 hours;
    uint32 constant initialAuctionPeriod = 2 weeks;
    uint constant minPrice = 0.01 ether;
    uint public registryCreated;

    event AuctionStarted(bytes32 indexed hash, uint auctionExpiryDate);
    event NewBid(bytes32 indexed hash, uint deposit);
    event BidRevealed(bytes32 indexed hash, address indexed owner, uint value, uint8 status);
    event HashRegistered(bytes32 indexed hash, address indexed owner, uint value, uint now);
    event HashReleased(bytes32 indexed hash, uint value);
    event HashInvalidated(bytes32 indexed hash, string indexed name, uint value, uint now);

    struct entry {
        Mode status;
        Deed deed;
        uint registrationDate;
        uint value;
        uint highestBid;
    }

    modifier onlyOwner(bytes32 _hash) {
        entry h = entries[_hash];
        if (msg.sender != h.deed.owner() || h.status != Mode.Owned) throw;
        _;
    }
    
    /**
     * @dev Constructs a new Registrar, with the provided address as the owner of the root node.
     * @param _ens The address of the ENS
     * @param _rootNode The hash of the rootnode.
     */
    function Registrar(address _ens, bytes32 _rootNode) {
        ens = AbstractENS(_ens);
        rootNode = _rootNode;
        registryCreated = now;
    }

    /**
     * @dev Returns the maximum of two unsigned integers
     * @param a A number to compare
     * @param b A number to compare
     * @return The maximum of two unsigned integers
     */
    function max(uint a, uint b) internal constant returns (uint max) {
        if (a > b)
            return a;
        else
            return b;
    }

    /**
     * @dev Returns the minimum of two unsigned integers
     * @param a A number to compare
     * @param b A number to compare
     * @return The minimum of two unsigned integers
     */
    function  min(uint a, uint b) internal constant returns (uint min) {
        if (a < b)
            return a;
        else
            return b;
    }

    /**
     * @dev Returns the length of a given string
     * @param s The string to measure the length of
     * @return The length of the input string
     */
    function strlen(string s) internal constant returns (uint) {
        // Starting here means the LSB will be the byte we care about
        uint ptr;
        uint end;
        assembly {
            ptr := add(s, 1)
            end := add(mload(s), ptr)
        }
        for (uint len = 0; ptr < end; len++) {
            uint8 b;
            assembly { b := and(mload(ptr), 0xFF) }
            if (b < 0x80) {
                ptr += 1;
            } else if(b < 0xE0) {
                ptr += 2;
            } else if(b < 0xF0) {
                ptr += 3;
            } else if(b < 0xF8) {
                ptr += 4;
            } else if(b < 0xFC) {
                ptr += 5;
            } else {
                ptr += 6;
            }
        }
        return len;
    }

    /**
     * @dev Start an auction for an available hash
     * 
     * Anyone can start an auction by sending an array of hashes that they want to bid for. 
     * Arrays are sent so that someone can open up an auction for X dummy hashes when they 
     * are only really interested in bidding for one. This will increase the cost for an 
     * attacker to simply bid blindly on all new auctions. Dummy auctions that are 
     * open but not bid on are closed after a week. 
     *
     * @param _hash The hash to start an auction on
     */    
    function startAuction(bytes32 _hash) {
        entry newAuction = entries[_hash];
        // Ensure the hash is available, and no auction is currently underway
        if ((newAuction.status == Mode.Auction && now < newAuction.registrationDate)
            || newAuction.status == Mode.Owned 
            || newAuction.status == Mode.Forbidden
            || now > registryCreated + 4 years)
            throw;
        
        // for the first month of the registry, make longer auctions
        newAuction.registrationDate = max(now + auctionLength, registryCreated + initialAuctionPeriod);
        newAuction.status = Mode.Auction;  
        newAuction.value = 0;
        newAuction.highestBid = 0;
        AuctionStarted(_hash, newAuction.registrationDate);      
    }

    /**
     * @dev Start multiple auctions for better anonymity
     * @param _hashes An array of hashes, at least one of which you presumably want to bid on
     */
    function startAuctions(bytes32[] _hashes)  {
        for (uint i = 0; i < _hashes.length; i ++ ) {
            startAuction(_hashes[i]);
        }
    }
    
    /**
     * @dev Hash the values required for a secret bid
     * @param hash The node corresponding to the desired namehash
     * @param owner The address which will own the 
     * @param value The bid amount
     * @param salt A random value to ensure secrecy of the bid
     * @return The hash of the bid values
     */
    function shaBid(bytes32 hash, address owner, uint value, bytes32 salt) constant returns (bytes32 sealedBid) {
        return sha3(hash, owner, value, salt);
    }
    
    /**
     * @dev Submit a new sealed bid on a desired hash in a blind auction
     * 
     * Bids are sent by sending a message to the main contract with a hash and an amount. The hash 
     * contains information about the bid, including the bidded hash, the bid amount, and a random 
     * salt. Bids are not tied to any one auction until they are revealed. The value of the bid 
     * itself can be masqueraded by sending more than the value of your actual bid. This is 
     * followed by a 24h reveal period. Bids revealed after this period will be burned and the ether unrecoverable. 
     * Since this is an auction, it is expected that most public hashes, like known domains and common dictionary 
     * words, will have multiple bidders pushing the price up. 
     *
     * @param sealedBid A sealedBid, created by the shaBid function
     */
    function newBid(bytes32 sealedBid) payable {
        if (address(sealedBids[sealedBid]) > 0 ) throw;
        // creates a new hash contract with the owner
        Deed newBid = new Deed();
        sealedBids[sealedBid] = newBid;
        NewBid(sealedBid, msg.value);
        if (!newBid.send(msg.value)) throw;
    } 

    /**
     * @dev Submit the properties of a bid to reveal them
     * @param _hash The node in the sealedBid
     * @param _owner The address in the sealedBid
     * @param _value The bid amount in the sealedBid
     * @param _salt The sale in the sealedBid
     */ 
    function unsealBid(bytes32 _hash, address _owner, uint _value, bytes32 _salt) {
        bytes32 seal = shaBid(_hash, _owner, _value, _salt);
        Deed bid = sealedBids[seal];
        if (address(bid) == 0 ) throw;
        sealedBids[seal] = Deed(0);
        bid.setOwner(_owner);
        entry h = entries[_hash];

        /* 
        A penalty is applied for submitting unrevealed bids, which could otherwise
        be used as a threat of revealing a bid higher than the second-highest 
        bid, to extort the winner into paying them.
        */
        if (bid.creationDate() > h.registrationDate - revealPeriod
            || now > h.registrationDate 
            || _value < minPrice) {
            // bid is invalid, burn 99%
            bid.closeDeed(10);
            BidRevealed(_hash, _owner, _value, 0);
            
        } else if (_value > h.highestBid) {
            // new winner
            // cancel the other bid, refund 99.9%
            if(address(h.deed) != 0) {
                Deed previousWinner = h.deed;
                previousWinner.closeDeed(999);
            }
            
            // set new winner
            // per the rules of a vickery auction, the value becomes the previous highestBid
            h.value = h.highestBid;
            h.highestBid = _value;
            h.deed = bid;
            bid.setBalance(_value);
            BidRevealed(_hash, _owner, _value, 2);
        
        } else if (_value > h.value) {
            // not winner, but affects second place
            h.value = _value;
            bid.closeDeed(999);
            BidRevealed(_hash, _owner, _value, 3);
            
        } else {
            // bid doesn't affect auction
            bid.closeDeed(999);
            BidRevealed(_hash, _owner, _value, 4);
        }
    }
    
    /**
     * @dev Cancel a bid
     * @param seal The value returned by the shaBid function
     */ 
    function cancelBid(bytes32 seal) {
        Deed bid = sealedBids[seal];
        // If the bid hasn't been revealed long after any possible auction date, then close it
        if (address(bid) == 0 
            || now < bid.creationDate() + auctionLength * 12 
            || bid.owner() > 0) throw; 

        // There is a fee for cancelling an old bid, but it's smaller than revealing it
        bid.setOwner(msg.sender);
        bid.closeDeed(5);
        sealedBids[seal] = Deed(0);
        BidRevealed(seal, 0, 0, 5);
    }

    /**
     * @dev Finalize an auction after the registration date has passed
     * @param _hash The hash of the name the auction is for
     */ 
    function finalizeAuction(bytes32 _hash) {
        entry h = entries[_hash];
        if (now < h.registrationDate 
            || h.highestBid == 0
            || h.status != Mode.Auction) throw;
        
        // set the hash
        h.status = Mode.Owned;
        h.value =  max(h.value, minPrice);

        // Assign the owner in ENS
        ens.setSubnodeOwner(rootNode, _hash, h.deed.owner());

        Deed deedContract = h.deed;
        deedContract.setBalance(h.value);
        HashRegistered(_hash, deedContract.owner(), h.value, now);
    }

    /**
     * @dev The owner of a domain may transfer it to someone else at any time.
     * @param _hash The node to transfer
     * @param newOwner The address to transfer ownership to
     */
    function transfer(bytes32 _hash, address newOwner) onlyOwner(_hash) {
        entry h = entries[_hash];
        h.deed.setOwner(newOwner);
        ens.setSubnodeOwner(rootNode, _hash, newOwner);
    }


    /**
     * @dev After some time, the owner can release the property and get their ether back
     * @param _hash The node to release
     */
    function releaseDeed(bytes32 _hash) onlyOwner(_hash) {
        entry h = entries[_hash];
        Deed deedContract = h.deed;
        if (now < h.registrationDate + 1 years 
            || now > registryCreated + 8 years) throw;
        
        h.status = Mode.Open;
        ens.setSubnodeOwner(rootNode, _hash, 0);
        deedContract.closeDeed(1000);
        HashReleased(_hash, h.value);
    }  

    /**
     * @dev Submit a name 6 characters long or less. If it has been registered, 
     * the submitter will earn 10% of the deed value. We are purposefully
     * handicapping the simplified registrar as a way to force it into being restructured
     * in a few years.
     * @param unhashedName An invalid name to search for in the registry.
     * 
     */
    function invalidateName(string unhashedName) {
        if (strlen(unhashedName) > 6 ) throw;
        bytes32 hash = sha3(unhashedName);
        
        entry h = entries[hash];
        h.status = Mode.Forbidden;
        ens.setSubnodeOwner(rootNode, hash, 0);
        if(address(h.deed) != 0) {
            // Reward the discoverer with 10% of the deed
            // The previous owner gets nothing
            h.deed.setOwner(msg.sender);
            h.deed.closeDeed(100);
        }
        HashInvalidated(hash, unhashedName, h.value, now);
    }

    /**
     * @dev Transfers the deed to the current registrar, if different from this one.
     * Used during the upgrade process to a permanent registrar.
     * @param _hash The name hash to transfer.
     */
    function transferRegistrars(bytes32 _hash) onlyOwner(_hash) {
        var registrar = ens.owner(rootNode);
        if(registrar == address(this))
            throw;

        entry h = entries[_hash];
        h.deed.setRegistrar(registrar);
        h.status = Mode.Forbidden;
    }
}