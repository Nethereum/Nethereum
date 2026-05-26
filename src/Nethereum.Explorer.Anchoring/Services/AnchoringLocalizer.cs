using Nethereum.Explorer.Services.Localization;

namespace Nethereum.Explorer.Anchoring.Services
{
    public class AnchoringLocalizer
    {
        private readonly ExplorerLocalizer _explorer;

        public AnchoringLocalizer(ExplorerLocalizer explorer)
        {
            _explorer = explorer;
            RegisterAll();
        }

        public string this[string key] => _explorer[key];

        private void RegisterAll()
        {
            _explorer.Register("en", new Dictionary<string, string>
            {
                [AnchoringKeys.Nav.Anchoring] = "Anchoring",

                [AnchoringKeys.Dashboard.Title] = "Anchoring",
                [AnchoringKeys.Dashboard.Chains] = "chains",
                [AnchoringKeys.Dashboard.NotConfigured] = "Anchoring indexer not configured for this explorer.",
                [AnchoringKeys.Dashboard.NoChains] = "No anchored chains found",
                [AnchoringKeys.Dashboard.NoChainsSub] = "Waiting for anchor submissions...",
                [AnchoringKeys.Dashboard.Chain] = "Chain",
                [AnchoringKeys.Dashboard.ViewDetails] = "View Details",

                [AnchoringKeys.Detail.Title] = "Chain {0} — Anchoring",
                [AnchoringKeys.Detail.AllChains] = "All Chains",
                [AnchoringKeys.Detail.AnchorHistory] = "Anchor History",
                [AnchoringKeys.Detail.Anchors] = "anchors",

                [AnchoringKeys.Stats.LatestBlock] = "Latest Block",
                [AnchoringKeys.Stats.TotalAnchors] = "Total Anchors",
                [AnchoringKeys.Stats.ProvenBlocks] = "Proven Blocks",
                [AnchoringKeys.Stats.ProofSystem] = "Proof System",
                [AnchoringKeys.Stats.AvgInterval] = "Avg Interval",

                [AnchoringKeys.Table.Blocks] = "Blocks",
                [AnchoringKeys.Table.EndBlockHash] = "End Block Hash",
                [AnchoringKeys.Table.StateRoot] = "State Root",
                [AnchoringKeys.Table.Proof] = "Proof",
                [AnchoringKeys.Table.MainchainTx] = "Mainchain Tx",
                [AnchoringKeys.Table.Operator] = "Operator",
                [AnchoringKeys.Table.ProofSize] = "Proof Size",

                [AnchoringKeys.Admin.Title] = "Anchoring Admin",
                [AnchoringKeys.Admin.Status] = "Status",
                [AnchoringKeys.Admin.Strategy] = "Strategy",
                [AnchoringKeys.Admin.DataAvailability] = "Data Availability",
                [AnchoringKeys.Admin.ProofMode] = "Proof Mode",
                [AnchoringKeys.Admin.Cadence] = "Cadence",
                [AnchoringKeys.Admin.Interval] = "Interval",
                [AnchoringKeys.Admin.ForceAnchor] = "Force Anchor Now",
                [AnchoringKeys.Admin.ApplyStrategy] = "Apply Strategy",
                [AnchoringKeys.Admin.SaveConfig] = "Save Config",
                [AnchoringKeys.Admin.Initializing] = "Initializing",
                [AnchoringKeys.Admin.Running] = "Running",
                [AnchoringKeys.Admin.Stopped] = "Stopped",
                [AnchoringKeys.Admin.AvailableStrategies] = "Available Strategies",
                [AnchoringKeys.Admin.CurrentConfig] = "Current Configuration",
                [AnchoringKeys.Admin.AnchorContract] = "Anchor Contract",
                [AnchoringKeys.Admin.Blocks] = "blocks",
                [AnchoringKeys.Admin.Milliseconds] = "ms",
            });

            _explorer.Register("es", new Dictionary<string, string>
            {
                [AnchoringKeys.Nav.Anchoring] = "Anclaje",

                [AnchoringKeys.Dashboard.Title] = "Anclaje",
                [AnchoringKeys.Dashboard.Chains] = "cadenas",
                [AnchoringKeys.Dashboard.NotConfigured] = "El indexador de anclaje no está configurado para este explorador.",
                [AnchoringKeys.Dashboard.NoChains] = "No se encontraron cadenas ancladas",
                [AnchoringKeys.Dashboard.NoChainsSub] = "Esperando envíos de anclaje...",
                [AnchoringKeys.Dashboard.Chain] = "Cadena",
                [AnchoringKeys.Dashboard.ViewDetails] = "Ver Detalles",

                [AnchoringKeys.Detail.Title] = "Cadena {0} — Anclaje",
                [AnchoringKeys.Detail.AllChains] = "Todas las Cadenas",
                [AnchoringKeys.Detail.AnchorHistory] = "Historial de Anclajes",
                [AnchoringKeys.Detail.Anchors] = "anclajes",

                [AnchoringKeys.Stats.LatestBlock] = "Último Bloque",
                [AnchoringKeys.Stats.TotalAnchors] = "Total de Anclajes",
                [AnchoringKeys.Stats.ProvenBlocks] = "Bloques Probados",
                [AnchoringKeys.Stats.ProofSystem] = "Sistema de Prueba",
                [AnchoringKeys.Stats.AvgInterval] = "Intervalo Prom.",

                [AnchoringKeys.Table.Blocks] = "Bloques",
                [AnchoringKeys.Table.EndBlockHash] = "Hash del Bloque Final",
                [AnchoringKeys.Table.StateRoot] = "Raíz de Estado",
                [AnchoringKeys.Table.Proof] = "Prueba",
                [AnchoringKeys.Table.MainchainTx] = "Tx en Mainchain",
                [AnchoringKeys.Table.Operator] = "Operador",
                [AnchoringKeys.Table.ProofSize] = "Tamaño de Prueba",

                [AnchoringKeys.Admin.Title] = "Admin de Anclaje",
                [AnchoringKeys.Admin.Status] = "Estado",
                [AnchoringKeys.Admin.Strategy] = "Estrategia",
                [AnchoringKeys.Admin.DataAvailability] = "Disponibilidad de Datos",
                [AnchoringKeys.Admin.ProofMode] = "Modo de Prueba",
                [AnchoringKeys.Admin.Cadence] = "Cadencia",
                [AnchoringKeys.Admin.Interval] = "Intervalo",
                [AnchoringKeys.Admin.ForceAnchor] = "Forzar Anclaje",
                [AnchoringKeys.Admin.ApplyStrategy] = "Aplicar Estrategia",
                [AnchoringKeys.Admin.SaveConfig] = "Guardar Config",
                [AnchoringKeys.Admin.Initializing] = "Inicializando",
                [AnchoringKeys.Admin.Running] = "Ejecutando",
                [AnchoringKeys.Admin.Stopped] = "Detenido",
                [AnchoringKeys.Admin.AvailableStrategies] = "Estrategias Disponibles",
                [AnchoringKeys.Admin.CurrentConfig] = "Configuración Actual",
                [AnchoringKeys.Admin.AnchorContract] = "Contrato de Anclaje",
                [AnchoringKeys.Admin.Blocks] = "bloques",
                [AnchoringKeys.Admin.Milliseconds] = "ms",
            });
        }
    }
}
