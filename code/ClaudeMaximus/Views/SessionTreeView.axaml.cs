using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ClaudeMaximus.ViewModels;

namespace ClaudeMaximus.Views;

/// <remarks>Created by Claude</remarks>
public partial class SessionTreeView : UserControl
{
	public SessionTreeView()
	{
		InitializeComponent();
	}

	// ── Add Directory ────────────────────────────────────────────────────────

	private async void OnAddDirectoryClicked(object? sender, RoutedEventArgs e)
	{
		if (DataContext is not SessionTreeViewModel vm)
			return;

		var topLevel = TopLevel.GetTopLevel(this);
		if (topLevel == null)
			return;

		var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
			new Avalonia.Platform.Storage.FolderPickerOpenOptions
			{
				Title       = "Select Working Directory",
				AllowMultiple = false,
			});

		if (folders.Count == 0)
			return;

		vm.AddDirectory(folders[0].Path.LocalPath);
	}

	// ── Tree selection ───────────────────────────────────────────────────────

	private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (DataContext is not SessionTreeViewModel vm)
			return;

		vm.SelectedSession = Tree.SelectedItem is SessionNodeViewModel session ? session : null;
	}

	// ── Context menu ─────────────────────────────────────────────────────────

	private async void OnContextMenuClicked(object? sender, RoutedEventArgs e)
	{
		if (sender is not MenuItem mi || DataContext is not SessionTreeViewModel vm)
			return;

		var action    = mi.Tag as string;
		var ownerVm   = GetContextMenuOwnerDataContext(mi);
		var ownerWindow = TopLevel.GetTopLevel(this) as Window;

		switch (action)
		{
			case "AddGroupToDirectory" when ownerVm is DirectoryNodeViewModel dir:
				await AddGroupToDirectoryAsync(vm, dir, ownerWindow);
				break;

			case "AddSessionToDirectory" when ownerVm is DirectoryNodeViewModel dir:
				await AddSessionToDirectoryAsync(vm, dir, ownerWindow);
				break;

			case "AddGroupToGroup" when ownerVm is GroupNodeViewModel grp:
				await AddGroupToGroupAsync(vm, grp, ownerWindow);
				break;

			case "AddSessionToGroup" when ownerVm is GroupNodeViewModel grp:
				await AddSessionToGroupAsync(vm, grp, ownerWindow);
				break;

			case "RenameGroup" when ownerVm is GroupNodeViewModel grp:
				await RenameGroupAsync(vm, grp, ownerWindow);
				break;

			case "RenameSession" when ownerVm is SessionNodeViewModel session:
				await RenameSessionAsync(vm, session, ownerWindow);
				break;

			case "DeleteDirectory" when ownerVm is DirectoryNodeViewModel dir:
				vm.TryDeleteDirectory(dir);
				break;

			case "DeleteGroup" when ownerVm is GroupNodeViewModel grp:
				DeleteGroupFromTree(vm, grp);
				break;

			case "DeleteSession" when ownerVm is SessionNodeViewModel session:
				DeleteSessionFromTree(vm, session);
				break;
		}
	}

	// ── Context menu helpers ─────────────────────────────────────────────────

	private static object? GetContextMenuOwnerDataContext(MenuItem mi)
	{
		// MenuItem → ContextMenu → StackPanel (PlacementTarget) → DataContext
		if (mi.Parent is ContextMenu cm)
			return cm.PlacementTarget?.DataContext ?? cm.DataContext;
		return null;
	}

	private static async Task AddGroupToDirectoryAsync(
		SessionTreeViewModel vm, DirectoryNodeViewModel dir, Window? owner)
	{
		if (owner == null) return;
		var name = await InputDialog.ShowAsync(owner, "New Group", "Group name:");
		if (!string.IsNullOrEmpty(name))
			vm.AddGroup(dir, name);
	}

	private static async Task AddSessionToDirectoryAsync(
		SessionTreeViewModel vm, DirectoryNodeViewModel dir, Window? owner)
	{
		if (owner == null) return;
		var name = await InputDialog.ShowAsync(owner, "New Session", "Session name:");
		if (!string.IsNullOrEmpty(name))
			vm.AddSession(dir, name);
	}

	private static async Task AddGroupToGroupAsync(
		SessionTreeViewModel vm, GroupNodeViewModel grp, Window? owner)
	{
		if (owner == null) return;
		var name = await InputDialog.ShowAsync(owner, "New Group", "Group name:");
		if (!string.IsNullOrEmpty(name))
			vm.AddGroupToGroup(grp, name);
	}

	private static async Task AddSessionToGroupAsync(
		SessionTreeViewModel vm, GroupNodeViewModel grp, Window? owner)
	{
		if (owner == null) return;
		var name = await InputDialog.ShowAsync(owner, "New Session", "Session name:");
		if (!string.IsNullOrEmpty(name))
			vm.AddSessionToGroup(grp, name);
	}

	private static async Task RenameGroupAsync(
		SessionTreeViewModel vm, GroupNodeViewModel grp, Window? owner)
	{
		if (owner == null) return;
		var name = await InputDialog.ShowAsync(owner, "Rename Group", "New name:");
		if (!string.IsNullOrEmpty(name))
			vm.RenameGroup(grp, name);
	}

	private static async Task RenameSessionAsync(
		SessionTreeViewModel vm, SessionNodeViewModel session, Window? owner)
	{
		if (owner == null) return;
		var name = await InputDialog.ShowAsync(owner, "Rename Session", "New name:");
		if (!string.IsNullOrEmpty(name))
			vm.RenameSession(session, name);
	}

	private void DeleteGroupFromTree(SessionTreeViewModel vm, GroupNodeViewModel grp)
	{
		// Find the parent and delete from it
		foreach (var dir in vm.Directories)
		{
			if (TryDeleteGroupFromParent(vm, dir, grp))
				return;
		}
	}

	private static bool TryDeleteGroupFromParent(
		SessionTreeViewModel vm, DirectoryNodeViewModel dir, GroupNodeViewModel target)
	{
		foreach (var child in dir.Children)
		{
			if (child == target)
				return vm.TryDeleteGroup(dir, target);

			if (child is GroupNodeViewModel grp && TryDeleteGroupFromGroup(vm, grp, target))
				return true;
		}
		return false;
	}

	private static bool TryDeleteGroupFromGroup(
		SessionTreeViewModel vm, GroupNodeViewModel parent, GroupNodeViewModel target)
	{
		foreach (var child in parent.Children)
		{
			if (child == target)
				return vm.TryDeleteGroupFromGroup(parent, target);

			if (child is GroupNodeViewModel grp && TryDeleteGroupFromGroup(vm, grp, target))
				return true;
		}
		return false;
	}

	private void DeleteSessionFromTree(SessionTreeViewModel vm, SessionNodeViewModel session)
	{
		foreach (var dir in vm.Directories)
		{
			if (TryDeleteSessionFromParent(vm, dir, session))
				return;
		}
	}

	private static bool TryDeleteSessionFromParent(
		SessionTreeViewModel vm, DirectoryNodeViewModel dir, SessionNodeViewModel target)
	{
		foreach (var child in dir.Children)
		{
			if (child == target)
				return vm.TryDeleteSession(dir, target);

			if (child is GroupNodeViewModel grp && TryDeleteSessionFromGroup(vm, grp, target))
				return true;
		}
		return false;
	}

	private static bool TryDeleteSessionFromGroup(
		SessionTreeViewModel vm, GroupNodeViewModel parent, SessionNodeViewModel target)
	{
		foreach (var child in parent.Children)
		{
			if (child == target)
				return vm.TryDeleteSessionFromGroup(parent, target);

			if (child is GroupNodeViewModel grp && TryDeleteSessionFromGroup(vm, grp, target))
				return true;
		}
		return false;
	}
}
