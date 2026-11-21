using Aster.Core.Interfaces;
using Aster.Core.Models;

namespace Aster.UI.Tests;

internal sealed class TestSettingsStore : ISettingsStore
{
    public TestSettingsStore(AppSettings? current = null) =>
        Current = current ?? SettingsSchema.Default;

    public AppSettings Current { get; private set; }
    public bool CanSave { get; set; } = true;
    public bool FailUpdates { get; set; }
    public string? LoadWarning => null;
    public int SaveCount { get; private set; }
    public event EventHandler<AppSettings>? Changed;

    public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        SaveCount++;
        Current = settings;
        Changed?.Invoke(this, settings);
        return Task.CompletedTask;
    }

    public Task<SettingsUpdateResult> UpdateAsync(
        Func<AppSettings, AppSettings> update,
        CancellationToken ct = default)
    {
        if (!CanSave || FailUpdates)
        {
            return Task.FromResult(new SettingsUpdateResult(false, Current));
        }

        SaveCount++;
        Current = update(Current) with { SchemaVersion = SettingsSchema.CurrentVersion };
        Changed?.Invoke(this, Current);
        return Task.FromResult(new SettingsUpdateResult(true, Current));
    }

    public void Raise(AppSettings settings)
    {
        Current = settings;
        Changed?.Invoke(this, settings);
    }
}
