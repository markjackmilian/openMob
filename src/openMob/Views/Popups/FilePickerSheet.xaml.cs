using openMob.Core.ViewModels;
using UXDivers.Popups.Maui;
using UXDivers.Popups.Services;

namespace openMob.Views.Popups;

/// <summary>
/// File picker popup — displays a searchable list of project files
/// loaded from the opencode server via <see cref="FilePickerViewModel"/>.
/// Triggers file loading via <see cref="FilePickerViewModel.LoadFilesCommand"/>
/// when the popup is navigated to.
/// </summary>
public partial class FilePickerSheet : PopupPage
{
    /// <summary>Initialises the file picker sheet with its ViewModel.</summary>
    /// <param name="viewModel">The file picker ViewModel.</param>
    public FilePickerSheet(FilePickerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    /// <summary>Triggers file loading when the popup appears.</summary>
    public override void OnNavigatedTo(IReadOnlyDictionary<string, object?> parameters)
    {
        base.OnNavigatedTo(parameters);

        if (BindingContext is FilePickerViewModel vm && vm.LoadFilesCommand.CanExecute(null))
        {
            _ = vm.LoadFilesCommand.ExecuteAsync(null);
        }
    }

    /// <summary>Closes the popup when the close button is tapped.</summary>
    private async void OnCloseButtonTapped(object? sender, EventArgs e)
    {
        await IPopupService.Current.PopAsync(this);
    }
}
