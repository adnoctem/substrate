using System;

namespace PSFoundation.PowerShell.Diagnostics;

/// <summary>Console palette data for the PowerShell presentation layer; does not write to a host.</summary>
public static class ColorCompatibility
{
    public static ConsoleColor[] GetColors() => (ConsoleColor[])Enum.GetValues(typeof(ConsoleColor));
}
