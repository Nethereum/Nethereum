using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Nethereum.X402.Facilitator;
using Nethereum.X402.Models;
using Nethereum.X402.Server;
using System.Text;
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
    private readonly long _maxBufferedResponseBytes;
    private readonly bool _replayProtectionEnabled;
    private readonly TimeSpan _replayRetention;
    private readonly IPaymentReplayStore _fallbackReplayStore = new InMemoryPaymentReplayStore();
    private const int X402Version = 2;

    public X402Middleware(RequestDelegate next, X402Options options, IFacilitatorClient facilitator)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));

        ArgumentNullException.ThrowIfNull(options, nameof(options));
        ArgumentNullException.ThrowIfNull(facilitator, nameof(facilitator));

        // Validate options
        options.Validate();

        _maxBufferedResponseBytes = options.MaxBufferedResponseBytes;
        _replayProtectionEnabled = options.EnablePaymentReplayProtection;
        _replayRetention = options.PaymentReplayRetention;

        // Initialize the core processor
        _processor = new X402FacilitatorProxyProcessor(facilitator, options.Routes);
    }

    private static ILogger? GetLogger(HttpContext context) =>
        context.RequestServices?.GetService(typeof(ILogger<X402Middleware>)) as ILogger;

    private IPaymentReplayStore GetReplayStore(HttpContext context) =>
        context.RequestServices?.GetService(typeof(IPaymentReplayStore)) as IPaymentReplayStore ?? _fallbackReplayStore;

    public async Task InvokeAsync(HttpContext context)
    {
        // Try to find a matching route
        var routeConfig = _processor.FindMatchingRoute(context.Request.Path, context.Request.Method);

        // If no matching route, pass through to next middleware
        if (routeConfig == null)
        {
            await _next(context);
            return;
        }

        var requirements = routeConfig.Requirements;

        if (!context.Request.Headers.TryGetValue(X402Headers.PaymentSignature, out var paymentHeader) ||
            string.IsNullOrWhiteSpace(paymentHeader))
        {
            // No payment provided - return 402 with payment requirements
            await Return402WithRequirements(context, routeConfig, X402ErrorCodes.PaymentRequired);
            return;
        }

        // Decode payment payload
        PaymentPayload payment;
        try
        {
            payment = X402FacilitatorProxyProcessor.DecodePaymentHeader(paymentHeader!);
        }
        catch (ArgumentException)
        {
            await Return402WithRequirements(context, routeConfig, X402ErrorCodes.InvalidPayload);
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
            GetLogger(context)?.LogError(ex, "x402 facilitator verify failed");
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = X402ErrorCodes.FacilitatorError }), context.RequestAborted);
            return;
        }

        if (!verificationResponse.IsValid)
        {
            // Payment verification failed
            await Return402WithRequirements(
                context,
                routeConfig,
                verificationResponse.InvalidReason ?? X402ErrorCodes.UnexpectedVerifyError,
                verificationResponse.Payer);
            return;
        }

        if (_replayProtectionEnabled)
        {
            var replayKey = PaymentReplayIdentity.Extract(payment);
            if (replayKey != null)
            {
                var reserved = await GetReplayStore(context)
                    .TryReserveAsync(replayKey, _replayRetention, context.RequestAborted);
                if (!reserved)
                {
                    GetLogger(context)?.LogWarning("x402 rejected a replayed payment for {Payer}", verificationResponse.Payer);
                    await Return402WithRequirements(
                        context, routeConfig, X402ErrorCodes.NonceAlreadyUsed, verificationResponse.Payer);
                    return;
                }
            }
            else
            {
                GetLogger(context)?.LogWarning("x402 replay protection: could not derive a payment identity; skipping reservation");
            }
        }

        var originalResponseBody = context.Response.Body;
        using var responseBuffer = new MemoryStream();
        context.Response.Body = new BoundedWriteStream(responseBuffer, _maxBufferedResponseBytes);

        // Call next middleware / endpoint
        try
        {
            await _next(context);
        }
        catch (ResponseTooLargeException)
        {
            context.Response.Body = originalResponseBody;
            GetLogger(context)?.LogWarning("x402 protected response exceeded the {Max}-byte buffer cap", _maxBufferedResponseBytes);
            if (!context.Response.HasStarted)
            {
                context.Response.Clear();
                context.Response.StatusCode = 500;
            }
            return;
        }

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
            GetLogger(context)?.LogError(ex, "x402 settlement failed");
            context.Response.Body = originalResponseBody;
            await Return402WithRequirements(context, routeConfig, X402ErrorCodes.UnexpectedSettleError);
            return;
        }

        if (!settlementResponse.Success)
        {
            // Settlement failed - return 402
            context.Response.Body = originalResponseBody;
            await Return402WithRequirements(
                context,
                routeConfig,
                settlementResponse.ErrorReason ?? X402ErrorCodes.UnexpectedSettleError);
            return;
        }

        var settlementHeader = X402FacilitatorProxyProcessor.EncodeSettlementResponse(settlementResponse);
        context.Response.Headers.Append(X402Headers.PaymentResponse, settlementHeader);

        responseBuffer.Seek(0, SeekOrigin.Begin);
        await responseBuffer.CopyToAsync(originalResponseBody, context.RequestAborted);
        context.Response.Body = originalResponseBody;
    }

    private static async Task Return402WithRequirements(
        HttpContext context,
        RoutePaymentConfig routeConfig,
        string errorMessage,
        string? payer = null)
    {
        context.Response.StatusCode = 402;
        context.Response.ContentType = "application/json";

        var resource = new ResourceInfo
        {
            Url = string.IsNullOrEmpty(routeConfig.Resource?.Url)
                ? $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}"
                : routeConfig.Resource!.Url,
            Description = routeConfig.Resource?.Description,
            MimeType = routeConfig.Resource?.MimeType
        };

        var response = new PaymentRequired
        {
            X402Version = X402Version,
            Error = errorMessage,
            Resource = resource,
            Accepts = new List<PaymentRequirements> { routeConfig.Requirements }
        };

        var json = JsonSerializer.Serialize(response);

        context.Response.Headers.Append(
            X402Headers.PaymentRequired,
            Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));

        await context.Response.WriteAsync(json, context.RequestAborted);
    }

    private sealed class ResponseTooLargeException : Exception { }

    private sealed class BoundedWriteStream : Stream
    {
        private readonly Stream _inner;
        private readonly long _limit;
        private long _written;

        public BoundedWriteStream(Stream inner, long limit)
        {
            _inner = inner;
            _limit = limit;
        }

        private void Track(long count)
        {
            _written += count;
            if (_written > _limit) throw new ResponseTooLargeException();
        }

        public override bool CanWrite => true;
        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }

        public override void Write(byte[] buffer, int offset, int count) { Track(count); _inner.Write(buffer, offset, count); }
        public override void Write(ReadOnlySpan<byte> buffer) { Track(buffer.Length); _inner.Write(buffer); }
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) { Track(count); return _inner.WriteAsync(buffer, offset, count, cancellationToken); }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) { Track(buffer.Length); return _inner.WriteAsync(buffer, cancellationToken); }
        public override void Flush() => _inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
    }
}
