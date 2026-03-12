using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ClaudeMaximus.ViewModels;

namespace ClaudeMaximus.Views;

/// <remarks>Created by Claude</remarks>
public partial class SessionView : UserControl
{
	public SessionView()
	{
		InitializeComponent();

		InputBox.KeyDown += OnInputKeyDown;

		// Auto-scroll to bottom when new messages arrive
		MessageList.Items.CollectionChanged += OnMessagesChanged;
	}

	protected override void OnDataContextChanged(System.EventArgs e)
	{
		base.OnDataContextChanged(e);

		if (DataContext is SessionViewModel vm)
		{
			// Re-subscribe auto-scroll to the new Messages collection
			MessageList.Items.CollectionChanged -= OnMessagesChanged;
			vm.Messages.CollectionChanged += OnMessagesChanged;
		}
	}

	private void OnInputKeyDown(object? sender, KeyEventArgs e)
	{
		if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.Control)
		{
			e.Handled = true;
			if (DataContext is SessionViewModel vm)
				vm.SendCommand.Execute(default)
					.Subscribe(new System.Reactive.AnonymousObserver<System.Reactive.Unit>(_ => { }, _ => { }, () => { }));
		}
	}

	private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.Action == NotifyCollectionChangedAction.Add)
			MessageScroller.ScrollToEnd();
	}
}
