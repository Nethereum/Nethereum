using Avalonia;
using Avalonia.Controls;
using System.Collections.Generic;
using System.Windows.Input;
using System.Linq;
using System.Collections;
using System;

namespace Nethereum.Wallet.UI.Components.Avalonia.Views.Shared
{
    public partial class WalletSegmentedControl : UserControl
    {
        public static readonly StyledProperty<IEnumerable> OptionsProperty =
            AvaloniaProperty.Register<WalletSegmentedControl, IEnumerable>(nameof(Options), defaultValue: new List<WalletSegmentOption>());

        public IEnumerable Options
        {
            get => GetValue(OptionsProperty);
            set => SetValue(OptionsProperty, value);
        }

        public static readonly StyledProperty<object> SelectedValueProperty =
            AvaloniaProperty.Register<WalletSegmentedControl, object>(nameof(SelectedValue));

        public object SelectedValue
        {
            get => GetValue(SelectedValueProperty);
            set => SetValue(SelectedValueProperty, value);
        }

        public static readonly StyledProperty<ICommand> SelectedValueChangedCommandProperty =
            AvaloniaProperty.Register<WalletSegmentedControl, ICommand>(nameof(SelectedValueChangedCommand));

        public ICommand SelectedValueChangedCommand
        {
            get => GetValue(SelectedValueChangedCommandProperty);
            set => SetValue(SelectedValueChangedCommandProperty, value);
        }

        public static readonly StyledProperty<bool> FullWidthProperty =
            AvaloniaProperty.Register<WalletSegmentedControl, bool>(nameof(FullWidth), defaultValue: true);

        public bool FullWidth
        {
            get => GetValue(FullWidthProperty);
            set => SetValue(FullWidthProperty, value);
        }

        public static readonly StyledProperty<bool> DisabledProperty =
            AvaloniaProperty.Register<WalletSegmentedControl, bool>(nameof(Disabled));

        public bool Disabled
        {
            get => GetValue(DisabledProperty);
            set => SetValue(DisabledProperty, value);
        }

        public WalletSegmentedControl()
        {
            InitializeComponent();
        }

        public void SelectOption(object value)
        {
            if (!Disabled && !Equals(SelectedValue, value))
            {
                SelectedValue = value;
                foreach (var option in Options.Cast<WalletSegmentOption>())
                {
                    option.IsSelected = Equals(option.Value, value);
                }
                SelectedValueChangedCommand?.Execute(value);
            }
        }
    }

    public class WalletSegmentOption
    {
        public object Value { get; set; } = default!;
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string? Icon { get; set; }
        public bool IsSelected { get; set; }
    }
}
