using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenNotes.Core.Abstractions;
using OpenNotes.Core.Markdown;
using System.Globalization;
using OpenNotes.Core.Models;
using OpenNotes.Core.Reminders;

namespace OpenNotes.UI.ViewModels;

/// <summary>
/// The note being edited. Shared by the main window and the mini window, so both always show the same state.
/// Changes are auto-saved after a short pause; the preview is re-rendered with a small debounce.
/// </summary>
public sealed partial class NoteEditorViewModel : ObservableObject, IAssetSink, IAssetSource
{
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan RenderDelay = TimeSpan.FromMilliseconds(140);

    private readonly INoteRepository _repository;
    private readonly IMarkdownRenderer _renderer;
    private readonly IPreviewAssetCache _assetCache;
    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _renderTimer;

    private NoteItemViewModel? _item;
    private bool _isLoading;
    private bool _isDirty;

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _content = string.Empty;
    [ObservableProperty] private string _previewHtml = string.Empty;
    [ObservableProperty] private string _saveStatus = string.Empty;
    [ObservableProperty] private int _wordCount;
    [ObservableProperty] private int _characterCount;

    public NoteEditorViewModel(INoteRepository repository, IMarkdownRenderer renderer, IPreviewAssetCache assetCache)
    {
        _repository = repository;
        _renderer = renderer;
        _assetCache = assetCache;

        _saveTimer = new DispatcherTimer { Interval = SaveDelay };
        _saveTimer.Tick += (_, _) => Flush();

        _renderTimer = new DispatcherTimer { Interval = RenderDelay };
        _renderTimer.Tick += (_, _) => RenderPreview();
    }

    /// <summary>Raised when saving fails (e.g. disk full, folder not writable).</summary>
    public event EventHandler<Exception>? SaveFailed;

    /// <summary>Priority, due date, reminder or done state of the current note changed.</summary>
    public event EventHandler? TaskStateChanged;

    public NoteItemViewModel? Item => _item;
    public bool HasNote => _item is not null;
    public Guid? NoteId => _item?.Id;
    public bool IsPinned => _item?.IsPinned ?? false;
    public bool CanAddAssets => _item is not null;

    public string MetaText => _item is null
        ? string.Empty
        : $"Létrehozva {NoteItemViewModel.FormatRelative(_item.Model.CreatedAt)}  ·  Módosítva {NoteItemViewModel.FormatRelative(_item.Model.UpdatedAt)}";

    public void Load(NoteItemViewModel? item)
    {
        if (ReferenceEquals(item, _item)) return;
        Flush();

        _item = item;
        _isLoading = true;
        try
        {
            Title = item?.Model.Title ?? string.Empty;
            Content = item?.Model.Content ?? string.Empty;
        }
        finally
        {
            _isLoading = false;
        }

        SaveStatus = item is null ? string.Empty : "Mentve";
        OnPropertyChanged(nameof(Item));
        OnPropertyChanged(nameof(HasNote));
        OnPropertyChanged(nameof(NoteId));
        OnPropertyChanged(nameof(IsPinned));
        OnPropertyChanged(nameof(MetaText));
        RaiseTaskProperties();
        RenderPreview();
    }

    // ───────────────────────── task properties (priority, due date, reminder) ─────────────────────────

    public NotePriority Priority
    {
        get => _item?.Model.Priority ?? NotePriority.None;
        set => UpdateTask(n => n.Priority == value, n => n.Priority = value);
    }

    /// <summary>Due day for the calendar (DateTime with time 00:00).</summary>
    public DateTime? DueDateValue
    {
        get => _item?.Model.DueDate?.ToDateTime(TimeOnly.MinValue);
        set
        {
            var date = value is { } d ? DateOnly.FromDateTime(d) : (DateOnly?)null;
            UpdateTask(n => n.DueDate == date, n =>
            {
                // Setting the first deadline turns the reminder on (it can be switched off).
                if (n.DueDate is null && date is not null) n.ReminderEnabled = true;
                n.DueDate = date;
                if (date is null) n.DueTime = null;
            });
        }
    }

    /// <summary>"HH:mm"; empty = no time (due at the end of the day). Accepts "9", "930", "9:30", "09.30".</summary>
    public string DueTimeText
    {
        get => _item?.Model.DueTime?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? string.Empty;
        set
        {
            if (!TimeInput.TryParse(value, out var time))
            {
                OnPropertyChanged();   // invalid input: show the stored value again
                return;
            }
            UpdateTask(n => n.DueTime == time, n =>
            {
                n.DueTime = time;
                if (time is not null && n.DueDate is null)
                {
                    n.DueDate = DateOnly.FromDateTime(DateTime.Today);
                    n.ReminderEnabled = true;
                }
            });
        }
    }

    public bool ReminderEnabled
    {
        get => _item?.Model.ReminderEnabled ?? false;
        set => UpdateTask(n => n.ReminderEnabled == value, n => n.ReminderEnabled = value);
    }

    public bool IsDone
    {
        get => _item?.Model.IsDone ?? false;
        set => UpdateTask(n => n.IsDone == value, n => n.IsDone = value);
    }

    public bool HasDue => _item?.Model.DueDate is not null;

    public string DueButtonText => _item?.Model.DueDate is { } date
        ? DueDateText.Absolute(date, _item.Model.DueTime, DateTime.Now)
        : "Határidő";

    /// <summary>Relative status shown next to the date, e.g. "Holnap", "Lejárt 2 napja".</summary>
    public string DueStatusText => _item is { Model.DueDate: not null } item && !item.Model.IsDone
        ? DueDateText.Relative(item.Model, DateTime.Now)
        : string.Empty;

    public ReminderState? DueState => _item?.DueState;

    [RelayCommand]
    private void SetDueInDays(string? days)
    {
        if (!int.TryParse(days, out var offset)) return;
        DueDateValue = DateTime.Today.AddDays(offset);
    }

    [RelayCommand]
    private void ClearDue()
    {
        UpdateTask(n => n.DueDate is null && !n.ReminderEnabled, n =>
        {
            n.DueDate = null;
            n.DueTime = null;
            n.ReminderEnabled = false;
        });
    }

    [RelayCommand]
    private void ToggleDone() => IsDone = !IsDone;

    /// <summary>Re-evaluates time-dependent texts (called periodically).</summary>
    public void RefreshTimeTexts()
    {
        OnPropertyChanged(nameof(DueStatusText));
        OnPropertyChanged(nameof(DueState));
    }

    private void UpdateTask(Func<Note, bool> isUnchanged, Action<Note> apply)
    {
        if (_item is null || isUnchanged(_item.Model)) return;

        apply(_item.Model);
        MarkChanged();
        RaiseTaskProperties();
        TaskStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseTaskProperties()
    {
        OnPropertyChanged(nameof(Priority));
        OnPropertyChanged(nameof(DueDateValue));
        OnPropertyChanged(nameof(DueTimeText));
        OnPropertyChanged(nameof(ReminderEnabled));
        OnPropertyChanged(nameof(IsDone));
        OnPropertyChanged(nameof(HasDue));
        OnPropertyChanged(nameof(DueButtonText));
        OnPropertyChanged(nameof(DueStatusText));
        OnPropertyChanged(nameof(DueState));
    }

    /// <summary>Re-reads the current note's model (after it was changed outside the editor).</summary>
    public void Reload()
    {
        var item = _item;
        _item = null;
        Load(item);
    }

    public void NotifyPinChanged() => OnPropertyChanged(nameof(IsPinned));

    /// <summary>Writes pending changes to disk immediately.</summary>
    public void Flush()
    {
        _saveTimer.Stop();
        if (!_isDirty || _item is null) return;

        try
        {
            _repository.Save(_item.Model);
            _isDirty = false;
            SaveStatus = $"Mentve {DateTime.Now:HH:mm}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SaveStatus = "Mentés sikertelen!";
            SaveFailed?.Invoke(this, ex);
        }
    }

    public string AddAsset(string fileName, byte[] data)
    {
        if (_item is null) throw new InvalidOperationException("Nincs megnyitott jegyzet.");

        var path = _item.Model.AddAsset(fileName, data);
        MarkChanged();
        return path;
    }

    public byte[]? GetAsset(string relativePath)
    {
        if (_item is null || !AssetName.IsAssetPath(relativePath)) return null;

        var name = Uri.UnescapeDataString(relativePath[(Note.AssetFolder.Length + 1)..]);
        return _item.Model.Assets.TryGetValue(name, out var data) ? data : null;
    }

    [RelayCommand]
    private void ToggleTask(int index) => Content = TaskListToggler.Toggle(Content, index);

    [RelayCommand]
    private void SaveNow()
    {
        if (_item is not null) _isDirty = true;
        Flush();
    }

    partial void OnTitleChanged(string value)
    {
        if (_isLoading || _item is null) return;
        _item.Model.Title = value;
        MarkChanged();
    }

    partial void OnContentChanged(string value)
    {
        WordCount = MarkdownText.CountWords(value);
        CharacterCount = value.Length;

        if (_isLoading || _item is null) return;
        _item.Model.Content = value;
        MarkChanged();

        _renderTimer.Stop();
        _renderTimer.Start();
    }

    private void MarkChanged()
    {
        _item!.Model.UpdatedAt = DateTimeOffset.Now;
        _item.Refresh();
        OnPropertyChanged(nameof(MetaText));

        _isDirty = true;
        SaveStatus = "Szerkesztés…";
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void RenderPreview()
    {
        _renderTimer.Stop();
        if (_item is null)
        {
            PreviewHtml = string.Empty;
            return;
        }

        var note = _item.Model;
        try
        {
            _assetCache.Sync(note);
        }
        catch (IOException)
        {
            // Images may not show in the preview, the note itself is unaffected.
        }

        PreviewHtml = _renderer.RenderHtml(Content, url => _assetCache.RewriteUrl(note.Id, url));
    }
}
