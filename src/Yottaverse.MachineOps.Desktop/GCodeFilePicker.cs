using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace Yottaverse.MachineOps.Desktop;

public sealed record PickedGCodeFile(string Name, string Content);

public interface IGCodeFilePicker
{
    public Task<PickedGCodeFile?> PickAsync();
}

public sealed class GCodeFilePicker : IGCodeFilePicker
{
    private static readonly FilePickerFileType GCodeFileType = new("G-code programs")
    {
        Patterns = ["*.nc", "*.ngc", "*.gcode", "*.tap"],
        AppleUniformTypeIdentifiers = ["public.text"],
        MimeTypes = ["text/plain"],
    };

    public async Task<PickedGCodeFile?> PickAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime lifetime ||
            lifetime.MainWindow is not Window window)
        {
            return null;
        }

        IReadOnlyList<IStorageFile> files = await window.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Open a G-code program",
                AllowMultiple = false,
                FileTypeFilter = [GCodeFileType, FilePickerFileTypes.TextPlain],
            });

        if (files.Count == 0)
        {
            return null;
        }

        IStorageFile file = files[0];
        await using Stream stream = await file.OpenReadAsync();
        using StreamReader reader = new(stream);
        string content = await reader.ReadToEndAsync();
        return new PickedGCodeFile(file.Name, content);
    }
}
