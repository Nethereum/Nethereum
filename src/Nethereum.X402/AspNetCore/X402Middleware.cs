using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Nethereum.X402.Facilitator;
using Nethereum.X402.Models;
using Nethereum.X402.Server;
using System.Text;
using System.Text.Json;

namespace Nethereum.X402.AspNetCore;

public class X402Middleware
{
    private readonly RequestDelegate _next;
    private readonly X402FacilitatorProxyProcessor _processor;
    private readonly long _maxBufferedResponseBytes;
    private readonly bool _replayProtectionEnabled;
    private readonly TimeSpan _replayRetention;
    private readonly IPaymentReplayStore _fallbackReplayStore = new InMemoryPaymentReplayStore();
    private const string PaymentRequiredCacheControl = "no-store";

    public X402Middleware(RequestDelegate next, X402Options options, IFacilitatorClient facilitator)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));

        ArgumentNullException.ThrowIfNull(options, nameof(options));
        ArgumentNullException.ThrowIfNull(facilitator, nameof(facilitator));

        options.Validate();

        _maxBufferedResponseBytes = options.MaxBufferedResponseBytes;
        _replayProtectionEnabled = options.EnablePaymentReplayProtection;
        _replayRetention = options.PaymentReplayRetention;

        _processor = new X402FacilitatorProxyProcessor(facilitator, options.Routes);
    }

    private static ILogger? GetLogger(HttpContext context) =>
        context.RequestServices?.GetService(typeof(ILogger<X402Middleware>)) as ILogger;

    private IPaymentReplayStore GetReplayStore(HttpContext context) =>
        context.RequestServices?.GetService(typeof(IPaymentReplayStore)) as IPaymentReplayStore ?? _fallbackReplayStore;

    public async Task InvokeAsync(HttpContext context)
    {
        var routeConfig = _processor.FindMatchingRoute(context.Request.Path.Value ?? string.Empty, context.Request.Method);

        if (routeConfig == null)
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(X402Headers.PaymentSignature, out var paymentHeader) ||
            string.IsNullOrWhiteSpace(paymentHeader))
        {
            await ReturnPaymentRequired(context, routeConfig, StatusCodes.Status402PaymentRequired, X402ErrorCodes.PaymentRequired);
            return;
        }

        PaymentPayload payment;
        try
        {
            payment = X402FacilitatorProxyProcessor.DecodePaymentHeader(paymentHeader!);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            await ReturnPaymentRequired(context, routeConfig, StatusCodes.Status400BadRequest, X402ErrorCodes.InvalidPayload);
            return;
        }

        var requirements = X402FacilitatorProxyProcessor.FindMatchingRequirements(routeConfig, payment);
        if (requirements == null)
        {
            await ReturnPaymentRequired(context, routeConfig, StatusCodes.Status402PaymentRequired, X402ErrorMessages.NoMatchingPaymentRequirements);
            return;
        }

        var upfront = X402RequirementSupport.IsUpfrontFlow(requirements);
        var (valid, payer) = await VerifyAsync(context, routeConfig, payment, requirements);
        if (!valid)
            return;

        string? reservedKey = null;
        if (_replayProtectionEnabled)
        {
            var reservation = await ReserveAsync(context, routeConfig, payment, requirements, payer);
            if (!reservation.Accepted)
                return;

            reservedKey = reservation.Key;
        }

        if (upfront)
        {
            await InvokeUpfrontAsync(context, payment, requirements, payer, reservedKey);
            return;
        }

        var outcome = new SettlementOutcome();
        try
        {
            await InvokeAuthorizationFlowAsync(context, payment, requirements, payer, outcome);
        }
        finally
        {
            if (!outcome.Settled)
                await ReleaseReservationAsync(context, reservedKey);
        }
    }

    private async Task<(bool Valid, string Payer)> VerifyAsync(
        HttpContext context, RoutePaymentConfig routeConfig, PaymentPayload payment, PaymentRequirements requirements)
    {
        VerificationResponse verificationResponse;
        try
        {
            verificationResponse = await _processor.VerifyPaymentAsync(payment, requirements, context.RequestAborted);
        }
        catch (Exception ex)
        {
            GetLogger(context)?.LogError(ex, "x402 facilitator verify failed");
            await WriteJsonErrorAsync(context, 500, X402ErrorCodes.FacilitatorError, noStore: false);
            return (false, string.Empty);
        }

        if (!verificationResponse.IsValid)
        {
            var invalidReason = verificationResponse.InvalidReason ?? X402ErrorCodes.UnexpectedVerifyError;
            await ReturnPaymentRequired(context, routeConfig, StatusCodeForInvalidReason(invalidReason), invalidReason);
            return (false, string.Empty);
        }

        return (true, verificationResponse.Payer ?? string.Empty);
    }

    private async Task<(bool Accepted, string? Key)> ReserveAsync(
        HttpContext context, RoutePaymentConfig routeConfig, PaymentPayload payment, PaymentRequirements requirements, string payer)
    {
        var replayKey = PaymentReplayIdentity.Extract(payment, requirements);
        if (replayKey == null)
        {
            GetLogger(context)?.LogWarning("x402 replay protection: could not derive a payment identity; rejecting the payment");
            await ReturnPaymentRequired(context, routeConfig, StatusCodes.Status400BadRequest, X402ErrorCodes.InvalidPayload);
            return (false, null);
        }

        bool reserved;
        try
        {
            reserved = await GetReplayStore(context)
                .TryReserveAsync(replayKey, _replayRetention, context.RequestAborted);
        }
        catch (PaymentReplayStoreFullException ex)
        {
            GetLogger(context)?.LogError(ex, "x402 replay store is full; refusing the payment");
            await ReturnReplayStoreFull(context);
            return (false, null);
        }

        if (!reserved)
        {
            GetLogger(context)?.LogWarning("x402 rejected a replayed payment for {Payer}", payer);
            await ReturnPaymentRequired(
                context, routeConfig, StatusCodes.Status402PaymentRequired, X402ErrorCodes.NonceAlreadyUsed);
            return (false, null);
        }

        return (true, replayKey);
    }

    private sealed class SettlementOutcome
    {
        public bool Settled { get; set; }
    }

    private async Task InvokeAuthorizationFlowAsync(
        HttpContext context, PaymentPayload payment, PaymentRequirements requirements, string payer, SettlementOutcome outcome)
    {
        var originalResponseBody = context.Response.Body;
        using var responseBuffer = new MemoryStream();
        context.Response.Body = new BoundedWriteStream(responseBuffer, _maxBufferedResponseBytes);

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

        if (context.Response.StatusCode >= 400)
        {
            responseBuffer.Seek(0, SeekOrigin.Begin);
            await responseBuffer.CopyToAsync(originalResponseBody, context.RequestAborted);
            context.Response.Body = originalResponseBody;
            return;
        }

        SettlementResponse settlementResponse;
        try
        {
            settlementResponse = await _processor.SettlePaymentAsync(payment, requirements, CancellationToken.None)
                ?? SettlementResponse.Failure(requirements.Network, X402ErrorCodes.SettlementPending, payer);
            outcome.Settled = KeepsReservation(settlementResponse);
        }
        catch (Exception ex)
        {
            GetLogger(context)?.LogError(ex, "x402 settlement failed");
            var unknown = X402SettleOutcomes.OutcomeIsUnknown(ex);
            outcome.Settled = unknown;
            context.Response.Body = originalResponseBody;
            await ReturnSettlementFailure(
                context,
                SettlementResponse.Failure(requirements.Network, unknown ? X402ErrorCodes.SettlementPending : X402ErrorCodes.UnexpectedSettleError, payer),
                requirements);
            return;
        }

        if (!settlementResponse.Success)
        {
            context.Response.Body = originalResponseBody;
            await ReturnSettlementFailure(context, settlementResponse, requirements);
            return;
        }

        AppendSettlementReceipt(context, settlementResponse);

        responseBuffer.Seek(0, SeekOrigin.Begin);
        await responseBuffer.CopyToAsync(originalResponseBody, context.RequestAborted);
        context.Response.Body = originalResponseBody;
    }

    private async Task InvokeUpfrontAsync(
        HttpContext context, PaymentPayload payment, PaymentRequirements requirements, string payer, string? reservedKey)
    {
        SettlementResponse settlementResponse;
        try
        {
            settlementResponse = await _processor.SettlePaymentAsync(payment, requirements, CancellationToken.None)
                ?? SettlementResponse.Failure(requirements.Network, X402ErrorCodes.SettlementPending, payer);
        }
        catch (Exception ex)
        {
            GetLogger(context)?.LogError(ex, "x402 upfront settlement failed");
            var unknown = X402SettleOutcomes.OutcomeIsUnknown(ex);
            if (!unknown)
                await ReleaseReservationAsync(context, reservedKey);

            await ReturnSettlementFailure(
                context,
                SettlementResponse.Failure(requirements.Network, unknown ? X402ErrorCodes.SettlementPending : X402ErrorCodes.UnexpectedSettleError, payer),
                requirements);
            return;
        }

        if (!settlementResponse.Success)
        {
            if (!KeepsReservation(settlementResponse))
                await ReleaseReservationAsync(context, reservedKey);

            await ReturnSettlementFailure(context, settlementResponse, requirements);
            return;
        }

        context.Response.OnStarting(() =>
        {
            AppendSettlementReceipt(context, settlementResponse);
            return Task.CompletedTask;
        });

        await _next(context);
    }

    private static bool KeepsReservation(SettlementResponse settlementResponse) =>
        !X402SettleOutcomes.IsDefiniteFailure(settlementResponse);

    private async Task ReleaseReservationAsync(HttpContext context, string? reservedKey)
    {
        if (reservedKey == null)
            return;

        try
        {
            await GetReplayStore(context).ReleaseAsync(reservedKey, CancellationToken.None);
        }
        catch (Exception ex)
        {
            GetLogger(context)?.LogError(ex, "x402 could not release the replay reservation");
        }
    }

    private static Task ReturnReplayStoreFull(HttpContext context) =>
        WriteJsonErrorAsync(context, StatusCodes.Status503ServiceUnavailable, X402ErrorCodes.ReplayStoreFull, noStore: true);

    private static async Task WriteJsonErrorAsync(HttpContext context, int statusCode, string error, bool noStore)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        if (noStore)
            context.Response.Headers["Cache-Control"] = PaymentRequiredCacheControl;
        await context.Response.WriteAsync(
            JsonSerializer.Serialize(new { error }), context.RequestAborted);
    }

    private static void AppendSettlementReceipt(HttpContext context, SettlementResponse settlementResponse)
    {
        context.Response.Headers.Append(
            X402Headers.PaymentResponse,
            X402FacilitatorProxyProcessor.EncodeSettlementResponse(settlementResponse));
        context.Response.Headers["Cache-Control"] = X402CacheControl.WithPrivate(context.Response.Headers["Cache-Control"].ToString());
    }

    private static int StatusCodeForInvalidReason(string invalidReason) => invalidReason switch
    {
        X402ErrorCodes.InvalidPayload => StatusCodes.Status400BadRequest,
        X402ErrorCodes.Permit2AllowanceRequired => StatusCodes.Status412PreconditionFailed,
        _ => StatusCodes.Status402PaymentRequired
    };

    private static async Task ReturnPaymentRequired(
        HttpContext context,
        RoutePaymentConfig routeConfig,
        int statusCode,
        string errorMessage)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        context.Response.Headers["Cache-Control"] = PaymentRequiredCacheControl;

        var resource = new ResourceInfo
        {
            Url = string.IsNullOrEmpty(routeConfig.Resource?.Url)
                ? $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}"
                : routeConfig.Resource!.Url,
            Description = routeConfig.Resource?.Description,
            MimeType = routeConfig.Resource?.MimeType,
            ServiceName = routeConfig.Resource?.ServiceName,
            Tags = routeConfig.Resource?.Tags,
            IconUrl = routeConfig.Resource?.IconUrl,
            ExtensionData = routeConfig.Resource?.ExtensionData
        };

        var response = new PaymentRequired
        {
            X402Version = X402Protocol.Version,
            Error = errorMessage,
            Resource = resource,
            Accepts = routeConfig.GetOffers().ToList(),
            Extensions = routeConfig.Extensions
        };

        var json = JsonSerializer.Serialize(response);

        context.Response.Headers.Append(
            X402Headers.PaymentRequired,
            Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));

        await context.Response.WriteAsync(json, context.RequestAborted);
    }

    private static async Task ReturnSettlementFailure(
        HttpContext context,
        SettlementResponse settlement,
        PaymentRequirements requirements)
    {
        settlement.Transaction ??= string.Empty;
        settlement.Network ??= requirements.Network;
        settlement.ErrorReason ??= X402ErrorCodes.UnexpectedSettleError;

        if (!context.Response.HasStarted)
            context.Response.Clear();

        context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
        context.Response.ContentType = "application/json";
        context.Response.Headers["Cache-Control"] = PaymentRequiredCacheControl;
        context.Response.Headers.Append(
            X402Headers.PaymentResponse,
            X402FacilitatorProxyProcessor.EncodeSettlementResponse(settlement));

        await context.Response.WriteAsync("{}", context.RequestAborted);
    }
}
