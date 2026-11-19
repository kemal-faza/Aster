using System.Globalization;
using Aster.Core.Interfaces;
using Aster.Core.Logic;
using Aster.Core.Models;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aster.UI.ViewModels;

public partial class MediaViewModel : ObservableObject
{
    private readonly IMediaSession _media;
    private readonly IUiDispatcher _dispatcher;
    private readonly IAppLog _log;
    private readonly DispatcherTimer _progressTimer;
    private readonly DispatcherTimer _vinylTimer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private DateTimeOffset _lastVinylTick;
    private string _artKey = string.Empty;

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _artist = string.Empty;
    [ObservableProperty] private string _sourceApp = string.Empty;
    [ObservableProperty] private bool _hasSession;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private double _positionSeconds;
    [ObservableProperty] private double _durationSeconds;
    [ObservableProperty] private string _positionText = "0:00";
    [ObservableProperty] private string _durationText = "0:00";
    [ObservableProperty] private Bitmap? _artworkImage;
    [ObservableProperty] private string _trackKey = string.Empty;
    [ObservableProperty] private bool _canSeek;
    [ObservableProperty] private bool _canGoNext;
    [ObservableProperty] private bool _canGoPrevious;
    [ObservableProperty] private int _waveformSeed;
    [ObservableProperty] private bool _hasArtwork;
    [ObservableProperty] private Color _accentColor = Color.Parse("#F6A6C8");
    [ObservableProperty] private IBrush _accentBrush = new SolidColorBrush(Color.Parse("#F6A6C8"));
    [ObservableProperty] private string _rawArtist = string.Empty;
    [ObservableProperty] private string _displayTitle = "Nothing playing";
    [ObservableProperty] private string _displayArtist = "Start something on Spotify, YouTube, or Apple Music";
    [ObservableProperty] private double _vinylAngle;

    public bool IsSeeking { get; set; }

    public double PlayingDotOpacity => IsPlaying ? 1 : 0;

    public MediaViewModel(IMediaSession media, IUiDispatcher dispatcher, IAppLog log)
    {
        _media = media;
        _dispatcher = dispatcher;
        _log = log;

        _media.Changed += OnMediaChanged;
        _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _progressTimer.Tick += (_, _) => UpdateProgress(_media.Current);
        _vinylTimer.Tick += (_, _) => AdvanceVinyl();
        Apply(_media.Current);
    }

    [RelayCommand]
    private Task PlayPauseAsync() => _media.PlayPauseAsync();

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private Task NextAsync() => _media.NextAsync();

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private Task PreviousAsync() => _media.PreviousAsync();

    [RelayCommand]
    private Task SeekAsync(double seconds) => _media.SeekAsync(TimeSpan.FromSeconds(seconds));

    partial void OnCanGoNextChanged(bool value) => NextCommand.NotifyCanExecuteChanged();
    partial void OnCanGoPreviousChanged(bool value) => PreviousCommand.NotifyCanExecuteChanged();
    partial void OnIsPlayingChanged(bool value) => OnPropertyChanged(nameof(PlayingDotOpacity));

    private void OnMediaChanged(object? sender, MediaSnapshot snapshot) =>
        _dispatcher.Post(() => Apply(snapshot));

    private void Apply(MediaSnapshot snapshot)
    {
        HasSession = snapshot.HasSession;
        IsPlaying = snapshot.Playback == PlaybackState.Playing;
        Title = snapshot.Title;
        RawArtist = snapshot.Artist;
        Artist = ResolveArtist(snapshot);
        SourceApp = snapshot.SourceApp;
        TrackKey = BuildTrackKey(snapshot);
        CanSeek = snapshot.CanSeek;
        CanGoNext = snapshot.CanGoNext;
        CanGoPrevious = snapshot.CanGoPrevious;
        WaveformSeed = snapshot.HasSession
            ? TrackHash.Of($"{snapshot.Title}|{snapshot.Artist}|{snapshot.Album}")
            : 0;
        DisplayTitle = snapshot.HasSession
            ? (string.IsNullOrWhiteSpace(snapshot.Title) ? "Unknown track" : snapshot.Title)
            : "Nothing playing";
        DisplayArtist = snapshot.HasSession
            ? ResolveArtist(snapshot)
            : "Start something on Spotify, YouTube, or Apple Music";
        DurationSeconds = Math.Max(0, snapshot.Duration.TotalSeconds);
        DurationText = Format(snapshot.Duration);
        UpdateProgress(snapshot);
        UpdateVinylTimer();

        if (!string.Equals(snapshot.ArtworkKey, _artKey, StringComparison.Ordinal))
        {
            _artKey = snapshot.ArtworkKey;
            _ = DecodeAndApplyAsync(snapshot.ArtworkBytes, snapshot.ArtworkKey);
        }

        if (IsPlaying)
        {
            _progressTimer.Start();
        }
        else
        {
            _progressTimer.Stop();
        }
    }

    private static string BuildTrackKey(MediaSnapshot snapshot) =>
        string.IsNullOrWhiteSpace(snapshot.TrackId)
            ? $"{snapshot.Title}|{snapshot.Artist}|{snapshot.Album}|{snapshot.SourceApp}"
            : snapshot.TrackId;

    private static string ResolveArtist(MediaSnapshot snapshot) =>
        !string.IsNullOrWhiteSpace(snapshot.Artist)
            ? snapshot.Artist
            : !string.IsNullOrWhiteSpace(snapshot.Album)
                ? snapshot.Album
                : "Unknown artist";

    private void UpdateVinylTimer()
    {
        if (!HasSession)
        {
            _vinylTimer.Stop();
            VinylAngle = 0;
            return;
        }

        if (IsPlaying)
        {
            if (!_vinylTimer.IsEnabled)
            {
                _lastVinylTick = DateTimeOffset.UtcNow;
                _vinylTimer.Start();
            }
            return;
        }

        _vinylTimer.Stop();
    }

    private void AdvanceVinyl()
    {
        var now = DateTimeOffset.UtcNow;
        var elapsed = (now - _lastVinylTick).TotalSeconds;
        _lastVinylTick = now;
        VinylAngle = (VinylAngle + elapsed * (360.0 / 11.0)) % 360;
    }

    private void UpdateProgress(MediaSnapshot snapshot)
    {
        if (IsSeeking)
        {
            return;
        }

        var position = _media.EstimatedPosition(DateTimeOffset.Now);
        PositionSeconds = Math.Max(0, position.TotalSeconds);
        PositionText = Format(position);
        Progress = snapshot.Duration.TotalSeconds <= 0
            ? 0
            : Math.Clamp(position.TotalSeconds / snapshot.Duration.TotalSeconds, 0, 1);
    }

    private async Task DecodeAndApplyAsync(byte[]? bytes, string key)
    {
        var bitmap = await Task.Run(() => Decode(bytes));
        _dispatcher.Post(() =>
        {
            if (!string.Equals(_artKey, key, StringComparison.Ordinal))
            {
                bitmap?.Dispose();
                return;
            }

            ArtworkImage?.Dispose();
            ArtworkImage = bitmap;
            HasArtwork = bitmap is not null;
        });
    }

    private static string Format(TimeSpan value) =>
        value.TotalHours >= 1
            ? value.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : value.ToString(@"m\:ss", CultureInfo.InvariantCulture);

    private static Bitmap? Decode(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(bytes);
            return Bitmap.DecodeToWidth(stream, 256);
        }
        catch
        {
            return null;
        }
    }
}
