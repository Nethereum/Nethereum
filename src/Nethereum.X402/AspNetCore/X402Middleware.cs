using Microsoft.AspNetCore.Http;
using Nethereum.X402.Facilitator;
using Nethereum.X402.Models;
using Nethereum.X402.Server;
using System.Text.Json;

namespace Nethereum.X402.AspNetCore;

/// <summary>
/// ASP.NET Core middleware for x402 payment processing.
/// Spec Reference: Section 8 - Server Implementation
/// </summary>
public class X402Middleware
{
    private readonly RequestDelegate _next;
    private readonly X402FacilitatorProxyProcessor _processor;
    private const int X402Version = 1;

    public X402Middleware(RequestDelegate next, X402Options options, IFacilitatorClient facilitator)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));

        ArgumentNullException.ThrowIfNull(options, nameof(options));
        ArgumentNullException.ThrowIfNull(facilitator, nameof(facilitator));

        // Validate options
        options.Validate();

        // Initialize the core processor
        _processor = new X402FacilitatorProxyProcessor(facilitator, options.Routes);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Try to find a matching route
        var requirements = _processor.FindMatchingRoute(context.Request.Path, context.Request.Method);

        // If no matching route, pass through to next middleware
        if (requirements == null)
        {
            await _next(context);
            return;
        }

        // Check for X-PAYMENT header
        if (!context.Request.Headers.TryGetValue("X-PAYMENT", out var paymentHeader) ||
            string.IsNullOrWhiteSpace(paymentHeader))
        {
            // No payment provided - return 402 with payment requirements
            await Return402WithRequirements(context, requirements, "X-PAYMENT header is required");
            return;
        }

        // Decode payment payload
        PaymentPayload payment;
        try
        {
            payment = X402FacilitatorProxyProcessor.DecodePaymentHeader(paymentHeader!);
        }
        catch (ArgumentException ex)
        {
            // Invalid payment payload
            await Return402WithRequirements(context, requirements, $"Invalid payment payload: {ex.Message}");
            return;
        }

        // Verify payment
        VerificationResponse verificationResponse;
        try
        {
            verificationResponse = await _processor.VerifyPaymentAsync(payment, requirements, context.RequestAborted);
        }
        catch (Exception ex)
        {
            // Facilitator error
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            var errorJson = JsonSerializer.Serialize(new
            {
                error = "Facilitator service error",
                details = ex.Message
            });
            await context.Response.WriteAsync(errorJson, context.RequestAborted);
            return;
        }

        if (!verificationResponse.IsValid)
        {
            // Payment verification failed
            await Return402WithRequirements(
                context,
                requirements,
                verificationResponse.InvalidReason ?? "Payment verification failed",
                verificationResponse.Payer);
            return;
        }

        // Payment verified - intercept the response to settle after endpoint execution
        var originalResponseBody = context.Response.Body;
        using var responseBuffer = new MemoryStream();
        context.Response.Body = responseBuffer;

        // Call next middleware / endpoint
        await _next(context);

        // Check response status
        if (context.Response.StatusCode >= 400)
        {
            // Endpoint returned error - skip settlement, return response as-is
            responseBuffer.Seek(0, SeekOrigin.Begin);
            await responseBuffer.CopyToAsync(originalResponseBody, context.RequestAborted);
            context.Response.Body = originalResponseBody;
            return;
        }

        // Settle payment
        SettlementResponse settlementResponse;
        try
        {
            settlementResponse = await _processor.SettlePaymentAsync(payment, requirements, context.RequestAborted);
        }
        catch (Exception ex)
        {
            // Settlement error - return 402
            context.Response.Body = originalResponseBody;
            await Return402WithRequirements(context, requirements, $"Settlement failed: {ex.Message}");
            return;
        }

        if (!settlementResponse.Success)
        {
            // Settlement failed - return 402
            context.Response.Body = originalResponseBody;
            await Return402WithRequirements(
                context,
                requirements,
                settlementResponse.ErrorReason ?? "Settlement failed");
            return;
        }

        // Settlement successful - add X-PAYMENT-RESPONSE header and return original response
        var settlementHeader = X402FacilitatorProxyProcessor.EncodeSettlementResponse(settlementResponse);
        context.Response.Headers.Append("X-PAYMENT-RESPONSE", settlementHeader);

        responseBuffer.Seek(0, SeekOrigin.Begin);
        await responseBuffer.CopyToAsync(originalResponseBody, context.RequestAborted);
        context.Response.Body = originalResponseBody;
    }

    private static async Task Return402WithRequirements(
        HttpContext context,
        PaymentRequirements requirements,
        string errorMessage,
        string? payer = null)
    {
        context.Response.StatusCode = 402;
        context.Response.ContentType = "application/json";

        var response = new PaymentRequirementsResponse
        {
            X402Version = X402Version,
            Error = errorMessage,
            Accepts = new List<PaymentRequirements> { requirements }
        };

        var json = JsonSerializer.Serialize(response);
        await context.Response.WriteAsync(json, context.RequestAborted);
    }
}
