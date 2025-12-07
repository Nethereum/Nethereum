using Avalonia.VisualTree;
using Avalonia;
using Avalonia.Controls;
using Nethereum.Wallet.UI.Components.Shared;
using Nethereum.Wallet.UI.Components.Core.Localization;
using System.Windows.Input;
using ReactiveUI;
using System.Reactive.Linq;

namespace Nethereum.Wallet.UI.Components.Avalonia.Views.Shared
{
    public partial class NotificationBadge : UserControl
    {
        public static readonly StyledProperty<NotificationBadgeViewModel> ViewModelProperty =
            AvaloniaProperty.Register<NotificationBadge, NotificationBadgeViewModel>(nameof(ViewModel));

        public NotificationBadgeViewModel ViewModel
        {
            get => GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        private readonly IComponentLocalizer<NotificationBadgeViewModel> _localizer;

        public ICommand HandleNotificationClickCommand { get; }

        public NotificationBadge()
        {
            InitializeComponent();
        }

        public NotificationBadge(IComponentLocalizer<NotificationBadgeViewModel> localizer)
        {
            InitializeComponent();
            _localizer = localizer;
            this.AttachedToVisualTree += OnAttachedToVisualTree;
            this.DetachedFromVisualTree += OnDetachedFromVisualTree;

            HandleNotificationClickCommand = ReactiveCommand.CreateFromTask(HandleNotificationClick);
        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            ViewModel.Initialize();
            ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
                ViewModel.Dispose();
            }
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Force UI update when ViewModel properties change
            // In Avalonia, if properties are Observable, this might not be strictly necessary
            // but it ensures consistency with Blazor's StateHasChanged
            InvalidateVisual();
        }

        private async Task HandleNotificationClick()
        {
            ViewModel.NavigateToPromptsCommand.Execute(null);
        }

        public string GetNotificationTitle()
        {
            if (ViewModel.NotificationCount == 1)
            {
                return _localizer.GetString(NotificationBadgeLocalizer.Keys.SingleNotification);
            }
            else
            {
                return _localizer.GetString(NotificationBadgeLocalizer.Keys.MultipleNotifications, ViewModel.NotificationCount.ToString());
            }
        }
    }
}
