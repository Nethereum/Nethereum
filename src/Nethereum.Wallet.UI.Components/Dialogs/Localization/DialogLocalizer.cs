using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.Dialogs.Localization
{
    public class DialogLocalizer : ComponentLocalizerBase<object>
    {
        public static class Keys
        {
            public const string ConfirmAction = "ConfirmAction";
            public const string CancelButton = "CancelButton";
            public const string ConfirmButton = "ConfirmButton";
            public const string SaveButton = "SaveButton";
            public const string DeleteButton = "DeleteButton";
            public const string CloseButton = "CloseButton";
            public const string OkButton = "OkButton";
            
            public const string ConfirmDeleteTitle = "ConfirmDeleteTitle";
            public const string ActionCannotBeUndone = "ActionCannotBeUndone";
            public const string ImportantWarning = "ImportantWarning";
            public const string UnderstandUndoWarning = "UnderstandUndoWarning";
            
            public const string Success = "Success";
            public const string Error = "Error";
            public const string Warning = "Warning";
            public const string Information = "Information";
            public const string Loading = "Loading";
            public const string ProcessingRequest = "ProcessingRequest";
            public const string OperationCompleted = "OperationCompleted";
            public const string OperationFailed = "OperationFailed";
            
            public const string RequiredField = "RequiredField";
            public const string InvalidInput = "InvalidInput";
            public const string FieldTooShort = "FieldTooShort";
            public const string FieldTooLong = "FieldTooLong";
            public const string InvalidFormat = "InvalidFormat";
        }
        
        public DialogLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }
        
        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.ConfirmAction] = "Confirm Action",
                [Keys.CancelButton] = "Cancel",
                [Keys.ConfirmButton] = "Confirm",
                [Keys.SaveButton] = "Save",
                [Keys.DeleteButton] = "Delete",
                [Keys.CloseButton] = "Close",
                [Keys.OkButton] = "OK",
                
                [Keys.ConfirmDeleteTitle] = "Confirm Delete",
                [Keys.ActionCannotBeUndone] = "This action cannot be undone",
                [Keys.ImportantWarning] = "Important:",
                [Keys.UnderstandUndoWarning] = "I understand this action cannot be undone",
                
                [Keys.Success] = "Success",
                [Keys.Error] = "Error",
                [Keys.Warning] = "Warning",
                [Keys.Information] = "Information",
                [Keys.Loading] = "Loading...",
                [Keys.ProcessingRequest] = "Processing request...",
                [Keys.OperationCompleted] = "Operation completed successfully",
                [Keys.OperationFailed] = "Operation failed: {0}",
                
                [Keys.RequiredField] = "This field is required",
                [Keys.InvalidInput] = "Invalid input",
                [Keys.FieldTooShort] = "Field must be at least {0} characters",
                [Keys.FieldTooLong] = "Field cannot exceed {0} characters",
                [Keys.InvalidFormat] = "Invalid format"
            });
            
            // Spanish (Spain) translations
            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.ConfirmAction] = "Confirmar Acción",
                [Keys.CancelButton] = "Cancelar",
                [Keys.ConfirmButton] = "Confirmar",
                [Keys.SaveButton] = "Guardar",
                [Keys.DeleteButton] = "Eliminar",
                [Keys.CloseButton] = "Cerrar",
                [Keys.OkButton] = "Aceptar",
                
                [Keys.ConfirmDeleteTitle] = "Confirmar Eliminación",
                [Keys.ActionCannotBeUndone] = "Esta acción no se puede deshacer",
                [Keys.ImportantWarning] = "Importante:",
                [Keys.UnderstandUndoWarning] = "Entiendo que esta acción no se puede deshacer",
                
                [Keys.Success] = "Éxito",
                [Keys.Error] = "Error",
                [Keys.Warning] = "Advertencia",
                [Keys.Information] = "Información",
                [Keys.Loading] = "Cargando...",
                [Keys.ProcessingRequest] = "Procesando solicitud...",
                [Keys.OperationCompleted] = "Operación completada exitosamente",
                [Keys.OperationFailed] = "La operación falló: {0}",
                
                [Keys.RequiredField] = "Este campo es requerido",
                [Keys.InvalidInput] = "Entrada inválida",
                [Keys.FieldTooShort] = "El campo debe tener al menos {0} caracteres",
                [Keys.FieldTooLong] = "El campo no puede exceder {0} caracteres",
                [Keys.InvalidFormat] = "Formato inválido"
            });
        }
    }
}