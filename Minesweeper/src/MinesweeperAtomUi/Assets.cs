using System;
using System.IO;
using System.Reflection;
using Avalonia.Platform;

public static class Assets
{
    /// <summary>Читает встроенный PNG-ассет Avalonia (avares://).</summary>
    public static byte[] GetPng(string name)
    {
        using var stream = AssetLoader.Open(
            new Uri($"avares://MinesweeperAtomUi/Resources/{name}.png"));
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
