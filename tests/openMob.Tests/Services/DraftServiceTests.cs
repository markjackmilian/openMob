using openMob.Core.Services;

namespace openMob.Tests.Services;

/// <summary>
/// Unit tests for <see cref="DraftService"/>.
/// Covers get/save/clear operations, overwrite behaviour, and argument validation.
/// </summary>
public sealed class DraftServiceTests
{
    private readonly IDraftService _sut;

    public DraftServiceTests()
    {
        // DraftService is internal — instantiate via the interface cast.
        // We use reflection or the InternalsVisibleTo approach.
        // Since DraftService is internal, we create it through the concrete type
        // accessible via the project reference (InternalsVisibleTo is set).
        _sut = new DraftService();
    }

    // ─── GetDraft ────────────────────────────────────────────────────────────

    [Fact]
    public void GetDraft_WhenNoExistingDraft_ReturnsNull()
    {
        // Act
        var result = _sut.GetDraft("unknown-session");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetDraft_WhenDraftExists_ReturnsSavedText()
    {
        // Arrange
        _sut.SaveDraft("session-1", "Hello world");

        // Act
        var result = _sut.GetDraft("session-1");

        // Assert
        result.Should().Be("Hello world");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetDraft_WhenSessionIdIsNullOrWhiteSpace_ThrowsArgumentException(string? sessionId)
    {
        // Act
        var act = () => _sut.GetDraft(sessionId!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    // ─── SaveDraft ───────────────────────────────────────────────────────────

    [Fact]
    public void SaveDraft_WhenCalled_StoresTextRetrievableByGetDraft()
    {
        // Act
        _sut.SaveDraft("session-1", "Draft text");

        // Assert
        _sut.GetDraft("session-1").Should().Be("Draft text");
    }

    [Fact]
    public void SaveDraft_WhenCalledTwice_OverwritesExistingDraft()
    {
        // Arrange
        _sut.SaveDraft("session-1", "First draft");

        // Act
        _sut.SaveDraft("session-1", "Second draft");

        // Assert
        _sut.GetDraft("session-1").Should().Be("Second draft");
    }

    [Fact]
    public void SaveDraft_WhenTextIsEmptyString_StoresEmptyString()
    {
        // Act
        _sut.SaveDraft("session-1", string.Empty);

        // Assert
        _sut.GetDraft("session-1").Should().Be(string.Empty);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SaveDraft_WhenSessionIdIsNullOrWhiteSpace_ThrowsArgumentException(string? sessionId)
    {
        // Act
        var act = () => _sut.SaveDraft(sessionId!, "text");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SaveDraft_WhenTextIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.SaveDraft("session-1", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    // ─── ClearDraft ──────────────────────────────────────────────────────────

    [Fact]
    public void ClearDraft_WhenDraftExists_RemovesDraft()
    {
        // Arrange
        _sut.SaveDraft("session-1", "Draft text");

        // Act
        _sut.ClearDraft("session-1");

        // Assert
        _sut.GetDraft("session-1").Should().BeNull();
    }

    [Fact]
    public void ClearDraft_WhenNoDraftExists_DoesNotThrow()
    {
        // Act
        var act = () => _sut.ClearDraft("non-existent-session");

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ClearDraft_WhenSessionIdIsNullOrWhiteSpace_ThrowsArgumentException(string? sessionId)
    {
        // Act
        var act = () => _sut.ClearDraft(sessionId!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    // ─── Isolation between sessions ──────────────────────────────────────────

    [Fact]
    public void SaveDraft_ForDifferentSessions_StoresIndependently()
    {
        // Arrange
        _sut.SaveDraft("session-1", "Draft A");
        _sut.SaveDraft("session-2", "Draft B");

        // Act & Assert
        _sut.GetDraft("session-1").Should().Be("Draft A");
        _sut.GetDraft("session-2").Should().Be("Draft B");
    }

    [Fact]
    public void ClearDraft_ForOneSession_DoesNotAffectOtherSessions()
    {
        // Arrange
        _sut.SaveDraft("session-1", "Draft A");
        _sut.SaveDraft("session-2", "Draft B");

        // Act
        _sut.ClearDraft("session-1");

        // Assert
        _sut.GetDraft("session-1").Should().BeNull();
        _sut.GetDraft("session-2").Should().Be("Draft B");
    }
}
