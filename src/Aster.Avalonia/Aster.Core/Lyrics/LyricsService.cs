using Aster.Core.Interfaces;
using Aster.Core.Models;

namespace Aster.Core.Lyrics;

public sealed class LyricsService : IDisposable
{
    private readonly ISettingsStore _settings;
    private readonly ILyricsProvider _provider;
    private readonly LyricsCache _cache;
    private readonly IUiDispatcher _dispatcher;
    private readonly IAppLog _log;
    private CancellationTokenSource? _cts;
    private int _generation;
    private string _currentKey = string.Empty;
    private MediaSnapshot? _lastSnapshot;
    private bool _lastEnabled;
    private bool _disposed;

    public LyricsService(
        ISettingsStore settings,
        ILyricsProvider provider,
        LyricsCache cache,
        IUiDispatcher dispatcher,
        IAppLog log)
    {
        _settings = settings;
        _provider = provider;
        _cache = cache;
        _dispatcher = dispatcher;
        _log = log;
        _lastEnabled = settings.Current.LyricsEnabled;
        settings.Changed += OnSettingsChanged;
    }

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        if (settings.LyricsEnabled == _lastEnabled)
        {
            return;
        }

        _lastEnabled = settings.LyricsEnabled;
        _currentKey = string.Empty;

        if (!_lastEnabled)
        {
            CancelPending();
            SetState(null, false, "Lyrics disabled in Settings", false);
            return;
        }

        if (_lastSnapshot is not null)
        {
            _ = LoadAsync(_lastSnapshot, force: true);
        }
    }

    public LyricsDocument? Current { get; private set; }
    public bool IsLoading { get; private set; }
    public string? StatusMessage { get; private set; }
    public bool HasError { get; private set; }
    public string CacheDirectory => _cache.DirectoryPath;
    public event EventHandler? Changed;

    public async Task LoadAsync(MediaSnapshot? snapshot, bool force = false)
    {
        if (_disposed)
        {
            return;
        }

        _lastSnapshot = snapshot;

        if (!_settings.Current.LyricsEnabled)
        {
            CancelPending();
            SetState(null, false, "Lyrics disabled in Settings", false);
            return;
        }

        if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.Title))
        {
            CancelPending();
            SetState(null, false, null, false);
            return;
        }

        var key = LyricsCache.KeyFor(snapshot);
        if (!force && string.Equals(key, _currentKey, StringComparison.Ordinal))
        {
            return;
        }

        CancelPending();
        var generation = ++_generation;
        var cts = new CancellationTokenSource();
        _cts = cts;
        _currentKey = key;
        SetState(null, true, null, false);

        try
        {
            if (!force)
            {
                var cached = await _cache.ReadAsync(key, cts.Token);
                if (IsStale(generation, cts))
                {
                    return;
                }

                if (cached is not null)
                {
                    SetState(cached, false, null, false);
                    return;
                }
            }

            var artist = snapshot.Artist.Equals("Unknown artist", StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : snapshot.Artist;
            var document = await _provider.GetAsync(
                new LyricsQuery(snapshot.Title, artist, snapshot.Album,
                    (int)Math.Round(snapshot.Duration.TotalSeconds)),
                cts.Token);
            if (IsStale(generation, cts))
            {
                return;
            }

            if (document is null)
            {
                SetState(null, false, "No lyrics found for this track", false);
                return;
            }

            await _cache.WriteAsync(key, document, cts.Token);
            if (IsStale(generation, cts))
            {
                return;
            }

            SetState(document, false, null, false);
        }
        catch (OperationCanceledException)
        {
            if (!IsStale(generation, cts))
            {
                SetState(null, false, "Couldn't reach LRCLIB", true);
            }
        }
        catch (Exception ex)
        {
            _log.Warn($"Lyrics load failed: {ex.Message}");
            if (!IsStale(generation, cts))
            {
                SetState(null, false, "Couldn't reach LRCLIB", true);
            }
        }
        finally
        {
            if (ReferenceEquals(_cts, cts))
            {
                _cts = null;
            }
            cts.Dispose();
        }
    }

    public Task RefreshAsync() => LoadAsync(_lastSnapshot, force: true);

    public void ClearCache() => _cache.Clear();

    private bool IsStale(int generation, CancellationTokenSource cts) =>
        _disposed || generation != _generation || cts.IsCancellationRequested;

    private void CancelPending()
    {
        _generation++;
        var cts = _cts;
        _cts = null;
        if (cts is null)
        {
            return;
        }

        try
        {
            cts.Cancel();
        }
        catch
        {
            // Ignore.
        }
    }

    private void SetState(
        LyricsDocument? document, bool loading, string? message, bool error)
    {
        Current = document;
        IsLoading = loading;
        StatusMessage = message;
        HasError = error;
        _dispatcher.Post(() => Changed?.Invoke(this, EventArgs.Empty));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _settings.Changed -= OnSettingsChanged;
        CancelPending();
        if (_provider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
