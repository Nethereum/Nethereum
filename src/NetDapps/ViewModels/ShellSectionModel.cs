using CommunityToolkit.Mvvm.ComponentModel;

namespace NetDapps.ViewModels;

public partial class ShellSectionModel : ObservableObject
{
    [ObservableProperty]
    private int badgeCount;

    public string Title { get; }
    public string Icon { get; }
    public string Route { get; }

    public ShellSectionModel(string title, string icon, string route)
    {
        Title = title;
        Icon = icon;
        Route = route;
    }
}
