using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.Core.Validation
{
    public abstract class LocalizedValidationModel : ObservableValidator
    {
        private readonly IComponentLocalizer _localizer;
        private readonly Dictionary<string, string> _customErrors = new Dictionary<string, string>();
        
        protected LocalizedValidationModel(IComponentLocalizer localizer)
        {
            _localizer = localizer;
        }
        
        public string GetLocalizedString(string key) => _localizer.GetString(key);
        
        public void SetFieldError(string fieldName, string? localizationKey)
        {
            if (localizationKey != null)
                _customErrors[fieldName] = _localizer.GetString(localizationKey);
            else
                _customErrors.Remove(fieldName);
        }
        
        public void ValidateField(string fieldName, params (bool condition, string errorKey)[] rules)
        {
            var errorKey = rules.FirstOrDefault(r => r.condition).errorKey;
            SetFieldError(fieldName, errorKey);
        }
        
        protected void AddCustomError(string errorMessage, string propertyName)
        {
            var localizedMessage = _localizer.GetString(errorMessage);
            _customErrors[propertyName] = localizedMessage;
            
            OnPropertyChanged(propertyName);
        }
        
        protected void ClearCustomErrors(string propertyName)
        {
            if (_customErrors.Remove(propertyName))
            {
                OnPropertyChanged(propertyName);
            }
        }
        
        public new System.Collections.IEnumerable GetErrors(string? propertyName)
        {
            var errors = new List<string>();
            
            // Get validation errors from ObservableValidator attributes
            var validationErrors = base.GetErrors(propertyName);
            if (validationErrors != null)
            {
                foreach (var error in validationErrors)
                {
                    string? errorMessage = null;

                    if (error != null)
                    {
                        if (error.GetType().Name == "ValidationResult")
                        {
                            var errorMessageProperty = error.GetType().GetProperty("ErrorMessage");
                            errorMessage = errorMessageProperty?.GetValue(error) as string;
                        }
                        else if (error is string)
                        {
                            errorMessage = error.ToString();
                        }
                        else
                        {
                            errorMessage = error.ToString();
                        }
                    }

                    if (errorMessage != null)
                    {
                        var localizedMessage = _localizer.GetString(errorMessage);
                        errors.Add(localizedMessage);
                    }
                }
            }

                if (string.IsNullOrEmpty(propertyName))
            {
                errors.AddRange(_customErrors.Values);
            }
            else if (_customErrors.TryGetValue(propertyName, out var customError))
            {
                errors.Add(customError);
            }
            
            return errors;
        }
        
        public new bool HasErrors => base.HasErrors || _customErrors.Any();
        
        protected void ClearErrors()
        {
            _customErrors.Clear();
        }
        
        public string? GetFieldError(string fieldName)
        {
            if (_customErrors.TryGetValue(fieldName, out var customError))
                return customError;
            
            var validationErrors = GetErrors(fieldName);
            if (validationErrors != null)
            {
                var firstError = validationErrors.Cast<string>().FirstOrDefault();
                return firstError;
            }
            
            return null;
        }
        
        public bool HasFieldErrors(string propertyName)
        {
            if (_customErrors.ContainsKey(propertyName))
                return true;
                
            var validationErrors = GetErrors(propertyName);
            return validationErrors != null && validationErrors.Cast<object>().Any();
        }
    }
}
