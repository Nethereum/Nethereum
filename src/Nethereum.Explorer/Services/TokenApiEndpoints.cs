using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.Explorer.Services;

public static class TokenApiEndpoints
{
    public static WebApplication MapTokenApiEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tokens").RequireRateLimiting("api");

        group.MapGet("/{address}/balances", async (string address, ITokenExplorerService tokenService) =>
        {
            if (!tokenService.IsAvailable)
                return Results.Json(new { error = "Token indexing not configured" }, statusCode: 503);

            var balances = await tokenService.GetTokenBalancesAsync(address);
            return Results.Ok(balances.Select(b => new
            {
                b.Address,
                b.ContractAddress,
                b.Balance,
                b.TokenType,
                b.LastUpdatedBlockNumber
            }));
        });

        group.MapGet("/{address}/nfts", async (string address, ITokenExplorerService tokenService) =>
        {
            if (!tokenService.IsAvailable)
                return Results.Json(new { error = "Token indexing not configured" }, statusCode: 503);

            var nfts = await tokenService.GetNFTInventoryAsync(address);
            return Results.Ok(nfts.Select(n => new
            {
                n.Address,
                n.ContractAddress,
                n.TokenId,
                n.Amount,
                n.TokenType,
                n.LastUpdatedBlockNumber
            }));
        });

        group.MapGet("/{address}/transfers", async (string address, int? page, int? pageSize, ITokenExplorerService tokenService) =>
        {
            if (!tokenService.IsAvailable)
                return Results.Json(new { error = "Token indexing not configured" }, statusCode: 503);

            var effectivePageSize = ExplorerConstants.ClampPageSize(pageSize ?? ExplorerConstants.DefaultPageSize);
            var transfers = await tokenService.GetTransfersByAddressAsync(address, page ?? 1, effectivePageSize);
            return Results.Ok(transfers.Select(MapTransferLog));
        });

        group.MapGet("/contract/{contractAddress}/transfers", async (string contractAddress, int? page, int? pageSize, ITokenExplorerService tokenService) =>
        {
            if (!tokenService.IsAvailable)
                return Results.Json(new { error = "Token indexing not configured" }, statusCode: 503);

            var effectivePageSize = ExplorerConstants.ClampPageSize(pageSize ?? ExplorerConstants.DefaultPageSize);
            var transfers = await tokenService.GetTransfersByContractAsync(contractAddress, page ?? 1, effectivePageSize);
            return Results.Ok(transfers.Select(MapTransferLog));
        });

        group.MapGet("/contract/{contractAddress}/metadata", async (string contractAddress, ITokenExplorerService tokenService) =>
        {
            if (!tokenService.IsAvailable)
                return Results.Json(new { error = "Token indexing not configured" }, statusCode: 503);

            var metadata = await tokenService.GetTokenMetadataAsync(contractAddress);
            if (metadata == null)
                return Results.NotFound();

            return Results.Ok(new
            {
                metadata.ContractAddress,
                metadata.Name,
                metadata.Symbol,
                metadata.Decimals,
                metadata.TokenType
            });
        });

        return app;
    }

    private static object MapTransferLog(ITokenTransferLogView t) => new
    {
        t.TransactionHash,
        t.LogIndex,
        t.BlockNumber,
        t.ContractAddress,
        t.FromAddress,
        t.ToAddress,
        t.Amount,
        t.TokenId,
        t.TokenType
    };
}
