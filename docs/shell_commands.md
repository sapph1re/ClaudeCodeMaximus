# Shell Command Notes

## dotnet — chain commands
`&&` does NOT work in Windows cmd, but works fine in bash (which Claude Code uses here). Use `&&` normally.

## dotnet build — app is always running (exe and dll locked)

ClaudeMaximus is always running when Claude Code is active (the session IS the app). The app does **not** run from `bin\Debug\net9.0\` — it runs from an arbitrary directory (typically `/publish`) where updated binaries are copied by the self-update mechanism on exit (see FR.8 in `requirements.md`). The standard build output is `code/ClaudeMaximus/bin/Debug/net9.0/`. Both the running directory and `bin\Debug\net9.0\` have locked DLLs during development, so a normal build always fails. Do NOT use the `Tempcmx-build` folder — it is deprecated and non-functional.

**Fix:** always redirect output to a temp directory:
```
dotnet build code/ClaudeMaximus.sln -o "C:\Temp\ClaudeMaximus-build-check"
```
This writes compiled output to a fresh directory that is never locked. Do NOT use `dotnet build` without `-o` on this project.

## Avalonia — compiled bindings in DataTemplates
`x:DataType` on a `DataTemplate` or `TreeDataTemplate` inside a control's `DataTemplates` collection does NOT correctly scope compiled bindings — Avalonia resolves `{Binding X}` against the outer control's `x:DataType` instead of the template's type, returning null silently.

**Fix:** add `x:CompileBindings="False"` to each such template. This overrides the project-level `AvaloniaUseCompiledBindingsByDefault=true` for that scope and uses runtime reflection binding, which correctly uses the item's actual runtime type.

## git add — CRLF files with core.autocrlf=input

When `core.autocrlf=input` is set, `git add` refuses to stage files with CRLF line endings (error: "CRLF would be replaced by LF"). New files written by the Write tool on Windows have CRLF.

**Fix:** use `git -c core.safecrlf=false add <file>` to stage without the safety check.

**DO NOT** try to convert line endings with PowerShell via bash — backtick escaping in `-replace` patterns gets mangled by bash, corrupting file content.
