# Nethereum.Explorer.Anchoring

> **PREVIEW** - This package is in preview. APIs may change between releases.

**Pluggable Blazor pages for the Nethereum Explorer that show AppChain anchoring: anchored chains, their anchor history, and an anchoring admin page**

[![NuGet](https://img.shields.io/nuget/v/Nethereum.Explorer.Anchoring.svg)](https://www.nuget.org/packages/Nethereum.Explorer.Anchoring/)

## Overview

`Nethereum.Explorer.Anchoring` is a Razor class library (`Microsoft.NET.Sdk.Razor`, `net10.0`) that adds anchoring pages to a [Nethereum.Explorer](../Nethereum.Explorer/README.md) instance. It reads the tables written by the indexer in [Nethereum.AppChain.Anchoring.Postgres](../Nethereum.AppChain.Anchoring.Postgres/README.md) (`AddAnchorIndexProcessing` and `AddAnchorSummaryDenormalizerProcessing`), so the Explorer host must point at the same PostgreSQL database as that indexer.

| Page | Route | Shows |
|---|---|---|
| `AnchoringDashboard` | `/anchoring` | One card per anchored chain from `IAnchorExplorerService.GetAllChainSummariesAsync()`, linking to its detail page; a "not configured" message when the service is not configured |
| `AnchoringChainDetail` | `/anchoring/{ChainId:long}` | The chain's `ChainAnchoringSummary` and its `AnchorRecord` history, 25 per page |
| `AnchoringAdmin` | `/anchoring/admin` | Status, strategies, configuration and a force-anchor action, through an `HttpClient` named `"anchoring"` calling `/api/anchor/status`, `/api/anchor/strategies`, `/api/anchor/config` and `/api/anchor/force` |

The admin page needs a host that registers an `HttpClient` named `"anchoring"` pointing at an anchor service exposing those `/api/anchor/*` endpoints; in this repository that service is `aspire/appchain/Nethereum.AppChain.AnchorService`.

## Installation

```bash
dotnet add package Nethereum.Explorer.Anchoring
```

## Dependencies

- **Nethereum.Explorer** - `IExplorerNavContributor`, `ExplorerNavItem`, `ExplorerLocalizer`
- **Nethereum.AppChain.Anchoring.Postgres** - the repositories and entities the service reads

## Registration

```csharp
public static class AnchorExplorerServiceCollectionExtensions
{
    public static IServiceCollection AddAnchorExplorerServices(
        this IServiceCollection services, string? connectionString = null)
}
```

- With a non-blank `connectionString` it calls `AddAnchorIndexPostgresStorage(connectionString)` and registers a scoped `IAnchorExplorerService` that is an `AnchorExplorerService` built from the four repositories, or a `NullAnchorExplorerService` if any repository is missing.
- Without one, it registers `NullAnchorExplorerService`.
- In both cases it registers `AnchorExplorerNavContributor` as `IExplorerNavContributor` (scoped) and `AnchoringLocalizer` (singleton). It needs an `ExplorerLocalizer` in the container, which the Explorer's own `AddExplorerServices` provides.

To show the pages, add this assembly (`typeof(Nethereum.Explorer.Anchoring.AnchorExplorerServiceCollectionExtensions).Assembly`) to the routable assemblies of the Explorer host with the `AddAdditionalAssemblies(...)` call of `MapRazorComponents<Nethereum.Explorer.Components.App>()`, as the Explorer README does for other plug-in assemblies, and call `AddAnchorExplorerServices` as well as `AddExplorerServices`; the order does not matter, because `AddExplorerServices` registers none of the service types `AddAnchorExplorerServices` registers (`IAnchorExplorerService`, `IExplorerNavContributor`, `AnchoringLocalizer`) and the only dependency is that an `ExplorerLocalizer` is registered by the time services are resolved, which `AddExplorerServices` does. No test starts an Explorer host, so this host wiring is described, not exercised; the registration behaviour itself is exercised below.

Without a connection string, nothing is shown and no navigation items are added:

```csharp
var services = new ServiceCollection();
services.AddSingleton<ExplorerLocalizer>();
services.AddAnchorExplorerServices();
using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();

var service = scope.ServiceProvider.GetRequiredService<IAnchorExplorerService>();
var navItems = scope.ServiceProvider.GetRequiredService<IExplorerNavContributor>().GetNavItems();
```

`service` is a `NullAnchorExplorerService` with `IsConfigured == false`, its lists are empty and its single-item lookups return `null`. Backed by `AnchorExplorerServiceTests.Given_NoConnectionString_When_AnchorExplorerServicesAreAdded_Then_TheServiceIsNotConfiguredAndNoNavItemsAreContributed` (`DocSection.DataIndexing`, `explorer-anchoring`, Order 2).

With a connection string, the service is configured and the navigation gets two entries:

```csharp
var services = new ServiceCollection();
services.AddSingleton<ExplorerLocalizer>();
services.AddAnchorExplorerServices("Host=localhost;Database=anchors;Username=postgres;Password=postgres");
using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();

_ = scope.ServiceProvider.GetRequiredService<AnchoringLocalizer>();
var service = scope.ServiceProvider.GetRequiredService<IAnchorExplorerService>();
var navItems = scope.ServiceProvider.GetRequiredService<IExplorerNavContributor>().GetNavItems();
```

`navItems` are `/anchoring` (order 100, icon `bi bi-link-45deg`) and `/anchoring/admin` (order 101, icon `bi bi-gear`). Resolving the service does not open a connection. Backed by `Given_AConnectionString_When_AnchorExplorerServicesAreAdded_Then_TheServiceIsConfiguredAndTheAnchoringNavItemsAreContributed` (Order 3). Resolving `AnchoringLocalizer` is what registers the English and Spanish strings in the shared `ExplorerLocalizer`.

## Reading anchoring data

```csharp
public interface IAnchorExplorerService
{
    bool IsConfigured { get; }
    Task<IReadOnlyList<ChainAnchoringSummary>> GetAllChainSummariesAsync();
    Task<ChainAnchoringSummary?> GetChainSummaryAsync(long chainId);
    Task<IReadOnlyList<AnchorRecord>> GetAnchorsAsync(long chainId, int page = 1, int pageSize = 25);
    Task<int> GetAnchorCountAsync(long chainId);
    Task<AnchorRecord?> GetAnchorByEndBlockAsync(long chainId, long endBlock);
    Task<IReadOnlyList<BlockProofRecord>> GetProofsAsync(long chainId, int page = 1, int pageSize = 25);
    Task<IReadOnlyList<ChainRegistration>> GetAllRegistrationsAsync();
}

public class AnchorExplorerService : IAnchorExplorerService
{
    public AnchorExplorerService(
        IAnchorRecordRepository anchorRepo,
        IChainRegistrationRepository chainRepo,
        IBlockProofRecordRepository proofRepo,
        IChainAnchoringSummaryRepository summaryRepo)
}

public class NullAnchorExplorerService : IAnchorExplorerService
```

`AnchorExplorerService.IsConfigured` is always `true`. `GetAnchorsAsync` and `GetProofsAsync` convert the 1-based `page` into `skip = (page - 1) * pageSize` and call the repository's `GetByChainAsync(chainId, skip, pageSize)`. The entity types (`ChainAnchoringSummary`, `AnchorRecord`, `BlockProofRecord`, `ChainRegistration`) are in `Nethereum.AppChain.Anchoring.Postgres.Entities`.

```csharp
IAnchorExplorerService service = new AnchorExplorerService(
    anchors, new InMemoryRegistrations(), new InMemoryProofs(), summaries);

var firstPage = await service.GetAnchorsAsync(420420);
var secondPage = await service.GetAnchorsAsync(420420, page: 2, pageSize: 25);
var count = await service.GetAnchorCountAsync(420420);
var anchor = await service.GetAnchorByEndBlockAsync(420420, 300);
var summary = await service.GetChainSummaryAsync(420420);
var allSummaries = await service.GetAllChainSummariesAsync();
```

With 30 anchors stored, `firstPage` has 25 entries and `secondPage` has 5. Backed by `AnchorExplorerServiceTests.Given_IndexedAnchors_When_TheExplorerServiceIsQueried_Then_ItPagesAnchorsAndReturnsSummaries` (`explorer-anchoring`, Order 1); the `InMemory*` classes are the test's in-memory implementations of the four repository interfaces. All three tests are in `tests/Nethereum.Explorer.UnitTests/AnchorExplorerServiceTests.cs`.

## Navigation and localisation

```csharp
public class AnchorExplorerNavContributor : IExplorerNavContributor
{
    public AnchorExplorerNavContributor(IAnchorExplorerService anchorService, ExplorerLocalizer localizer)
    public IReadOnlyList<ExplorerNavItem> GetNavItems()
}

public class AnchoringLocalizer
{
    public AnchoringLocalizer(ExplorerLocalizer explorer)
    public string this[string key] { get; }
}

public static class AnchoringKeys
```

`GetNavItems()` returns an empty list when the service is not configured. `AnchoringKeys` holds the string keys (nested classes `Nav`, `Dashboard`, `Detail`, `Stats` and more) that `AnchoringLocalizer` registers English and Spanish text for.
