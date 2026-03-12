using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ClaudeMaximus.ViewModels;

namespace ClaudeMaximus.Views;

/// <remarks>Created by Claude</remarks>
public partial class MainWindow : Window
{
	private bool _closeConfirmed;

	public MainWindow()
	{
		InitializeComponent();
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

	// ── Overlay: confirm dialog ───────────────────────────────────────────────

	public async Task<bool> ShowConfirmOverlayAsync(string message, string okLabel = "OK")
	{
		var tcs = new TaskCompletionSource<bool>();

		ConfirmMessage.Text  = message;
		ConfirmOkBtn.Content = okLabel;
		ConfirmCard.IsVisible   = true;
		OverlayPanel.IsVisible  = true;

		void OnOk(object? s, RoutedEventArgs _)     => tcs.TrySetResult(true);
		void OnCancel(object? s, RoutedEventArgs _)  => tcs.TrySetResult(false);
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

	// ── Window drag via menu bar ──────────────────────────────────────────────

	private void OnMenuBarPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		if (e.Source is MenuItem) return;   // let menu items open normally
		if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
			BeginMoveDrag(e);
	}

	protected override void OnClosed(System.EventArgs e)
	{
		if (DataContext is MainWindowViewModel vm)
		{
			var settings = App.Services.GetService(typeof(Services.IAppSettingsService))
				as Services.IAppSettingsService;

			if (settings != null)
			{
				settings.Settings.Window.Width    = Width;
				settings.Settings.Window.Height   = Height;
				settings.Settings.Window.Left     = Position.X;
				settings.Settings.Window.Top      = Position.Y;
				settings.Settings.Window.SplitterPosition = vm.SplitterPosition;
				settings.Save();
			}
		}

		base.OnClosed(e);
	}
}
