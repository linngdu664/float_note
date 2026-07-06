using System.IO;
using System.Text.Json;
using FloatNote.Models;

namespace FloatNote.Services;

public sealed class AppStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _dataDirectory;
    private readonly string _statePath;

    public AppStorage()
    {
        _dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FloatNote");
        _statePath = Path.Combine(_dataDirectory, "app-state.json");
    }

    public async Task<AppState> LoadAsync()
    {
        Directory.CreateDirectory(_dataDirectory);

        if (!File.Exists(_statePath))
        {
            return new AppState();
        }

        await using var stream = File.OpenRead(_statePath);
        var state = await JsonSerializer.DeserializeAsync<AppState>(stream, JsonOptions);
        return state ?? new AppState();
    }

    public async Task SaveAsync(AppState state)
    {
        Directory.CreateDirectory(_dataDirectory);

        var tempPath = _statePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, state, JsonOptions);
        }

        File.Move(tempPath, _statePath, overwrite: true);
    }
}
