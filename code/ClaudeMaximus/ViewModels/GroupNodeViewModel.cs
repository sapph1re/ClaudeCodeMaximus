using System.Collections.ObjectModel;
using ClaudeMaximus.Models;
using ReactiveUI;

namespace ClaudeMaximus.ViewModels;

/// <remarks>Created by Claude</remarks>
public sealed class GroupNodeViewModel : ViewModelBase
{
	private string _name;

	public GroupNodeModel Model { get; }

	public string WorkingDirectory => Model.WorkingDirectory;

	public string Name
	{
		get => _name;
		set
		{
			this.RaiseAndSetIfChanged(ref _name, value);
			Model.Name = value;
		}
	}

	/// <summary>
	/// Combined children collection for TreeView binding.
	/// Contains GroupNodeViewModel and SessionNodeViewModel in display order (groups first).
	/// </summary>
	public ObservableCollection<ViewModelBase> Children { get; } = [];

	public GroupNodeViewModel(GroupNodeModel model)
	{
		Model = model;
		_name = model.Name;

		foreach (var g in model.Groups)
			Children.Add(new GroupNodeViewModel(g));

		foreach (var s in model.Sessions)
			Children.Add(new SessionNodeViewModel(s));
	}

	public void AddGroup(GroupNodeViewModel group)
	{
		Children.Add(group);
		Model.Groups.Add(group.Model);
	}

	public void AddSession(SessionNodeViewModel session)
	{
		Children.Add(session);
		Model.Sessions.Add(session.Model);
	}

	public bool CanDelete => Children.Count == 0;
}
