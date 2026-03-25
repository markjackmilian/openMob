using System.Globalization;
using openMob.Core.Infrastructure.Localization;

namespace openMob.Tests.Infrastructure.Localization;

/// <summary>
/// Unit tests for <see cref="LocalizationHelper"/>.
/// </summary>
public sealed class LocalizationHelperTests
{
    [Theory]
    [InlineData("en", "en")]
    [InlineData("it", "it")]
    [InlineData("IT", "it")]
    [InlineData(null, "en")]
    [InlineData("fr", "en")]
    public void ResolveCulture_WhenLanguageCodeProvided_ReturnsExpectedCulture(string? languageCode, string expectedName)
    {
        // Act
        var culture = LocalizationHelper.ResolveCulture(languageCode);

        // Assert
        culture.Name.Should().Be(expectedName);
    }

    [Fact]
    public void ApplyCulture_WhenCalled_SetsCurrentCultureAndCurrentUICulture()
    {
        // Arrange
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;

        // Act
        LocalizationHelper.ApplyCulture("it");

        // Assert
        CultureInfo.CurrentCulture.Name.Should().Be("it");
        CultureInfo.CurrentUICulture.Name.Should().Be("it");

        // Cleanup
        CultureInfo.CurrentCulture = previousCulture;
        CultureInfo.CurrentUICulture = previousUiCulture;
        CultureInfo.DefaultThreadCurrentCulture = previousCulture;
        CultureInfo.DefaultThreadCurrentUICulture = previousUiCulture;
    }
}
