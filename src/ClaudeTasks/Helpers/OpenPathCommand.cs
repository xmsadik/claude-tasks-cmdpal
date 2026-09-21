using System;
using System.Diagnostics;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace ClaudeTasks;

/// <summary>
/// Opens a file or folder with the OS shell's default handler. The path comes from a
/// <see cref="Func{T}"/> resolved at invoke time (not captured up front), so the command
/// always acts on whatever the store currently considers "active" rather than a value frozen
/// when the owning page/item was built.
/// </summary>
internal sealed partial class OpenPathCommand : InvokableCommand
{
    private readonly Func<string?> _pathProvider;

    public OpenPathCommand(string name, string icon, Func<string?> pathProvider)
    {
        Name = name;
        Icon = new IconInfo(icon);
        _pathProvider = pathProvider;
    }

    public override CommandResult Invoke()
    {
        var path = _pathProvider();
        if (!string.IsNullOrEmpty(path))
        {
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or System.IO.FileNotFoundException)
            {
                // Nothing sensible to surface from a Dismiss()-only command; best effort.
            }
        }

        return CommandResult.Dismiss();
    }
}
