using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ClaudeMaximus.Views;

/// <remarks>Created by Claude</remarks>
public partial class AutocompletePopup : UserControl
{
	public AutocompletePopup()
	{
		InitializeComponent();
	}

	/// <summary>Single click selects and accepts the suggestion (like a menu).</summary>
	private void OnSuggestionTapped(object? sender, TappedEventArgs e)
	{
		AcceptFromParentSession();
	}

	/// <summary>Double-click also accepts (belt and suspenders).</summary>
	private void OnSuggestionDoubleTapped(object? sender, TappedEventArgs e)
	{
		AcceptFromParentSession();
	}

	/// <summary>
	/// Walk up the visual tree to find the SessionView and call its accept method.
	/// This lets the popup behave like a menu — click an item, it gets inserted.
	/// </summary>
	private void AcceptFromParentSession()
	{
		// Find the SessionView ancestor that owns this popup
		var parent = this.Parent;
		while (parent != null)
		{
			if (parent is SessionView sessionView)
			{
				sessionView.AcceptAutocompleteSuggestionFromPopup();
				return;
			}
			parent = (parent as Avalonia.Visual)?.Parent as Avalonia.Controls.Control
			         ?? (parent as Avalonia.Controls.Primitives.Popup)?.Parent as Avalonia.Controls.Control;
		}
	}
}
