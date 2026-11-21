using System.Collections.ObjectModel;
using Aster.Core.Lyrics;
using Aster.UI.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aster.UI.ViewModels;

public partial class LyricsViewModel : ObservableObject, IDisposable
{
    private static readonly IBrush ActiveBrush = new SolidColorBrush(Color.Parse("#F5F5F7"));
    private static readonly IBrush InactiveBrush = new SolidColorBrush(Color.Parse("#8C8C96"));
    private static readonly IBrush PlainBrush = new SolidColorBrush(Color.Parse("#A8A8B0"));
    private static readonly IBrush AccentBrush = new SolidColorBrush(Color.Parse("#8FA8FF"));
    private static readonly IBrush MutedBrush = new SolidColorBrush(Color.Parse("#5E5E66"));

    private readonly LyricsService _lyrics;
    private readonly MediaViewModel _media;
    private readonly SettingsViewModel _settings;

    [ObservableProperty] private int _activeIndex = -1;
    [ObservableProperty] private string _statusText = "LRCLIB";
    [ObservableProperty] private IBrush _statusBrush = MutedBrush;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hasError;

    public ObservableCollection<LyricsLineViewModel> Lines { get; } = new();
    public IRelayCommand<double> SeekToLineCommand { get; }
    public IRelayCommand RefreshCommand { get; }

    public LyricsViewModel(
        LyricsService lyrics, MediaViewModel media, SettingsViewModel settings)
    {
        _lyrics = lyrics;
        _media = media;
        _settings = settings;
        SeekToLineCommand = new RelayCommand<double>(SeekToLine);
        RefreshCommand = new RelayCommand(() => _ = _lyrics.RefreshAsync());
        _lyrics.Changed += OnLyricsChanged;
        _media.PropertyChanged += OnMediaPropertyChanged;
        _settings.PropertyChanged += OnSettingsPropertyChanged;
        OnLyricsChanged(this, EventArgs.Empty);
    }

    public bool HasMedia => _media.HasSession;
    public bool CanRefresh => _media.HasSession && !_lyrics.IsLoading && _settings.LyricsEnabled;
    public bool HasLines => Lines.Count > 0;
    public bool ShowMessage => !HasLines;
    public string MessageText =>
        !_media.HasSession
            ? "Play something to see lyrics"
            : _lyrics.IsLoading
                ? "Searching lyrics..."
                : _lyrics.Current?.IsInstrumental == true
                    ? "Instrumental track"
                    : _lyrics.StatusMessage ?? "No lyrics found for this track";
    public string RefreshGlyph => Glyph.Get(Glyph.Kind.Refresh);

    private void OnLyricsChanged(object? sender, EventArgs e)
    {
        IsLoading = _lyrics.IsLoading;
        HasError = _lyrics.HasError;
        OnPropertyChanged(nameof(CanRefresh));
        StatusText = _lyrics.IsLoading
            ? "SEARCHING"
            : _lyrics.Current switch
            {
                { IsInstrumental: true } => "INSTRUMENTAL",
                { IsSynced: true } => "SYNCED",
                { PlainLyrics: not null } => "PLAIN",
                _ => "LRCLIB"
            };
        StatusBrush = _lyrics.IsLoading || _lyrics.Current?.IsSynced == true
            ? AccentBrush
            : MutedBrush;
        Rebuild();
        OnPropertyChanged(nameof(MessageText));
    }

    private void Rebuild()
    {
        Lines.Clear();
        var document = _lyrics.Current;
        if (document is not null)
        {
            if (document.IsSynced)
            {
                foreach (var line in document.Lines)
                {
                    Lines.Add(new LyricsLineViewModel(line.Text, line.TimeSeconds, true));
                }
            }
            else if (!string.IsNullOrWhiteSpace(document.PlainLyrics))
            {
                foreach (var text in document.PlainLyrics
                             .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    Lines.Add(new LyricsLineViewModel(text.Trim(), -1, false));
                }
            }
        }

        UpdateActiveIndex();
        UpdateLineStyles();
        OnPropertyChanged(nameof(HasLines));
        OnPropertyChanged(nameof(ShowMessage));
        OnPropertyChanged(nameof(MessageText));
    }

    private void OnMediaPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MediaViewModel.PositionSeconds))
        {
            UpdateActiveIndex();
        }

        if (e.PropertyName == nameof(MediaViewModel.HasSession))
        {
            OnPropertyChanged(nameof(HasMedia));
            OnPropertyChanged(nameof(CanRefresh));
        }
    }

    private void UpdateActiveIndex()
    {
        if (_lyrics.Current?.IsSynced != true || Lines.Count == 0)
        {
            ActiveIndex = -1;
            return;
        }

        var index = -1;
        for (var i = 0; i < Lines.Count; i++)
        {
            if (Lines[i].TimeSeconds <= _media.PositionSeconds)
            {
                index = i;
            }
            else
            {
                break;
            }
        }

        ActiveIndex = index;
    }

    partial void OnActiveIndexChanged(int value) => UpdateLineStyles();

    private void UpdateLineStyles()
    {
        var synced = _lyrics.Current?.IsSynced == true;
        for (var i = 0; i < Lines.Count; i++)
        {
            var active = i == ActiveIndex;
            var distance = ActiveIndex < 0 ? 0 : Math.Abs(i - ActiveIndex);
            var line = Lines[i];
            line.Foreground = active ? ActiveBrush : synced ? InactiveBrush : PlainBrush;
            line.TextOpacity = active
                ? 1
                : ActiveIndex < 0
                    ? 0.7
                    : distance switch { 1 => 0.8, 2 => 0.56, _ => 0.36 };
        }
    }

    private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.LyricsEnabled))
        {
            OnPropertyChanged(nameof(CanRefresh));
        }
    }

    private void SeekToLine(double seconds)
    {
        if (_media.CanSeek && seconds >= 0)
        {
            _media.SeekCommand.Execute(seconds);
        }
    }

    public void Dispose()
    {
        _lyrics.Changed -= OnLyricsChanged;
        _media.PropertyChanged -= OnMediaPropertyChanged;
        _settings.PropertyChanged -= OnSettingsPropertyChanged;
    }
}

public sealed partial class LyricsLineViewModel : ObservableObject
{
    public LyricsLineViewModel(string text, double timeSeconds, bool isSynced)
    {
        Text = text;
        TimeSeconds = timeSeconds;
        IsSynced = isSynced;
    }

    public string Text { get; }
    public double TimeSeconds { get; }
    public bool IsSynced { get; }
    [ObservableProperty] private IBrush _foreground = new SolidColorBrush(Color.Parse("#8C8C96"));
    [ObservableProperty] private double _textOpacity = 0.7;
}
