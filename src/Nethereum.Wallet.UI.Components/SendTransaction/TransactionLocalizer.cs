using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.SendTransaction
{
    public class TransactionLocalizer : ComponentLocalizerBase<TransactionViewModel>
    {
        public static class Keys
        {
            public const string GasEstimationFailed = "GasEstimationFailed";
            public const string GasPriceFetchFailed = "GasPriceFetchFailed";
            public const string NonceFetchFailed = "NonceFetchFailed";
            public const string SimulationFailed = "SimulationFailed";
            public const string TransactionFailed = "TransactionFailed";
            
            public const string EstimatingGas = "EstimatingGas";
            public const string LoadingNonce = "LoadingNonce";
            public const string SimulatingTransaction = "SimulatingTransaction";
            public const string SendingTransaction = "SendingTransaction";
            
            public const string GasLimit = "GasLimit";
            public const string GasPrice = "GasPrice";
            public const string MaxFee = "MaxFee";
            public const string PriorityFee = "PriorityFee";
            public const string Nonce = "Nonce";
            
            public const string TotalGasCost = "TotalGasCost";
            public const string TotalTransactionCost = "TotalTransactionCost";
            public const string DefaultNativeTokenName = "DefaultNativeTokenName";
            public const string NativeTransferDisplay = "NativeTransferDisplay";
            public const string ContractInteractionDisplay = "ContractInteractionDisplay";
            
            public const string CannotSendToSelf = "CannotSendToSelf";
            public const string FromAddressMismatch = "FromAddressMismatch";
            public const string DataMustStartWith0x = "DataMustStartWith0x";
            public const string NonceMustBeNonNegative = "NonceMustBeNonNegative";
            
            public const string SimulateTransaction = "SimulateTransaction";
            public const string RefreshNonce = "RefreshNonce";
            public const string EstimateGas = "EstimateGas";
            
            public const string InvalidTransaction = "InvalidTransaction";
            public const string MissingNonce = "MissingNonce";
            public const string MissingGasConfiguration = "MissingGasConfiguration";
            
            public const string NetworkInitializationWarning = "NetworkInitializationWarning";
            
            public const string TransactionDetails = "TransactionDetails";
            public const string From = "From";
            public const string To = "To";
            public const string Contract = "Contract";
            public const string Amount = "Amount";
            public const string Network = "Network";
            public const string ChainId = "ChainId";
            public const string TransactionParameters = "TransactionParameters";
            public const string TransactionData = "TransactionData";
            public const string DecodingTransaction = "DecodingTransaction";
            public const string DecodingError = "DecodingError";
            public const string FunctionCall = "FunctionCall";
            public const string RawTransactionData = "RawTransactionData";
            public const string HideRawData = "HideRawData";
            public const string ShowRawData = "ShowRawData";
            public const string GasConfiguration = "GasConfiguration";
            public const string GasSlow = "GasSlow";
            public const string GasNormal = "GasNormal";
            public const string GasFast = "GasFast";
            public const string GasCustom = "GasCustom";
            public const string CostSummary = "CostSummary";
            public const string EstimatedGasFee = "EstimatedGasFee";
            public const string TotalCost = "TotalCost";
            
            public const string LoadingGasPrices = "LoadingGasPrices";
            public const string NoValidGasPriceData = "NoValidGasPriceData";
            public const string GasStrategyLoadFailed = "GasStrategyLoadFailed";
            public const string GasConfigurationIssue = "GasConfigurationIssue";
            public const string RefreshGasStrategiesFailed = "RefreshGasStrategiesFailed";
            public const string InitializationFailed = "InitializationFailed";
            public const string UsingStrategy = "UsingStrategy";
            public const string NetworkBasePriceUseCustom = "NetworkBasePriceUseCustom";
            public const string UnknownFunction = "UnknownFunction";
            public const string UnknownFunctionDescription = "UnknownFunctionDescription";
            public const string DataSize = "DataSize";
            public const string RawData = "RawData";
            public const string AdvancedOptions = "AdvancedOptions";
            public const string AmountToSend = "AmountToSend";
            public const string GasFee = "GasFee";
            public const string Parameters = "Parameters";
            public const string Name = "Name";
            public const string Type = "Type";
            public const string Value = "Value";
            public const string Balance = "Balance";
            public const string MaxButton = "Max";
            public const string GasSpeed = "GasSpeed";
            public const string GasSlowDescription = "GasSlowDescription";
            public const string GasNormalDescription = "GasNormalDescription";
            public const string GasFastDescription = "GasFastDescription";
            public const string GasCustomDescription = "GasCustomDescription";
            
            public const string GasMode = "GasMode";
            public const string GasModeEip1559 = "GasModeEip1559";
            public const string GasModeLegacy = "GasModeLegacy";
            public const string GasPriceAdjustment = "GasPriceAdjustment";
            public const string NetworkGasPrice = "NetworkGasPrice";
            public const string YourGasPrice = "YourGasPrice";
            public const string Multiplier08 = "Multiplier08";
            public const string Multiplier10 = "Multiplier10";
            public const string Multiplier12 = "Multiplier12";
            public const string Multiplier08Description = "Multiplier08Description";
            public const string Multiplier10Description = "Multiplier10Description";
            public const string Multiplier12Description = "Multiplier12Description";
            public const string Custom = "Custom";
            public const string SaveCustomGas = "SaveCustomGas";
            public const string RefreshGasPrices = "RefreshGasPrices";
            public const string GasCost = "GasCost";
            public const string EIP1559NotAvailable = "EIP1559NotAvailable";
            public const string RecipientRequired = "RecipientRequired";
            public const string InvalidAmount = "InvalidAmount";
            public const string InvalidGasConfiguration = "InvalidGasConfiguration";
            public const string NonceRequired = "NonceRequired";
            public const string ValidationError = "ValidationError";
        }
        
        public TransactionLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }
        
        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.GasEstimationFailed] = "Gas estimation failed",
                [Keys.GasPriceFetchFailed] = "Gas price fetch failed",
                [Keys.NonceFetchFailed] = "Failed to fetch nonce",
                [Keys.SimulationFailed] = "Transaction simulation failed",
                [Keys.TransactionFailed] = "Transaction failed",
                
                [Keys.EstimatingGas] = "Estimating gas...",
                [Keys.LoadingNonce] = "Loading nonce...",
                [Keys.SimulatingTransaction] = "Simulating transaction...",
                [Keys.SendingTransaction] = "Sending transaction...",
                
                [Keys.GasLimit] = "Gas Limit",
                [Keys.GasPrice] = "Gas Price",
                [Keys.MaxFee] = "Max Fee",
                [Keys.PriorityFee] = "Priority Fee",
                [Keys.Nonce] = "Nonce",
                
                [Keys.TotalGasCost] = "Total Gas Cost",
                [Keys.TotalTransactionCost] = "Total Transaction Cost",
                [Keys.DefaultNativeTokenName] = "Ether",
                [Keys.NativeTransferDisplay] = "{0} transfer",
                [Keys.ContractInteractionDisplay] = "Contract interaction",
                
                [Keys.CannotSendToSelf] = "Cannot send to self",
                [Keys.FromAddressMismatch] = "Transaction from address does not match the selected account",
                [Keys.DataMustStartWith0x] = "Data must start with 0x",
                [Keys.NonceMustBeNonNegative] = "Nonce must be non-negative",
                
                [Keys.SimulateTransaction] = "Simulate Transaction",
                [Keys.RefreshNonce] = "Refresh Nonce",
                [Keys.EstimateGas] = "Estimate Gas",
                
                [Keys.InvalidTransaction] = "Invalid transaction",
                [Keys.MissingNonce] = "Nonce is required",
                [Keys.MissingGasConfiguration] = "Gas configuration is required",
                
                [Keys.TransactionDetails] = "Transaction Details",
                [Keys.From] = "From",
                [Keys.To] = "To", 
                [Keys.Contract] = "Contract",
                [Keys.Amount] = "Amount",
                [Keys.Network] = "Network",
                [Keys.ChainId] = "Chain ID",
                [Keys.TransactionParameters] = "Transaction Parameters",
                [Keys.TransactionData] = "Transaction Data",
                [Keys.DecodingTransaction] = "Decoding transaction...",
                [Keys.DecodingError] = "Decoding Error",
                [Keys.FunctionCall] = "Function Call",
                [Keys.RawTransactionData] = "Raw Transaction Data",
                [Keys.HideRawData] = "Hide Raw Data",
                [Keys.ShowRawData] = "Show Raw Data",
                [Keys.GasConfiguration] = "Gas Configuration",
                [Keys.GasSlow] = "Slow",
                [Keys.GasNormal] = "Normal",
                [Keys.GasFast] = "Fast",
                [Keys.GasCustom] = "Custom",
                [Keys.GasSpeed] = "Speed",
                [Keys.GasSlowDescription] = "Lower gas price",
                [Keys.GasNormalDescription] = "Standard gas price",
                [Keys.GasFastDescription] = "Higher gas price",
                [Keys.GasCustomDescription] = "Set your own gas price",
                [Keys.CostSummary] = "Cost Summary",
                [Keys.EstimatedGasFee] = "Estimated Gas Fee",
                [Keys.TotalCost] = "Total Cost",
                
                [Keys.LoadingGasPrices] = "Loading gas prices...",
                [Keys.NoValidGasPriceData] = "No valid gas price data available",
                [Keys.GasStrategyLoadFailed] = "Failed to load gas strategy",
                [Keys.GasConfigurationIssue] = "Gas configuration issue",
                [Keys.RefreshGasStrategiesFailed] = "Failed to refresh gas strategies",
                [Keys.InitializationFailed] = "Transaction initialization failed",
                [Keys.NetworkInitializationWarning] = "Network initialization warning",
                [Keys.UsingStrategy] = "Using strategy",
                [Keys.NetworkBasePriceUseCustom] = "Network base price - use CUSTOM to adjust",
                [Keys.UnknownFunction] = "Unknown Function",
                [Keys.UnknownFunctionDescription] = "This function signature is not in our database",
                [Keys.DataSize] = "Data Size",
                [Keys.RawData] = "Raw Data",
                [Keys.AdvancedOptions] = "Advanced Options",
                [Keys.AmountToSend] = "Amount to Send",
                [Keys.GasFee] = "Gas Fee",
                [Keys.Parameters] = "Parameters",
                [Keys.Name] = "Name",
                [Keys.Type] = "Type",
                [Keys.Value] = "Value",
                [Keys.Balance] = "Balance",
                [Keys.MaxButton] = "Max",
                
                [Keys.GasMode] = "Fee Mode",
                [Keys.GasModeEip1559] = "EIP-1559",
                [Keys.GasModeLegacy] = "Legacy",
                [Keys.GasPriceAdjustment] = "Gas Price Adjustment",
                [Keys.NetworkGasPrice] = "Network Base Price",
                [Keys.YourGasPrice] = "Your Gas Price",
                [Keys.Multiplier08] = "0.8x",
                [Keys.Multiplier10] = "1.0x",
                [Keys.Multiplier12] = "1.2x",
                [Keys.Multiplier08Description] = "20% below network - may take longer to confirm",
                [Keys.Multiplier10Description] = "Network recommended - standard confirmation time",
                [Keys.Multiplier12Description] = "20% above network - faster confirmation expected",
                [Keys.Custom] = "Custom",
                [Keys.SaveCustomGas] = "Save Custom Settings",
                [Keys.RefreshGasPrices] = "Refresh Prices",
                [Keys.GasCost] = "Gas Cost",
                [Keys.EIP1559NotAvailable] = "EIP-1559 not available, please use Legacy mode",
                [Keys.RecipientRequired] = "Recipient address is required",
                [Keys.InvalidAmount] = "Amount must be greater than zero",
                [Keys.InvalidGasConfiguration] = "Invalid gas configuration",
                [Keys.NonceRequired] = "Nonce is required",
                [Keys.ValidationError] = "Validation Error"
            });
            
            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.GasEstimationFailed] = "Error al estimar gas",
                [Keys.GasPriceFetchFailed] = "Error al obtener precio de gas",
                [Keys.NonceFetchFailed] = "Error al obtener nonce",
                [Keys.SimulationFailed] = "Error en la simulación de transacción",
                [Keys.TransactionFailed] = "Transacción fallida",
                
                [Keys.EstimatingGas] = "Estimando gas...",
                [Keys.LoadingNonce] = "Cargando nonce...",
                [Keys.SimulatingTransaction] = "Simulando transacción...",
                [Keys.SendingTransaction] = "Enviando transacción...",
                
                [Keys.GasLimit] = "Límite de Gas",
                [Keys.GasPrice] = "Precio de Gas",
                [Keys.MaxFee] = "Tarifa Máxima",
                [Keys.PriorityFee] = "Tarifa Prioritaria",
                [Keys.Nonce] = "Nonce",
                
                [Keys.TotalGasCost] = "Costo Total de Gas",
                [Keys.TotalTransactionCost] = "Costo Total de Transacción",
                [Keys.DefaultNativeTokenName] = "Éter",
                [Keys.NativeTransferDisplay] = "Transferencia de {0}",
                [Keys.ContractInteractionDisplay] = "Interacción con contrato",
                
                [Keys.CannotSendToSelf] = "No se puede enviar a sí mismo",
                [Keys.FromAddressMismatch] = "La dirección de origen de la transacción no coincide con la cuenta seleccionada",
                [Keys.DataMustStartWith0x] = "Los datos deben empezar con 0x",
                [Keys.NonceMustBeNonNegative] = "El nonce debe ser no negativo",
                
                [Keys.SimulateTransaction] = "Simular Transacción",
                [Keys.RefreshNonce] = "Actualizar Nonce",
                [Keys.EstimateGas] = "Estimar Gas",
                
                [Keys.InvalidTransaction] = "Transacción inválida",
                [Keys.MissingNonce] = "El nonce es requerido",
                [Keys.MissingGasConfiguration] = "La configuración de gas es requerida",
                
                [Keys.TransactionDetails] = "Detalles de la Transacción",
                [Keys.From] = "Desde",
                [Keys.To] = "Para",
                [Keys.Contract] = "Contrato",
                [Keys.Amount] = "Cantidad",
                [Keys.Network] = "Red",
                [Keys.ChainId] = "ID de Cadena",
                [Keys.TransactionParameters] = "Parámetros de Transacción",
                [Keys.TransactionData] = "Datos de Transacción",
                [Keys.DecodingTransaction] = "Decodificando transacción...",
                [Keys.DecodingError] = "Error de Decodificación",
                [Keys.FunctionCall] = "Llamada de Función",
                [Keys.RawTransactionData] = "Datos de Transacción Sin Procesar",
                [Keys.HideRawData] = "Ocultar Datos Sin Procesar",
                [Keys.ShowRawData] = "Mostrar Datos Sin Procesar",
                [Keys.GasConfiguration] = "Configuración de Gas",
                [Keys.GasSlow] = "Lento",
                [Keys.GasNormal] = "Normal",
                [Keys.GasFast] = "Rápido",
                [Keys.GasCustom] = "Personalizado",
                [Keys.GasSpeed] = "Velocidad",
                [Keys.GasSlowDescription] = "Precio de gas más bajo",
                [Keys.GasNormalDescription] = "Precio de gas estándar",
                [Keys.GasFastDescription] = "Precio de gas más alto",
                [Keys.GasCustomDescription] = "Establece tu propio precio de gas",
                [Keys.CostSummary] = "Resumen de Costos",
                [Keys.EstimatedGasFee] = "Tarifa de Gas Estimada",
                [Keys.TotalCost] = "Costo Total",
                
                [Keys.LoadingGasPrices] = "Cargando precios de gas...",
                [Keys.NoValidGasPriceData] = "No hay datos válidos de precio de gas disponibles",
                [Keys.GasStrategyLoadFailed] = "Error al cargar estrategia de gas",
                [Keys.GasConfigurationIssue] = "Problema con la configuración de gas",
                [Keys.RefreshGasStrategiesFailed] = "Error al actualizar estrategias de gas",
                [Keys.InitializationFailed] = "Error en la inicialización de transacción",
                [Keys.NetworkInitializationWarning] = "Advertencia de inicialización de red",
                [Keys.UsingStrategy] = "Usando estrategia",
                [Keys.NetworkBasePriceUseCustom] = "Precio base de la red - usar PERSONALIZADO para ajustar",
                [Keys.UnknownFunction] = "Función Desconocida",
                [Keys.UnknownFunctionDescription] = "Esta firma de función no está en nuestra base de datos",
                [Keys.DataSize] = "Tamaño de Datos",
                [Keys.RawData] = "Datos Sin Procesar",
                [Keys.AdvancedOptions] = "Opciones Avanzadas",
                [Keys.AmountToSend] = "Cantidad a Enviar",
                [Keys.GasFee] = "Tarifa de Gas",
                [Keys.Parameters] = "Parámetros",
                [Keys.Name] = "Nombre",
                [Keys.Type] = "Tipo",
                [Keys.Value] = "Valor",
                [Keys.Balance] = "Balance",
                [Keys.MaxButton] = "Máx",
                
                [Keys.GasMode] = "Modo de Tarifa",
                [Keys.GasModeEip1559] = "EIP-1559",
                [Keys.GasModeLegacy] = "Heredado",
                [Keys.GasPriceAdjustment] = "Ajuste de Precio de Gas",
                [Keys.NetworkGasPrice] = "Precio Base de Red",
                [Keys.YourGasPrice] = "Tu Precio de Gas",
                [Keys.Multiplier08] = "0.8x",
                [Keys.Multiplier10] = "1.0x",
                [Keys.Multiplier12] = "1.2x",
                [Keys.Multiplier08Description] = "20% por debajo de la red - puede tardar más en confirmar",
                [Keys.Multiplier10Description] = "Recomendado por la red - tiempo de confirmación estándar",
                [Keys.Multiplier12Description] = "20% por encima de la red - confirmación más rápida esperada",
                [Keys.Custom] = "Personalizado",
                [Keys.SaveCustomGas] = "Guardar Configuración Personalizada",
                [Keys.RefreshGasPrices] = "Actualizar Precios",
                [Keys.GasCost] = "Costo de Gas",
                [Keys.EIP1559NotAvailable] = "EIP-1559 no disponible, por favor use el modo Legacy",
                [Keys.RecipientRequired] = "La dirección del destinatario es requerida",
                [Keys.InvalidAmount] = "La cantidad debe ser mayor que cero",
                [Keys.InvalidGasConfiguration] = "Configuración de gas inválida",
                [Keys.NonceRequired] = "El nonce es requerido",
                [Keys.ValidationError] = "Error de Validación"
            });
        }
    }
}
