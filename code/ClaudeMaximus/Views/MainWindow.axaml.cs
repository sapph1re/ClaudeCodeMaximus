using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ClaudeMaximus.Services;
using ClaudeMaximus.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudeMaximus.Views;

/// <remarks>Created by Claude</remarks>
public partial class MainWindow : Window
{
	private bool _closeConfirmed;
	private PixelPoint? _pendingPosition;

	private bool _pendingMaximized;

	public MainWindow()
	{
		InitializeComponent();
		BuildNumberText.Text = GetBuildNumber();

		var ws = App.Services.GetRequiredService<IAppSettingsService>().Settings.Window;

		Width  = ws.Width;
		Height = ws.Height;
		_pendingMaximized = ws.IsMaximized;

		MainContentGrid.ColumnDefinitions[0].Width = new GridLength(
			Math.Clamp(ws.SplitterPosition, 180, 600));

		// Validate saved position against current screens; defer actual positioning to Opened
		_pendingPosition = ValidatePosition((int)ws.Left, (int)ws.Top, Width, Height);

		Opened += OnWindowOpened;
	}

	private void OnWindowOpened(object? sender, EventArgs e)
	{
		Opened -= OnWindowOpened;

		// Apply validated position now that the window handle exists
		if (_pendingPosition.HasValue)
		{
			Position = _pendingPosition.Value;
			_pendingPosition = null;
		}

		// Restore maximized state after position is set so RestoreBounds are correct
		if (_pendingMaximized)
			WindowState = WindowState.Maximized;

		// Restore active session selection now that the tree UI is ready
		if (DataContext is MainWindowViewModel vm)
			vm.RestoreActiveSession();
	}

	/// <summary>
	/// Checks if the saved window center lands on any connected screen.
	/// If not, falls back to primary screen center.
	/// </summary>
	private PixelPoint ValidatePosition(int left, int top, double width, double height)
	{
		var centerX = left + (int)(width / 2);
		var centerY = top + (int)(height / 2);

		foreach (var screen in Screens.All)
		{
			if (screen.Bounds.Contains(new PixelPoint(centerX, centerY)))
				return new PixelPoint(left, top);
		}

		// Saved position is off-screen — center on primary
		var primary = Screens.Primary?.Bounds ?? new PixelRect(0, 0, 1920, 1080);
		var x = primary.X + (primary.Width - (int)width) / 2;
		var y = primary.Y + (primary.Height - (int)height) / 2;
		return new PixelPoint(x, y);
	}

	protected override async void OnClosing(WindowClosingEventArgs e)
	{
		base.OnClosing(e);

		if (_closeConfirmed) return;
		if (DataContext is not MainWindowViewModel vm) return;

		var count = vm.ActiveSessionCount;
		if (count == 0) return;

		e.Cancel = true;

		var noun      = count == 1 ? "session is" : "sessions are";
		var message   = $"There are {count} Claude Code {noun} currently active.\nAre you sure you want to terminate them and close?";
		var confirmed = await ShowConfirmOverlayAsync(message, "Yes, close");

		if (!confirmed) return;

		vm.TerminateAllSessions();
		_closeConfirmed = true;
		Close();
	}

	protected override void OnClosed(EventArgs e)
	{
		// Save scroll position of the active session view before closing
		SaveActiveSessionScrollPosition();

		var settings = App.Services.GetRequiredService<IAppSettingsService>();
		var ws       = settings.Settings.Window;

		var isMaximized = WindowState == WindowState.Maximized;
		ws.IsMaximized = isMaximized;

		// Always save position (needed to know which screen the window is on).
		// Only save size when not maximized, so the normal/restored bounds are preserved.
		ws.Left = Position.X;
		ws.Top  = Position.Y;

		if (!isMaximized)
		{
			ws.Width  = Width;
			ws.Height = Height;
		}

		ws.SplitterPosition = MainContentGrid.ColumnDefinitions[0].Width.Value;

		settings.Save();

		base.OnClosed(e);
	}

	private void SaveActiveSessionScrollPosition()
	{
		// Find the active SessionView and capture its scroll offset
		var sessionView = this.FindDescendantOfType<SessionView>();
		if (sessionView?.DataContext is SessionViewModel vm)
			vm.ScrollOffset = sessionView.FindDescendantOfType<ScrollViewer>()?.Offset.Y ?? 0;
	}

	// ── Overlay: confirm dialog ───────────────────────────────────────────────

	public async Task<bool> ShowConfirmOverlayAsync(string message, string okLabel = "OK")
	{
		var tcs = new TaskCompletionSource<bool>();

		ConfirmMessage.Text  = message;
		ConfirmOkBtn.Content = okLabel;
		ConfirmCard.IsVisible   = true;
		OverlayPanel.IsVisible  = true;

		void OnOk(object? s, RoutedEventArgs _)    => tcs.TrySetResult(true);
		void OnCancel(object? s, RoutedEventArgs _) => tcs.TrySetResult(false);
		void OnKeyDown(object? s, KeyEventArgs e)
		{
			if (e.Key == Key.Escape) tcs.TrySetResult(false);
		}

		ConfirmOkBtn.Click     += OnOk;
		ConfirmCancelBtn.Click += OnCancel;
		this.KeyDown           += OnKeyDown;

		var result = await tcs.Task;

		ConfirmOkBtn.Click     -= OnOk;
		ConfirmCancelBtn.Click -= OnCancel;
		this.KeyDown           -= OnKeyDown;

		OverlayPanel.IsVisible = false;
		ConfirmCard.IsVisible  = false;

		return result;
	}

	// ── Overlay: input dialog ─────────────────────────────────────────────────

	public async Task<string?> ShowInputOverlayAsync(string title, string prompt, string? initialValue = null)
	{
		var tcs = new TaskCompletionSource<string?>();

		InputCardTitle.Text    = title;
		InputCardPrompt.Text   = prompt;
		InputCardBox.Text      = initialValue ?? string.Empty;
		InputCard.IsVisible    = true;
		OverlayPanel.IsVisible = true;
		InputCardBox.Focus();
		InputCardBox.SelectAll();

		void Submit()
		{
			var text = InputCardBox.Text?.Trim();
			if (!string.IsNullOrEmpty(text))
				tcs.TrySetResult(text);
		}

		void OnOk(object? s, RoutedEventArgs _)    => Submit();
		void OnCancel(object? s, RoutedEventArgs _) => tcs.TrySetResult(null);
		void OnKeyDown(object? s, KeyEventArgs e)
		{
			if (e.Key == Key.Enter)  { e.Handled = true; Submit(); }
			if (e.Key == Key.Escape) { e.Handled = true; tcs.TrySetResult(null); }
		}

		InputOkBtn.Click     += OnOk;
		InputCancelBtn.Click += OnCancel;
		InputCardBox.KeyDown += OnKeyDown;

		var result = await tcs.Task;

		InputOkBtn.Click     -= OnOk;
		InputCancelBtn.Click -= OnCancel;
		InputCardBox.KeyDown -= OnKeyDown;

		OverlayPanel.IsVisible = false;
		InputCard.IsVisible    = false;

		return result;
	}

	// ── Window drag via title bar ─────────────────────────────────────────────

	private void OnMenuBarPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		if (IsTitleBarControl(e.Source)) return;
		if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
			BeginMoveDrag(e);
	}

	private static bool IsTitleBarControl(object? source)
	{
		var visual = source as Visual;
		while (visual != null)
		{
			if (visual is Button or MenuItem) return true;
			visual = visual.GetVisualParent();
		}
		return false;
	}

	// ── Window control buttons ────────────────────────────────────────────────

	private void OnMinimizeClick(object? sender, RoutedEventArgs e) =>
		WindowState = WindowState.Minimized;

	private void OnMaximizeRestoreClick(object? sender, RoutedEventArgs e) =>
		WindowState = WindowState == WindowState.Maximized
			? WindowState.Normal
			: WindowState.Maximized;

	private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

	private static string GetBuildNumber()
	{
		try
		{
			var assembly = Assembly.GetExecutingAssembly();
			var location = assembly.Location;
			if (!string.IsNullOrEmpty(location) && File.Exists(location))
			{
				var buildTime = File.GetLastWriteTime(location);
				return buildTime.ToString("yyyyMM.dd.HHmm");
			}

			// Fallback for single-file publish: use entry assembly
			var entry = Assembly.GetEntryAssembly();
			if (entry?.Location is { Length: > 0 } entryLoc && File.Exists(entryLoc))
			{
				var buildTime = File.GetLastWriteTime(entryLoc);
				return buildTime.ToString("yyyyMM.dd.HHmm");
			}
		}
		catch
		{
			// Ignore — build number is cosmetic.
		}

		return string.Empty;
	}
}
