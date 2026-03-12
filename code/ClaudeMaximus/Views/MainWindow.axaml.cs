using Avalonia;
using Avalonia.Controls;
using ClaudeMaximus.ViewModels;

namespace ClaudeMaximus.Views;

/// <remarks>Created by Claude</remarks>
public partial class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();
	}

	protected override void OnClosed(System.EventArgs e)
	{
		if (DataContext is MainWindowViewModel vm)
		{
			var settings = App.Services.GetService(typeof(Services.IAppSettingsService))
				as Services.IAppSettingsService;

			if (settings != null)
			{
				settings.Settings.Window.Width = Width;
				settings.Settings.Window.Height = Height;
				settings.Settings.Window.Left = Position.X;
				settings.Settings.Window.Top = Position.Y;
				settings.Settings.Window.SplitterPosition = vm.SplitterPosition;
				settings.Save();
			}
		}

		base.OnClosed(e);
	}
}
