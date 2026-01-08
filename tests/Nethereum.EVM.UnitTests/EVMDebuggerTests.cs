using Nethereum.ABI.ABIRepository;
using Nethereum.ABI.CompilationMetadata;
using Nethereum.EVM.Debugging;
using Nethereum.EVM.SourceInfo;
using System.Collections.Generic;
using System.Numerics;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    public class EVMDebuggerTests
    {
        private const string SimpleOwnerSource = @"// SPDX-License-Identifier: MIT
pragma solidity >=0.5.0 <0.9.0;
contract SimpleOwner {

    address private _owner;

    constructor()
    {
        _owner = msg.sender;
    }

    function getOwner() public view returns(address owner) {
        return _owner;
    }
}";

        private const string SimpleOwnerBytecode = "6080604052348015600f57600080fd5b506004361060285760003560e01c8063893d20e814602d575b600080fd5b60336047565b604051603e919060ad565b60405180910390f35b60008060009054906101000a900473ffffffffffffffffffffffffffffffffffffffff16905090565b600073ffffffffffffffffffffffffffffffffffffffff82169050919050565b60006099826070565b9050919050565b60a7816090565b82525050565b600060208201905060c0600083018460a0565b9291505056fea2646970667358221220571729c39bd05edb51c651a38d2adafe1822fa3b0e4ae39601f2901a6d95fd3064736f6c63430008090033";

        private const string SimpleOwnerSourceMap = "66:216:0:-:0;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;192:87;;;:::i;:::-;;;;;;;:::i;:::-;;;;;;;;;232:13;265:6;;;;;;;;;;;258:13;;192:87;:::o;7:126:1:-;44:7;84:42;77:5;73:54;62:65;;7:126;;;:::o;139:96::-;176:7;205:24;223:5;205:24;:::i;:::-;194:35;;139:96;;;:::o;241:118::-;328:24;346:5;328:24;:::i;:::-;323:3;316:37;241:118;;:::o;365:222::-;458:4;496:2;485:9;481:18;473:26;;509:71;577:1;566:9;562:17;553:6;509:71;:::i;:::-;365:222;;;;:::o";

        private const string SimpleOwnerABI = @"[{""inputs"":[],""name"":""getOwner"",""outputs"":[{""internalType"":""address"",""name"":""owner"",""type"":""address""}],""stateMutability"":""view"",""type"":""function""}]";

        private ABIInfo CreateTestABIInfo()
        {
            var abiInfo = new ABIInfo
            {
                ABI = SimpleOwnerABI,
                Address = "0x1234567890123456789012345678901234567890",
                ContractName = "SimpleOwner",
                ChainId = 1,
                RuntimeBytecode = SimpleOwnerBytecode,
                RuntimeSourceMap = SimpleOwnerSourceMap,
                SourceFileIndex = new Dictionary<int, string>
                {
                    { 0, "simpleOwner.sol" },
                    { 1, "GeneratedLib.sol" }
                },
                Metadata = new Nethereum.ABI.CompilationMetadata.CompilationMetadata
                {
                    Sources = new Dictionary<string, SourceCode>
                    {
                        {
                            "simpleOwner.sol",
                            new SourceCode { Content = SimpleOwnerSource }
                        }
                    }
                }
            };
            return abiInfo;
        }

        private List<ProgramTrace> CreateMockTrace()
        {
            var trace = new List<ProgramTrace>();
            var instructions = ProgramInstructionsUtils.GetProgramInstructions(SimpleOwnerBytecode);

            for (int i = 0; i < 10 && i < instructions.Count; i++)
            {
                trace.Add(new ProgramTrace
                {
                    ProgramAddress = "0x1234567890123456789012345678901234567890",
                    CodeAddress = "0x1234567890123456789012345678901234567890",
                    VMTraceStep = i,
                    ProgramTraceStep = i,
                    Instruction = instructions[i],
                    Stack = new List<string> { "0x01", "0x02" },
                    Memory = "00" + new string('0', 62),
                    Storage = new Dictionary<string, string>(),
                    Depth = 0
                });
            }

            return trace;
        }

        [Fact]
        public void ShouldCreateDebugSessionFromTrace()
        {
            var abiInfo = CreateTestABIInfo();
            var storage = new ABIInfoInMemoryStorage();
            storage.AddABIInfo(abiInfo);

            var trace = CreateMockTrace();
            var session = new EVMDebuggerSession(storage);
            session.LoadFromTrace(trace, 1);

            Assert.Equal(10, session.TotalSteps);
            Assert.Equal(0, session.CurrentStep);
            Assert.True(session.CanStepForward);
            Assert.False(session.CanStepBack);
        }

        [Fact]
        public void ShouldNavigateForwardAndBackward()
        {
            var abiInfo = CreateTestABIInfo();
            var storage = new ABIInfoInMemoryStorage();
            storage.AddABIInfo(abiInfo);

            var trace = CreateMockTrace();
            var session = new EVMDebuggerSession(storage);
            session.LoadFromTrace(trace, 1);

            Assert.Equal(0, session.CurrentStep);

            session.StepForward();
            Assert.Equal(1, session.CurrentStep);

            session.StepForward();
            Assert.Equal(2, session.CurrentStep);

            session.StepBack();
            Assert.Equal(1, session.CurrentStep);
        }

        [Fact]
        public void ShouldGoToSpecificStep()
        {
            var abiInfo = CreateTestABIInfo();
            var storage = new ABIInfoInMemoryStorage();
            storage.AddABIInfo(abiInfo);

            var trace = CreateMockTrace();
            var session = new EVMDebuggerSession(storage);
            session.LoadFromTrace(trace, 1);

            session.GoToStep(5);
            Assert.Equal(5, session.CurrentStep);

            session.GoToStart();
            Assert.Equal(0, session.CurrentStep);

            session.GoToEnd();
            Assert.Equal(9, session.CurrentStep);
        }

        [Fact]
        public void ShouldGetCurrentTraceInfo()
        {
            var abiInfo = CreateTestABIInfo();
            var storage = new ABIInfoInMemoryStorage();
            storage.AddABIInfo(abiInfo);

            var trace = CreateMockTrace();
            var session = new EVMDebuggerSession(storage);
            session.LoadFromTrace(trace, 1);

            Assert.NotNull(session.CurrentTrace);
            Assert.NotNull(session.CurrentInstruction);
            Assert.NotNull(session.CurrentStack);
            Assert.NotNull(session.CurrentMemory);
        }

        [Fact]
        public void ShouldGetSourceLocation()
        {
            var abiInfo = CreateTestABIInfo();
            var storage = new ABIInfoInMemoryStorage();
            storage.AddABIInfo(abiInfo);

            var trace = CreateMockTrace();
            var session = new EVMDebuggerSession(storage);
            session.LoadFromTrace(trace, 1);

            var sourceLocation = session.GetCurrentSourceLocation();
            if (sourceLocation != null)
            {
                Assert.NotNull(sourceLocation.FilePath);
                Assert.NotNull(sourceLocation.FullFileContent);
            }
        }

        [Fact]
        public void ShouldGenerateDebugString()
        {
            var abiInfo = CreateTestABIInfo();
            var storage = new ABIInfoInMemoryStorage();
            storage.AddABIInfo(abiInfo);

            var trace = CreateMockTrace();
            var session = new EVMDebuggerSession(storage);
            session.LoadFromTrace(trace, 1);

            var debugString = session.ToDebugString();
            Assert.NotNull(debugString);
            Assert.Contains("Step 1/10", debugString);
        }

        [Fact]
        public void ShouldGenerateSummaryString()
        {
            var abiInfo = CreateTestABIInfo();
            var storage = new ABIInfoInMemoryStorage();
            storage.AddABIInfo(abiInfo);

            var trace = CreateMockTrace();
            var session = new EVMDebuggerSession(storage);
            session.LoadFromTrace(trace, 1);

            var summaryString = session.ToSummaryString();
            Assert.NotNull(summaryString);
            Assert.Contains("[1/10]", summaryString);
        }

        [Fact]
        public void ShouldTestSourceMapDecompression()
        {
            var sourceMaps = new SourceMapUtil().UnCompressSourceMap(SimpleOwnerSourceMap);
            Assert.NotEmpty(sourceMaps);

            var firstMap = sourceMaps[0];
            Assert.Equal(66, firstMap.Position);
            Assert.Equal(216, firstMap.Length);
            Assert.Equal(0, firstMap.SourceFile);
        }

        [Fact]
        public void ShouldCreateSourceLocationFromSourceMap()
        {
            var sourceMaps = new SourceMapUtil().UnCompressSourceMap(SimpleOwnerSourceMap);
            var sourceMap = sourceMaps[0];

            var location = SourceLocation.FromSourceMap(sourceMap, "simpleOwner.sol", SimpleOwnerSource);

            Assert.NotNull(location);
            Assert.Equal("simpleOwner.sol", location.FilePath);
            Assert.Equal(66, location.Position);
            Assert.Equal(216, location.Length);
            Assert.True(location.LineNumber >= 1);
            Assert.True(location.ColumnNumber >= 1);
        }

        [Fact]
        public void ShouldGetContextLines()
        {
            var sourceMaps = new SourceMapUtil().UnCompressSourceMap(SimpleOwnerSourceMap);
            var functionMap = sourceMaps[31];

            var location = SourceLocation.FromSourceMap(functionMap, "simpleOwner.sol", SimpleOwnerSource);
            if (location != null)
            {
                var contextLines = location.GetContextLines(2, 2);
                Assert.NotNull(contextLines);
            }
        }

        [Fact]
        public void ShouldHaveDebugInfo()
        {
            var abiInfo = CreateTestABIInfo();
            Assert.True(abiInfo.HasDebugInfo);

            var abiInfoNoDebug = new ABIInfo
            {
                Address = "0x1234",
                ContractName = "Test"
            };
            Assert.False(abiInfoNoDebug.HasDebugInfo);
        }

        [Fact]
        public void ShouldGetSourceContent()
        {
            var abiInfo = CreateTestABIInfo();

            var content = abiInfo.GetSourceContent(0);
            Assert.Equal(SimpleOwnerSource, content);

            var noContent = abiInfo.GetSourceContent(99);
            Assert.Null(noContent);
        }

        [Fact]
        public void ShouldGetSourceFilePath()
        {
            var abiInfo = CreateTestABIInfo();

            var path = abiInfo.GetSourceFilePath(0);
            Assert.Equal("simpleOwner.sol", path);

            var noPath = abiInfo.GetSourceFilePath(99);
            Assert.Null(noPath);
        }

        [Fact]
        public void ShouldTestExtensionMethods()
        {
            var abiInfo = CreateTestABIInfo();
            var storage = new ABIInfoInMemoryStorage();
            storage.AddABIInfo(abiInfo);

            var trace = CreateMockTrace();
            var session = trace.CreateDebugSession(storage, 1);

            Assert.NotNull(session);
            Assert.Equal(10, session.TotalSteps);
        }

        [Fact]
        public void ShouldEnumerateWithSource()
        {
            var abiInfo = CreateTestABIInfo();
            var storage = new ABIInfoInMemoryStorage();
            storage.AddABIInfo(abiInfo);

            var trace = CreateMockTrace();
            var session = new EVMDebuggerSession(storage);
            session.LoadFromTrace(trace, 1);

            int count = 0;
            foreach (var stepInfo in session.EnumerateWithSource())
            {
                Assert.NotNull(stepInfo.Trace);
                count++;
            }

            Assert.Equal(10, count);
        }
    }
}
