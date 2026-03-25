using openMob.Core.ViewModels;
using Microsoft.Maui.ApplicationModel;
using UXDivers.Popups.Maui;
using UXDivers.Popups.Services;

#if IOS
using CoreGraphics;
using Foundation;
using UIKit;
#endif

namespace openMob.Views.Popups;

/// <summary>
/// Message composer popup — provides a large writing area with session controls,
/// picker buttons, and a send action. Replaces the inline InputBarView.
/// ViewModel initialisation is handled by MauiPopupService before this popup is pushed.
/// </summary>
public partial class MessageComposerSheet : PopupPage
{
#if IOS
    private NSObject? _keyboardWillShowObserver;
    private NSObject? _keyboardWillHideObserver;
    private NSObject? _keyboardWillChangeFrameObserver;
#endif

    /// <summary>Initialises the message composer sheet with its ViewModel.</summary>
    /// <param name="viewModel">The message composer ViewModel.</param>
    public MessageComposerSheet(MessageComposerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    /// <inheritdoc />
    public override void OnAppearing()
    {
        base.OnAppearing();

#if IOS
        RegisterKeyboardObservers();
#endif
    }

    /// <inheritdoc />
    public override void OnDisappearing()
    {
#if IOS
        UnregisterKeyboardObservers();
        ResetComposerTranslation();
#endif

        base.OnDisappearing();
    }

#if IOS
    /// <summary>Registers iOS keyboard notifications for the composer sheet.</summary>
    private void RegisterKeyboardObservers()
    {
        if (_keyboardWillShowObserver is not null)
            return;

        var center = NSNotificationCenter.DefaultCenter;
        _keyboardWillShowObserver = center.AddObserver(UIKeyboard.WillShowNotification, HandleKeyboardNotification);
        _keyboardWillHideObserver = center.AddObserver(UIKeyboard.WillHideNotification, HandleKeyboardNotification);
        _keyboardWillChangeFrameObserver = center.AddObserver(UIKeyboard.WillChangeFrameNotification, HandleKeyboardNotification);
    }

    /// <summary>Unregisters iOS keyboard notifications.</summary>
    private void UnregisterKeyboardObservers()
    {
        var center = NSNotificationCenter.DefaultCenter;

        if (_keyboardWillShowObserver is not null)
        {
            center.RemoveObserver(_keyboardWillShowObserver);
            _keyboardWillShowObserver = null;
        }

        if (_keyboardWillHideObserver is not null)
        {
            center.RemoveObserver(_keyboardWillHideObserver);
            _keyboardWillHideObserver = null;
        }

        if (_keyboardWillChangeFrameObserver is not null)
        {
            center.RemoveObserver(_keyboardWillChangeFrameObserver);
            _keyboardWillChangeFrameObserver = null;
        }
    }

    /// <summary>Resets the composer sheet translation to its resting position.</summary>
    private void ResetComposerTranslation()
    {
        if (ComposerSheetRoot is null)
            return;

        ComposerSheetRoot.TranslationY = 0;
    }

    /// <summary>Handles iOS keyboard show, hide, and frame change notifications.</summary>
    /// <param name="notification">The keyboard notification payload.</param>
    private void HandleKeyboardNotification(NSNotification notification)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (ComposerSheetRoot is null)
                return;

            if (notification.Name == UIKeyboard.WillHideNotification)
            {
                AnimateComposerTranslation(0, notification);
                return;
            }

            if (notification.UserInfo is null ||
                !TryGetKeyboardFrame(notification, out var keyboardFrame) ||
                !TryGetKeyboardAnimation(notification, out var duration, out var curve))
            {
                return;
            }

            var screenHeight = UIScreen.MainScreen.Bounds.Height;
            var overlap = Math.Max(0, screenHeight - keyboardFrame.Y);
            AnimateComposerTranslation(-overlap, duration, curve);
        });
    }

    /// <summary>Extracts the keyboard frame from a notification.</summary>
    private static bool TryGetKeyboardFrame(NSNotification notification, out CGRect keyboardFrame)
    {
        keyboardFrame = CGRect.Empty;

        if (notification.UserInfo is null)
            return false;

        if (notification.UserInfo[UIKeyboard.FrameEndUserInfoKey] is not NSValue frameValue)
            return false;

        keyboardFrame = frameValue.CGRectValue;
        return true;
    }

    /// <summary>Extracts keyboard animation timing from a notification.</summary>
    private static bool TryGetKeyboardAnimation(NSNotification notification, out double duration, out UIViewAnimationOptions options)
    {
        duration = 0.25;
        options = UIViewAnimationOptions.BeginFromCurrentState | UIViewAnimationOptions.AllowUserInteraction;

        if (notification.UserInfo is null)
            return false;

        if (notification.UserInfo[UIKeyboard.AnimationDurationUserInfoKey] is NSNumber durationValue)
            duration = durationValue.DoubleValue;

        if (notification.UserInfo[UIKeyboard.AnimationCurveUserInfoKey] is NSNumber curveValue)
        {
            var curve = (UIViewAnimationCurve)curveValue.Int32Value;
            options |= (UIViewAnimationOptions)((int)curve << 16);
        }

        return true;
    }

    /// <summary>Animates the composer sheet to the requested vertical offset.</summary>
    private void AnimateComposerTranslation(double translationY, NSNotification notification)
    {
        if (!TryGetKeyboardAnimation(notification, out var duration, out var options))
            return;

        AnimateComposerTranslation(translationY, duration, options);
    }

    /// <summary>Animates the composer sheet to the requested vertical offset.</summary>
    private void AnimateComposerTranslation(double translationY, double duration, UIViewAnimationOptions options)
    {
        if (ComposerSheetRoot is null)
            return;

        UIView.Animate(duration, 0, options, () =>
        {
            ComposerSheetRoot.TranslationY = translationY;
        }, completion: null!);
    }
#endif

    /// <summary>Closes the popup when the close button is tapped, saving draft first.</summary>
    private async void OnCloseButtonTapped(object? sender, EventArgs e)
    {
        // Save draft before closing
        if (BindingContext is MessageComposerViewModel vm)
        {
            vm.CloseCommand.Execute(null);
            return;
        }

        await IPopupService.Current.PopAsync(this);
    }
}
