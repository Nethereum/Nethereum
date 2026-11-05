using System;
using System.Net;
using System.Net.Http;
using Nethereum.JsonRpc.Client;
using Nethereum.X402.Models;

namespace Nethereum.X402.Facilitator;

internal static class X402SettleOutcomes
{
    public static bool IsDefiniteStatus(HttpStatusCode status) => status switch
    {
        HttpStatusCode.BadRequest
            or HttpStatusCode.Unauthorized
            or HttpStatusCode.Forbidden
            or HttpStatusCode.NotFound
            or HttpStatusCode.Conflict
            or HttpStatusCode.RequestEntityTooLarge
            or HttpStatusCode.UnsupportedMediaType
            or HttpStatusCode.UnprocessableEntity => true,
        _ => (int)status is >= 200 and < 300
    };

    public static bool IsProvablyUnsent(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current is HttpRequestException
                {
                    StatusCode: null,
                    HttpRequestError: HttpRequestError.ConnectionError or HttpRequestError.NameResolutionError
                })
                return true;
        }

        return false;
    }

    public static bool IsDefiniteFailure(SettlementResponse? settlement) =>
        settlement != null
        && !settlement.Success
        && !string.IsNullOrEmpty(settlement.ErrorReason)
        && settlement.ErrorReason != X402ErrorCodes.SettlementPending;

    public static bool OutcomeIsUnknown(Exception ex) =>
        !(IsProvablyUnsent(ex) || ex is HttpRequestException { StatusCode: { } status } && IsDefiniteStatus(status));

    private static readonly string[] KnownTransactionReplies =
    {
        "already known",
        "known transaction",
        "existing txn with same hash"
    };

    public static bool IsKnownTransactionReply(RpcResponseException rejection)
    {
        var message = rejection.RpcError?.Message ?? rejection.Message;
        if (string.IsNullOrEmpty(message))
            return false;

        foreach (var known in KnownTransactionReplies)
        {
            if (message.IndexOf(known, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    public static bool BroadcastOutcomeIsUnknown(Exception ex) =>
        ex is RpcResponseException rejection ? IsKnownTransactionReply(rejection) : !IsProvablyUnsent(ex);
}
