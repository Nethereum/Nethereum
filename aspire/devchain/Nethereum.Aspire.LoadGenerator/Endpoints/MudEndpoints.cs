using Nethereum.Aspire.LoadGenerator.Scenarios;

namespace Nethereum.Aspire.LoadGenerator.Endpoints;

public static class MudEndpoints
{
    public static WebApplication MapMudEndpoints(this WebApplication app)
    {
        app.MapGet("/mud/world-address", () =>
        {
            var address = MudWorldScenario.DeployedWorldAddress;
            if (string.IsNullOrEmpty(address))
            {
                return Results.NotFound(new { status = "pending", message = "World not yet deployed" });
            }
            return Results.Ok(new { address });
        });

        return app;
    }
}
