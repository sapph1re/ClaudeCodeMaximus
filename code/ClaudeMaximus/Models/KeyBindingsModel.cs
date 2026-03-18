using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ClaudeMaximus.Models;

/// <summary>
/// Stores action name to key binding mappings. Each value is a key combo string
/// (e.g., "Ctrl+I" or "Cmd+I"). Multiple bindings per action are separated by comma.
/// </summary>
/// <remarks>Created by Claude</remarks>
public sealed class KeyBindingsModel
{
	/// <summary>
	/// Action name -> key combo string (e.g., "Cmd+I", "Ctrl+W, Escape").
	/// </summary>
	public Dictionary<string, string> Bindings { get; set; } = [];

	/// <summary>
	/// Creates a KeyBindingsModel populated with platform-appropriate defaults.
	/// </summary>
	public static KeyBindingsModel CreateDefaults()
	{
		var model = new KeyBindingsModel();
		var isMac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

		if (isMac)
		{
			model.Bindings[Constants.KeyBindings.ImportSessions] = "Cmd+I";
			model.Bindings[Constants.KeyBindings.CloseDialog] = "Escape, Cmd+W";
		}
		else
		{
			model.Bindings[Constants.KeyBindings.ImportSessions] = "Ctrl+I";
			model.Bindings[Constants.KeyBindings.CloseDialog] = "Escape, Ctrl+W";
		}

		return model;
	}

	/// <summary>
	/// Ensures all known actions have bindings, filling in defaults for any missing ones.
	/// </summary>
	public void EnsureDefaults()
	{
		var defaults = CreateDefaults();
		foreach (var (action, binding) in defaults.Bindings)
		{
			if (!Bindings.ContainsKey(action))
				Bindings[action] = binding;
		}
	}
}
