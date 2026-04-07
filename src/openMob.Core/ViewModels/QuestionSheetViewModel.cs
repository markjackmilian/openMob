using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace openMob.Core.ViewModels;

/// <summary>
/// ViewModel for the question bottom sheet popup.
/// Displays the full question text, option chips, and optional free-text input.
/// </summary>
/// <remarks>
/// <para>
/// This ViewModel takes callbacks (<paramref name="onAnswerSubmitted"/>, <paramref name="onDismiss"/>)
/// instead of injecting services directly, following the pattern used by other sheet ViewModels
/// (e.g. <c>ModelPickerSheet</c> uses <c>Action&lt;string&gt;</c> callbacks).
/// </para>
/// <para>
/// The ViewModel does NOT call <c>ReplyToQuestionAsync</c> directly — the callback delegates
/// to <c>ChatViewModel.AnswerQuestionAsync</c> which already handles the API call, Sentry capture,
/// card resolution, and <c>IsAiResponding</c> flag.
/// </para>
/// </remarks>
public sealed partial class QuestionSheetViewModel : ObservableObject
{
    private readonly Func<string, string, Task> _onAnswerSubmitted;
    private readonly Func<Task> _onDismiss;

    /// <summary>Gets the question request identifier.</summary>
    public string QuestionId { get; }

    /// <summary>Gets the full question text.</summary>
    public string QuestionText { get; }

    /// <summary>Gets the predefined answer options.</summary>
    public IReadOnlyList<string> Options { get; }

    /// <summary>Gets a value indicating whether free-text input is allowed.</summary>
    public bool AllowFreeText { get; }

    /// <summary>Gets or sets the free-text answer typed by the user.</summary>
    [ObservableProperty]
    private string _freeTextAnswer = string.Empty;

    /// <summary>Gets or sets whether an answer submission is in progress.</summary>
    [ObservableProperty]
    private bool _isSubmitting;

    /// <summary>Gets a value indicating whether the options list has items.</summary>
    public bool HasOptions => Options.Count > 0;

    /// <summary>
    /// Initializes a new instance of <see cref="QuestionSheetViewModel"/>.
    /// </summary>
    /// <param name="questionId">The question request identifier.</param>
    /// <param name="questionText">The full question text.</param>
    /// <param name="options">The predefined answer options.</param>
    /// <param name="allowFreeText">Whether free-text input is allowed.</param>
    /// <param name="onAnswerSubmitted">Callback invoked with (questionId, answer) when the user submits an answer.</param>
    /// <param name="onDismiss">Callback to dismiss the sheet.</param>
    public QuestionSheetViewModel(
        string questionId,
        string questionText,
        IReadOnlyList<string> options,
        bool allowFreeText,
        Func<string, string, Task> onAnswerSubmitted,
        Func<Task> onDismiss)
    {
        ArgumentNullException.ThrowIfNull(questionId);
        ArgumentNullException.ThrowIfNull(questionText);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(onAnswerSubmitted);
        ArgumentNullException.ThrowIfNull(onDismiss);

        QuestionId = questionId;
        QuestionText = questionText;
        Options = options;
        AllowFreeText = allowFreeText;
        _onAnswerSubmitted = onAnswerSubmitted;
        _onDismiss = onDismiss;
    }

    /// <summary>
    /// Submits a predefined option as the answer.
    /// </summary>
    /// <param name="option">The selected option text.</param>
    [RelayCommand]
    private async Task SelectOptionAsync(string option)
    {
        if (IsSubmitting || string.IsNullOrWhiteSpace(option))
            return;

        IsSubmitting = true;
        try
        {
            await _onAnswerSubmitted(QuestionId, option);
            await _onDismiss();
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    /// <summary>
    /// Submits the free-text answer.
    /// </summary>
    [RelayCommand]
    private async Task SubmitFreeTextAsync()
    {
        if (IsSubmitting || string.IsNullOrWhiteSpace(FreeTextAnswer))
            return;

        IsSubmitting = true;
        try
        {
            await _onAnswerSubmitted(QuestionId, FreeTextAnswer.Trim());
            await _onDismiss();
        }
        finally
        {
            IsSubmitting = false;
        }
    }
}
