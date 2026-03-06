namespace Nethereum.Aspire.LoadGenerator.Contracts;

public static class EventEmitterContract
{
    // Minimal contract: constructor + emitEvent() that fires an Event(uint256)
    // pragma solidity ^0.8.0;
    // contract EventEmitter {
    //     event TestEvent(uint256 indexed value, address indexed sender);
    //     uint256 public counter;
    //     function emitEvent() external {
    //         counter++;
    //         emit TestEvent(counter, msg.sender);
    //     }
    // }
    public const string BYTECODE = "0x6080806040523460135760d9908160188239f35b5f80fdfe60808060405260043610156011575f80fd5b5f3560e01c90816361bc221a14608c5750637b0cb83914602f575f80fd5b346088575f3660031901126088575f545f198114607457600101805f5533907f9457b0abc6a87108b750271d78f46ad30369fbeb6a7454888743813252fca3fc5f80a3005b634e487b7160e01b5f52601160045260245ffd5b5f80fd5b346088575f3660031901126088576020905f548152f3fea26469706673582212206acdc646c3663ed20c87c51f6800aced89a7d3f8306555718f0725c18a3e560264736f6c634300081c0033";

    public const string ABI = @"[{""inputs"":[],""name"":""emitEvent"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[],""name"":""counter"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""}]";
}
