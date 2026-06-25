using System.Collections.Generic;
using Nethereum.Explorer.Services;
using Nethereum.Explorer.Services.Localization;

namespace Nethereum.Explorer.Anchoring.Services
{
    public class AnchorExplorerNavContributor : IExplorerNavContributor
    {
        private readonly IAnchorExplorerService _anchorService;
        private readonly ExplorerLocalizer _localizer;

        public AnchorExplorerNavContributor(IAnchorExplorerService anchorService, ExplorerLocalizer localizer)
        {
            _anchorService = anchorService;
            _localizer = localizer;
        }

        public IReadOnlyList<ExplorerNavItem> GetNavItems()
        {
            if (!_anchorService.IsConfigured)
                return [];

            return
            [
                new ExplorerNavItem
                {
                    Label = _localizer[AnchoringKeys.Nav.Anchoring],
                    Href = "/anchoring",
                    IconClass = "bi bi-link-45deg",
                    Order = 100
                },
                new ExplorerNavItem
                {
                    Label = _localizer[AnchoringKeys.Admin.Title],
                    Href = "/anchoring/admin",
                    IconClass = "bi bi-gear",
                    Order = 101
                }
            ];
        }
    }
}
