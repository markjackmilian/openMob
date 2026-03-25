using Microsoft.Maui.Controls.Xaml;
using openMob.Core.Localization;

namespace openMob.Localization;

/// <summary>
/// XAML markup extension that resolves localized strings from <see cref="AppResources"/>.
/// </summary>
[ContentProperty(nameof(Key))]
public sealed class TranslateExtension : IMarkupExtension<string>
{
    /// <summary>
    /// Gets or sets the resource key.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <inheritdoc />
    public string ProvideValue(IServiceProvider serviceProvider)
        => AppResources.Get(Key);

    /// <inheritdoc />
    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider)
        => ProvideValue(serviceProvider);
}
