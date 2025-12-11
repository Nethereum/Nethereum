using Microsoft.AspNetCore.Mvc;
using Moq;
using Nethereum.X402.Facilitator;
using Nethereum.X402.Models;
using Nethereum.X402.Processors;

namespace Nethereum.X402.IntegrationTests.Facilitator;

/// <summary>
/// BDD tests for FacilitatorController implementation
///
/// Traceability:
/// - Spec: Section 7, Facilitator API
/// - Use Cases: UC-F1 through UC-F4
/// - Requirements: ASP.NET controller for verify, settle, and supported endpoints
/// - Implementation: src/Nethereum.X402/Facilitator/FacilitatorController.cs
/// </summary>
public class FacilitatorControllerTests
{
    /// <summary>
    /// Spec: Section 7 - Controller initialization with processor
    /// Use Case: UC-F1 Scenario 1
    /// </summary>
    [Fact]
    public void Given_ValidProcessor_When_CreatingController_Then_ControllerIsCreated()
    {
        // Arrange
        var mockProcessor = new Mock<IX402PaymentProcessor>();

        // Act
        var controller = new FacilitatorController(mockProcessor.Object);

        // Assert
        Assert.NotNull(controller);
    }

    /// <summary>
    /// Spec: Section 7 - Null parameter validation
    /// Use Case: UC-F1 Scenario 2
    /// </summary>
    [Fact]
    public void Given_NullProcessor_When_CreatingController_Then_ArgumentNullExceptionIsThrown()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new FacilitatorController(null!));
    }

    /// <summary>
    /// Spec: Section 7.1 - POST /facilitator/verify endpoint
    /// Use Case: UC-F2 Scenario 1
    /// </summary>
    [Fact]
    public async Task Given_ValidPayment_When_Verifying_Then_SuccessResponseIsReturned()
    {
        // Arrange
        var mockProcessor = new Mock<IX402PaymentProcessor>();
        var expectedResponse = new VerificationResponse
        {
            IsValid = true,
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        mockProcessor
            .Setup(p => p.VerifyPaymentAsync(
                It.IsAny<PaymentPayload>(),
                It.IsAny<PaymentRequirements>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var controller = new FacilitatorController(mockProcessor.Object);
        var request = new FacilitatorVerifyRequest
        {
            PaymentPayload = CreateTestPaymentPayload(),
            PaymentRequirements = CreateTestPaymentRequirements()
        };

        // Act
        var result = await controller.Verify(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<VerificationResponse>(okResult.Value);
        Assert.True(response.IsValid);
        Assert.Equal("0x857b06519E91e3A54538791bDbb0E22373e36b66", response.Payer);
    }

    /// <summary>
    /// Spec: Section 7.1 - POST /verify with invalid payment
    /// Use Case: UC-F2 Scenario 2
    /// </summary>
    [Fact]
    public async Task Given_InvalidPayment_When_Verifying_Then_FailureResponseIsReturned()
    {
        // Arrange
        var mockProcessor = new Mock<IX402PaymentProcessor>();
        var expectedResponse = new VerificationResponse
        {
            IsValid = false,
            InvalidReason = X402ErrorCodes.InvalidSignature,
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        mockProcessor
            .Setup(p => p.VerifyPaymentAsync(
                It.IsAny<PaymentPayload>(),
                It.IsAny<PaymentRequirements>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var controller = new FacilitatorController(mockProcessor.Object);
        var request = new FacilitatorVerifyRequest
        {
            PaymentPayload = CreateTestPaymentPayload(),
            PaymentRequirements = CreateTestPaymentRequirements()
        };

        // Act
        var result = await controller.Verify(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<VerificationResponse>(okResult.Value);
        Assert.False(response.IsValid);
        Assert.Equal(X402ErrorCodes.InvalidSignature, response.InvalidReason);
    }

    /// <summary>
    /// Spec: Section 7.1 - Request validation
    /// Use Case: UC-F2 Scenario 3
    /// </summary>
    [Fact]
    public async Task Given_NullRequest_When_Verifying_Then_BadRequestIsReturned()
    {
        // Arrange
        var mockProcessor = new Mock<IX402PaymentProcessor>();
        var controller = new FacilitatorController(mockProcessor.Object);

        // Act
        var result = await controller.Verify(null!);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    /// <summary>
    /// Spec: Section 7.1 - Request validation
    /// Use Case: UC-F2 Scenario 3
    /// </summary>
    [Fact]
    public async Task Given_NullPaymentPayload_When_Verifying_Then_BadRequestIsReturned()
    {
        // Arrange
        var mockProcessor = new Mock<IX402PaymentProcessor>();
        var controller = new FacilitatorController(mockProcessor.Object);
        var request = new FacilitatorVerifyRequest
        {
            PaymentPayload = null!,
            PaymentRequirements = CreateTestPaymentRequirements()
        };

        // Act
        var result = await controller.Verify(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    /// <summary>
    /// Spec: Section 7.1 - Request validation
    /// Use Case: UC-F2 Scenario 3
    /// </summary>
    [Fact]
    public async Task Given_NullPaymentRequirements_When_Verifying_Then_BadRequestIsReturned()
    {
        // Arrange
        var mockProcessor = new Mock<IX402PaymentProcessor>();
        var controller = new FacilitatorController(mockProcessor.Object);
        var request = new FacilitatorVerifyRequest
        {
            PaymentPayload = CreateTestPaymentPayload(),
            PaymentRequirements = null!
        };

        // Act
        var result = await controller.Verify(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    /// <summary>
    /// Spec: Section 7.2 - POST /facilitator/settle endpoint
    /// Use Case: UC-F3 Scenario 1
    /// </summary>
    [Fact]
    public async Task Given_VerifiedPayment_When_Settling_Then_SuccessResponseIsReturned()
    {
        // Arrange
        var mockProcessor = new Mock<IX402PaymentProcessor>();
        var expectedResponse = new SettlementResponse
        {
            Success = true,
            Transaction = "0xabc123",
            Network = "base-sepolia",
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        mockProcessor
            .Setup(p => p.SettlePaymentAsync(
                It.IsAny<PaymentPayload>(),
                It.IsAny<PaymentRequirements>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var controller = new FacilitatorController(mockProcessor.Object);
        var request = new FacilitatorSettleRequest
        {
            PaymentPayload = CreateTestPaymentPayload(),
            PaymentRequirements = CreateTestPaymentRequirements()
        };

        // Act
        var result = await controller.Settle(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SettlementResponse>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal("0xabc123", response.Transaction);
    }

    /// <summary>
    /// Spec: Section 7.2 - POST /settle with failed settlement
    /// Use Case: UC-F3 Scenario 2
    /// </summary>
    [Fact]
    public async Task Given_FailedSettlement_When_Settling_Then_FailureResponseIsReturned()
    {
        // Arrange
        var mockProcessor = new Mock<IX402PaymentProcessor>();
        var expectedResponse = new SettlementResponse
        {
            Success = false,
            ErrorReason = X402ErrorCodes.InsufficientFunds,
            Transaction = "0x",
            Network = "base-sepolia",
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        mockProcessor
            .Setup(p => p.SettlePaymentAsync(
                It.IsAny<PaymentPayload>(),
                It.IsAny<PaymentRequirements>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var controller = new FacilitatorController(mockProcessor.Object);
        var request = new FacilitatorSettleRequest
        {
            PaymentPayload = CreateTestPaymentPayload(),
            PaymentRequirements = CreateTestPaymentRequirements()
        };

        // Act
        var result = await controller.Settle(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SettlementResponse>(okResult.Value);
        Assert.False(response.Success);
        Assert.Equal(X402ErrorCodes.InsufficientFunds, response.ErrorReason);
    }

    /// <summary>
    /// Spec: Section 7.2 - Request validation
    /// Use Case: UC-F3 Scenario 3
    /// </summary>
    [Fact]
    public async Task Given_NullRequest_When_Settling_Then_BadRequestIsReturned()
    {
        // Arrange
        var mockProcessor = new Mock<IX402PaymentProcessor>();
        var controller = new FacilitatorController(mockProcessor.Object);

        // Act
        var result = await controller.Settle(null!);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    /// <summary>
    /// Spec: Section 7.2 - Request validation
    /// Use Case: UC-F3 Scenario 3
    /// </summary>
    [Fact]
    public async Task Given_NullPaymentPayload_When_Settling_Then_BadRequestIsReturned()
    {
        // Arrange
        var mockProcessor = new Mock<IX402PaymentProcessor>();
        var controller = new FacilitatorController(mockProcessor.Object);
        var request = new FacilitatorSettleRequest
        {
            PaymentPayload = null!,
            PaymentRequirements = CreateTestPaymentRequirements()
        };

        // Act
        var result = await controller.Settle(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    /// <summary>
    /// Spec: Section 7.2 - Request validation
    /// Use Case: UC-F3 Scenario 3
    /// </summary>
    [Fact]
    public async Task Given_NullPaymentRequirements_When_Settling_Then_BadRequestIsReturned()
    {
        // Arrange
        var mockProcessor = new Mock<IX402PaymentProcessor>();
        var controller = new FacilitatorController(mockProcessor.Object);
        var request = new FacilitatorSettleRequest
        {
            PaymentPayload = CreateTestPaymentPayload(),
            PaymentRequirements = null!
        };

        // Act
        var result = await controller.Settle(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    /// <summary>
    /// Spec: Section 7.3 - GET /facilitator/supported endpoint
    /// Use Case: UC-F4 Scenario 1
    /// </summary>
    [Fact]
    public async Task Given_Processor_When_GettingSupported_Then_SupportedKindsAreReturned()
    {
        // Arrange
        var mockProcessor = new Mock<IX402PaymentProcessor>();
        var expectedResponse = new SupportedPaymentKindsResponse
        {
            Kinds = new List<PaymentKind>
            {
                new() { X402Version = 1, Scheme = "exact", Network = "base-sepolia" },
                new() { X402Version = 1, Scheme = "exact", Network = "ethereum-mainnet" }
            }
        };

        mockProcessor
            .Setup(p => p.GetSupportedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var controller = new FacilitatorController(mockProcessor.Object);

        // Act
        var result = await controller.GetSupported();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SupportedPaymentKindsResponse>(okResult.Value);
        Assert.NotNull(response.Kinds);
        Assert.Equal(2, response.Kinds.Count);
        Assert.Equal("exact", response.Kinds[0].Scheme);
        Assert.Equal("base-sepolia", response.Kinds[0].Network);
    }

    /// <summary>
    /// Spec: Section 7.3 - GET /supported with empty list
    /// Use Case: UC-F4 Scenario 2
    /// </summary>
    [Fact]
    public async Task Given_ProcessorWithNoSupport_When_GettingSupported_Then_EmptyArrayIsReturned()
    {
        // Arrange
        var mockProcessor = new Mock<IX402PaymentProcessor>();
        var expectedResponse = new SupportedPaymentKindsResponse
        {
            Kinds = new List<PaymentKind>()
        };

        mockProcessor
            .Setup(p => p.GetSupportedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var controller = new FacilitatorController(mockProcessor.Object);

        // Act
        var result = await controller.GetSupported();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SupportedPaymentKindsResponse>(okResult.Value);
        Assert.NotNull(response.Kinds);
        Assert.Empty(response.Kinds);
    }

    /// <summary>
    /// Spec: Section 7 - Cancellation token support
    /// Use Case: UC-F2 Scenario 4
    /// </summary>
    [Fact]
    public async Task Given_CancellationToken_When_Verifying_Then_TokenIsPassedToProcessor()
    {
        // Arrange
        var mockProcessor = new Mock<IX402PaymentProcessor>();
        var expectedResponse = new VerificationResponse
        {
            IsValid = true,
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        CancellationToken capturedToken = default;
        mockProcessor
            .Setup(p => p.VerifyPaymentAsync(
                It.IsAny<PaymentPayload>(),
                It.IsAny<PaymentRequirements>(),
                It.IsAny<CancellationToken>()))
            .Callback<PaymentPayload, PaymentRequirements, CancellationToken>((_, _, token) =>
            {
                capturedToken = token;
            })
            .ReturnsAsync(expectedResponse);

        var controller = new FacilitatorController(mockProcessor.Object);
        var request = new FacilitatorVerifyRequest
        {
            PaymentPayload = CreateTestPaymentPayload(),
            PaymentRequirements = CreateTestPaymentRequirements()
        };
        var cts = new CancellationTokenSource();

        // Act
        await controller.Verify(request, cts.Token);

        // Assert
        Assert.Equal(cts.Token, capturedToken);
    }

    /// <summary>
    /// Spec: Section 7 - Cancellation token support
    /// Use Case: UC-F3 Scenario 4
    /// </summary>
    [Fact]
    public async Task Given_CancellationToken_When_Settling_Then_TokenIsPassedToProcessor()
    {
        // Arrange
        var mockProcessor = new Mock<IX402PaymentProcessor>();
        var expectedResponse = new SettlementResponse
        {
            Success = true,
            Transaction = "0xabc123",
            Network = "base-sepolia",
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        CancellationToken capturedToken = default;
        mockProcessor
            .Setup(p => p.SettlePaymentAsync(
                It.IsAny<PaymentPayload>(),
                It.IsAny<PaymentRequirements>(),
                It.IsAny<CancellationToken>()))
            .Callback<PaymentPayload, PaymentRequirements, CancellationToken>((_, _, token) =>
            {
                capturedToken = token;
            })
            .ReturnsAsync(expectedResponse);

        var controller = new FacilitatorController(mockProcessor.Object);
        var request = new FacilitatorSettleRequest
        {
            PaymentPayload = CreateTestPaymentPayload(),
            PaymentRequirements = CreateTestPaymentRequirements()
        };
        var cts = new CancellationTokenSource();

        // Act
        await controller.Settle(request, cts.Token);

        // Assert
        Assert.Equal(cts.Token, capturedToken);
    }

    /// <summary>
    /// Spec: Section 7 - Cancellation token support
    /// Use Case: UC-F4 Scenario 3
    /// </summary>
    [Fact]
    public async Task Given_CancellationToken_When_GettingSupported_Then_TokenIsPassedToProcessor()
    {
        // Arrange
        var mockProcessor = new Mock<IX402PaymentProcessor>();
        var expectedResponse = new SupportedPaymentKindsResponse
        {
            Kinds = new List<PaymentKind>()
        };

        CancellationToken capturedToken = default;
        mockProcessor
            .Setup(p => p.GetSupportedAsync(It.IsAny<CancellationToken>()))
            .Callback<CancellationToken>(token =>
            {
                capturedToken = token;
            })
            .ReturnsAsync(expectedResponse);

        var controller = new FacilitatorController(mockProcessor.Object);
        var cts = new CancellationTokenSource();

        // Act
        await controller.GetSupported(cts.Token);

        // Assert
        Assert.Equal(cts.Token, capturedToken);
    }

    private PaymentPayload CreateTestPaymentPayload()
    {
        return new PaymentPayload
        {
            Scheme = "exact",
            Payload = new ExactSchemePayload
            {
                Signature = "0xsignature",
                Authorization = new Nethereum.X402.Models.Authorization
                {
                    From = "0x857b06519E91e3A54538791bDbb0E22373e36b66",
                    To = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
                    Value = "10000",
                    ValidAfter = "0",
                    ValidBefore = "1740672154",
                    Nonce = "0xf3746613c2d920b5fdabc0856f2aeb2d4f88ee6037b8cc5d04a71a4462f13480"
                }
            }
        };
    }

    private PaymentRequirements CreateTestPaymentRequirements()
    {
        return new PaymentRequirements
        {
            Scheme = "exact",
            Network = "base-sepolia",
            MaxAmountRequired = "10000",
            Resource = "/api/data",
            Description = "Test resource",
            MimeType = "application/json",
            PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
            MaxTimeoutSeconds = 300,
            Asset = "0x1c7D4B196Cb0C7B01d743Fbc6116a902379C7238"
        };
    }
}
