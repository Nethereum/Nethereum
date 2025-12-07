using Microsoft.AspNetCore.Components;
using MudBlazor;
using Nethereum.Wallet.UI.Components.Core.Localization;
using System.Collections.Generic;

namespace Nethereum.Wallet.UI.Components.Blazor.Shared
{
    public abstract class WalletFormControlBase : ComponentBase
    {
        #region Localization Parameters
        
        [Parameter] public string LabelKey { get; set; } = "";
        [Parameter] public string HelpKey { get; set; } = "";
        [Parameter] public string PlaceholderKey { get; set; } = "";
        [Parameter] public string RequiredErrorKey { get; set; } = "";
        
        [Parameter] public string Label { get; set; } = "";
        [Parameter] public string HelpText { get; set; } = "";
        [Parameter] public string Placeholder { get; set; } = "";
        [Parameter] public string RequiredError { get; set; } = "";
        
        #endregion

        #region Common Form Parameters
        
        [Parameter] public bool Required { get; set; }
        [Parameter] public bool Loading { get; set; }
        [Parameter] public bool Disabled { get; set; }
        [Parameter] public bool ReadOnly { get; set; }
        [Parameter] public string Class { get; set; } = "";
        [Parameter] public string Style { get; set; } = "";
        
        #endregion

        #region Pass-through Parameters
        [Parameter(CaptureUnmatchedValues = true)] 
        public Dictionary<string, object> AdditionalAttributes { get; set; } = new();
        
        #endregion

        #region Localization Parameters
        [Parameter] public IComponentLocalizer? Localizer { get; set; }
        
        #endregion

        #region Helper Methods
        protected string GetLabel()
        {
            if (!string.IsNullOrEmpty(Label))
                return Label;
                
            if (!string.IsNullOrEmpty(LabelKey) && Localizer != null)
                return Localizer.GetString(LabelKey);
                
            return "";
        }
        protected string GetHelpText()
        {
            if (!string.IsNullOrEmpty(HelpText))
                return HelpText;
                
            if (!string.IsNullOrEmpty(HelpKey) && Localizer != null)
                return Localizer.GetString(HelpKey);
                
            return "";
        }
        protected string GetPlaceholder()
        {
            if (!string.IsNullOrEmpty(Placeholder))
                return Placeholder;
                
            if (!string.IsNullOrEmpty(PlaceholderKey) && Localizer != null)
                return Localizer.GetString(PlaceholderKey);
                
            return "";
        }
        protected string GetRequiredError()
        {
            if (!string.IsNullOrEmpty(RequiredError))
                return RequiredError;
                
            if (!string.IsNullOrEmpty(RequiredErrorKey) && Localizer != null)
                return Localizer.GetString(RequiredErrorKey);
                
            return "This field is required";
        }
        protected virtual Variant GetVariant() => Variant.Outlined;
        protected virtual string GetClasses()
        {
            var classes = new List<string> { "wallet-modern-input" };
            
            if (Loading)
                classes.Add("wallet-loading");
                
            if (ReadOnly)
                classes.Add("wallet-readonly");
                
            if (!string.IsNullOrEmpty(Class))
                classes.Add(Class);
                
            return string.Join(" ", classes);
        }
        protected virtual bool IsDisabled() => Disabled || Loading;
        
        #endregion
    }
}