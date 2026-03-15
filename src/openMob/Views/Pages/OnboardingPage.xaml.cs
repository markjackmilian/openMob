using openMob.Core.ViewModels;

namespace openMob.Views.Pages;

/// <summary>Onboarding page — 5-step linear setup flow.</summary>
public partial class OnboardingPage : ContentPage
{
    /// <summary>Initialises the onboarding page with its ViewModel.</summary>
    /// <param name="viewModel">The onboarding ViewModel.</param>
    public OnboardingPage(OnboardingViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
