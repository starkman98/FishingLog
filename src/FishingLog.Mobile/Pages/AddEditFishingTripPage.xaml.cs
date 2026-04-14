using FishingLog.Mobile.ViewModels;

namespace FishingLog.Mobile.Pages;

/// <summary>
/// Code-behind for the add/edit fishing trip form page.
/// </summary>
public partial class AddEditFishingTripPage : ContentPage
{
    public AddEditFishingTripPage(AddEditFishingTripViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}