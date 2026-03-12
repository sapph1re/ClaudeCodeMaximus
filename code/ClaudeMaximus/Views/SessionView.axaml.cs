using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ClaudeMaximus.Views;

/// <remarks>Created by Claude</remarks>
public partial class SessionView : UserControl
{
	public SessionView()
	{
		InitializeComponent();

		InputBox.KeyDown += OnInputKeyDown;
	}

	private void OnInputKeyDown(object? sender, KeyEventArgs e)
	{
		if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.Control)
		{
			e.Handled = true;
			SendCurrentInput();
		}
	}

	private void OnSendClicked(object? sender, RoutedEventArgs e)
		=> SendCurrentInput();

	private void SendCurrentInput()
	{
		var text = InputBox.Text?.Trim();
		if (string.IsNullOrEmpty(text))
			return;

		InputBox.Text = string.Empty;
		// TODO Phase 3: forward to ClaudeProcessManager
	}
}
