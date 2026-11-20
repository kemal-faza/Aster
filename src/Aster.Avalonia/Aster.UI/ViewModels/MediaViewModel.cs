using System.Globalization;
using Aster.Core.Interfaces;
using Aster.Core.Logic;
using Aster.Core.Models;
using Aster.UI.Media;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aster.UI.ViewModels;

public partial class MediaViewModel : ObservableObject, IDisposable
{
    private readonly IMediaSession _media;
    private readonly IUiDispatcher _dispatcher;
    private readonly IAppLog _log;
    private readonly DispatcherTimer _playbackTimer =
        new() { Interval = TimeSpan.FromMilliseconds(33) };
    private DateTimeOffset _lastVinylTick;
    private int _lastPositionSecond = -1;
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

    public IBrush AccentGlowBrush => new RadialGradientBrush
    {
        GradientOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
        Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
        RadiusX = new RelativeScalar(0.5, RelativeUnit.Relative),
        RadiusY = new RelativeScalar(0.5, RelativeUnit.Relative),
        GradientStops = new GradientStops
        {
            new(Color.FromArgb(76, AccentColor.R, AccentColor.G, AccentColor.B), 0),
            new(Color.FromArgb(0, AccentColor.R, AccentColor.G, AccentColor.B), 1)
        }
    };

    public IBrush LyricsGlowBrush => CreateGlowBrush(AccentColor);

    private static IBrush CreateGlowBrush(Color color) =>
        new RadialGradientBrush
        {
            GradientOrigin = new RelativePoint(0.82, 0.04, RelativeUnit.Relative),
            Center = new RelativePoint(0.82, 0.04, RelativeUnit.Relative),
            RadiusX = new RelativeScalar(0.9, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(1.0, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new(Color.FromArgb(36, color.R, color.G, color.B), 0),
                new(Color.FromArgb(12, color.R, color.G, color.B), 0.55),
                new(Color.FromArgb(0, color.R, color.G, color.B), 1)
            }
        };

    public MediaViewModel(IMediaSession media, IUiDispatcher dispatcher, IAppLog log)
    {
        _media = media;
        _dispatcher = dispatcher;
        _log = log;

        _media.Changed += OnMediaChanged;
        _playbackTimer.Tick += (_, _) => OnPlaybackTick();
        Apply(_media.Current);
    }

    public void Dispose()
    {
        _playbackTimer.Stop();
        _media.Changed -= OnMediaChanged;
    }

    [RelayCommand]
    private Task PlayPauseAsync() => _media.PlayPauseAsync();

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private Task NextAsync() => _media.NextAsync();

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private Task PreviousAsync() => _media.PreviousAsync();

    [RelayCommand]
    private async Task SeekAsync(double seconds)
    {
        if (DurationSeconds <= 0)
        {
            return;
        }

        var target = Math.Clamp(seconds, 0, DurationSeconds);
        var position = TimeSpan.FromSeconds(target);
        PositionSeconds = target;
        PositionText = Format(position);
        Progress = target / DurationSeconds;
        IsSeeking = true;

        try
        {
            await _media.SeekAsync(position);
            var deadline = DateTimeOffset.UtcNow + TimeSpan.FromMilliseconds(500);
            while (DateTimeOffset.UtcNow < deadline &&
                   Math.Abs(_media.Current.Position.TotalSeconds - target) > 1.0)
            {
                await Task.Delay(25);
            }
        }
        finally
        {
            IsSeeking = false;
        }
    }

    partial void OnCanGoNextChanged(bool value) => NextCommand.NotifyCanExecuteChanged();
    partial void OnCanGoPreviousChanged(bool value) => PreviousCommand.NotifyCanExecuteChanged();
    partial void OnIsPlayingChanged(bool value) => OnPropertyChanged(nameof(PlayingDotOpacity));
    partial void OnAccentColorChanged(Color value)
    {
        OnPropertyChanged(nameof(AccentGlowBrush));
        OnPropertyChanged(nameof(LyricsGlowBrush));
    }

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
        _lastPositionSecond = -1;
        UpdateProgress(snapshot);
        UpdatePlaybackTimer();

        if (!string.Equals(snapshot.ArtworkKey, _artKey, StringComparison.Ordinal))
        {
            _artKey = snapshot.ArtworkKey;
            _ = DecodeAndApplyAsync(snapshot.ArtworkBytes, snapshot.ArtworkKey);
        }

        if (IsPlaying)
        {
            _playbackTimer.Start();
        }
        else
        {
            _playbackTimer.Stop();
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

    private void UpdatePlaybackTimer()
    {
        if (!HasSession)
        {
            _playbackTimer.Stop();
            VinylAngle = 0;
            return;
        }

        if (!IsPlaying)
        {
            _playbackTimer.Stop();
            return;
        }

        if (!_playbackTimer.IsEnabled)
        {
            _lastVinylTick = DateTimeOffset.UtcNow;
            _playbackTimer.Start();
        }
    }

    private void OnPlaybackTick()
    {
        var now = DateTimeOffset.UtcNow;
        var elapsed = (now - _lastVinylTick).TotalSeconds;
        _lastVinylTick = now;
        VinylAngle = (VinylAngle + elapsed * (360.0 / 11.0)) % 360;
        UpdateProgress(_media.Current);
    }

    private void UpdateProgress(MediaSnapshot snapshot)
    {
        if (IsSeeking)
        {
            return;
        }

        var position = _media.EstimatedPosition(DateTimeOffset.Now);
        var tick = ComputePlaybackTick(position, snapshot.Duration, _lastPositionSecond);
        PositionSeconds = tick.PositionSeconds;
        Progress = tick.Progress;

        if (tick.PositionText is not null)
        {
            PositionText = tick.PositionText;
            _lastPositionSecond = tick.WholeSecond;
        }
    }

    internal static PlaybackTick ComputePlaybackTick(
        TimeSpan position,
        TimeSpan duration,
        int lastSecond)
    {
        var seconds = Math.Max(0, position.TotalSeconds);
        var progress = duration.TotalSeconds <= 0
            ? 0
            : Math.Clamp(seconds / duration.TotalSeconds, 0, 1);
        var whole = (int)Math.Floor(seconds);
        var text = whole == lastSecond ? null : Format(position);
        return new PlaybackTick(seconds, progress, text, whole);
    }

    internal readonly record struct PlaybackTick(
        double PositionSeconds,
        double Progress,
        string? PositionText,
        int WholeSecond);

    private async Task DecodeAndApplyAsync(byte[]? bytes, string key)
    {
        var bitmap = await Task.Run(() => Decode(bytes));
        var accent = await Task.Run(() => ArtworkAccent.Extract(bytes));
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
            AccentColor = accent;
            AccentBrush = new SolidColorBrush(accent);
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
