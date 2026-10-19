using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using OpenNotes.Core.Abstractions;
using OpenNotes.Core.Editing;
using OpenNotes.UI.Editing;

namespace OpenNotes.UI.Controls;

/// <summary>
/// Markdown source editor: AvalonEdit with syntax colouring, a formatting toolbar,
/// keyboard shortcuts, list continuation and image paste / drop.
/// </summary>
public partial class MarkdownEditor : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(MarkdownEditor),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextPropertyChanged));

    public static readonly DependencyProperty DocumentKeyProperty = DependencyProperty.Register(
        nameof(DocumentKey), typeof(object), typeof(MarkdownEditor), new PropertyMetadata(null, OnDocumentKeyChanged));

    public static readonly DependencyProperty AssetSinkProperty = DependencyProperty.Register(
        nameof(AssetSink), typeof(IAssetSink), typeof(MarkdownEditor));

    public static readonly DependencyProperty AssetSourceProperty = DependencyProperty.Register(
        nameof(AssetSource), typeof(IAssetSource), typeof(MarkdownEditor), new PropertyMetadata(null, OnAssetSourceChanged));

    public static readonly DependencyProperty OpenImageCommandProperty = DependencyProperty.Register(
        nameof(OpenImageCommand), typeof(ICommand), typeof(MarkdownEditor));

    public static readonly DependencyProperty InlineImageMaxHeightProperty = DependencyProperty.Register(
        nameof(InlineImageMaxHeight), typeof(double), typeof(MarkdownEditor), new PropertyMetadata(360d, OnInlineImageMaxHeightChanged));

    public static readonly DependencyProperty ImportFilesCommandProperty = DependencyProperty.Register(
        nameof(ImportFilesCommand), typeof(ICommand), typeof(MarkdownEditor));

    public static readonly DependencyProperty EditorFontSizeProperty = DependencyProperty.Register(
        nameof(EditorFontSize), typeof(double), typeof(MarkdownEditor), new PropertyMetadata(15d, OnEditorFontSizeChanged));

    public static readonly DependencyProperty CompactToolbarProperty = DependencyProperty.Register(
        nameof(CompactToolbar), typeof(bool), typeof(MarkdownEditor), new PropertyMetadata(false, OnCompactToolbarChanged));

    public static readonly DependencyProperty EditorPaddingProperty = DependencyProperty.Register(
        nameof(EditorPadding), typeof(Thickness), typeof(MarkdownEditor), new PropertyMetadata(new Thickness(36, 22, 32, 60), OnEditorPaddingChanged));

    private readonly MarkdownFormatter _formatter = new();
    private readonly AvalonEditSurface _surface;
    private readonly Dictionary<string, Action> _actions;
    private readonly InlineImageGenerator _imageGenerator = new();
    private bool _syncing;
    private double _lastLayoutWidth;

    public MarkdownEditor()
    {
        InitializeComponent();
        _surface = new AvalonEditSurface(Editor);
        _actions = CreateActions();
        ConfigureEditor();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Changes when another note is loaded: clears undo history and scrolls to the top.</summary>
    public object? DocumentKey
    {
        get => GetValue(DocumentKeyProperty);
        set => SetValue(DocumentKeyProperty, value);
    }

    public IAssetSink? AssetSink
    {
        get => (IAssetSink?)GetValue(AssetSinkProperty);
        set => SetValue(AssetSinkProperty, value);
    }

    /// <summary>Provides embedded images so they can be shown inline in the editor.</summary>
    public IAssetSource? AssetSource
    {
        get => (IAssetSource?)GetValue(AssetSourceProperty);
        set => SetValue(AssetSourceProperty, value);
    }

    /// <summary>Executed with the Markdown path (<c>assets/x.png</c>) when an inline image is clicked.</summary>
    public ICommand? OpenImageCommand
    {
        get => (ICommand?)GetValue(OpenImageCommandProperty);
        set => SetValue(OpenImageCommandProperty, value);
    }

    /// <summary>Maximum height of inline images (smaller in the mini window).</summary>
    public double InlineImageMaxHeight
    {
        get => (double)GetValue(InlineImageMaxHeightProperty);
        set => SetValue(InlineImageMaxHeightProperty, value);
    }

    /// <summary>Receives dropped .openNote / .md files (parameter: <c>IReadOnlyList&lt;string&gt;</c>).</summary>
    public ICommand? ImportFilesCommand
    {
        get => (ICommand?)GetValue(ImportFilesCommandProperty);
        set => SetValue(ImportFilesCommandProperty, value);
    }

    public double EditorFontSize
    {
        get => (double)GetValue(EditorFontSizeProperty);
        set => SetValue(EditorFontSizeProperty, value);
    }

    public bool CompactToolbar
    {
        get => (bool)GetValue(CompactToolbarProperty);
        set => SetValue(CompactToolbarProperty, value);
    }

    public Thickness EditorPadding
    {
        get => (Thickness)GetValue(EditorPaddingProperty);
        set => SetValue(EditorPaddingProperty, value);
    }

    public void FocusEditor(bool moveCaretToEnd = false)
    {
        if (moveCaretToEnd) Editor.CaretOffset = Editor.Document.TextLength;
        Editor.TextArea.Focus();
        Keyboard.Focus(Editor.TextArea);
        Editor.TextArea.Caret.BringCaretToView();
    }

    // ───────────────────────── setup ─────────────────────────

    private void ConfigureEditor()
    {
        Editor.SyntaxHighlighting = MarkdownHighlighting.Definition;

        var options = Editor.Options;
        options.EnableHyperlinks = false;
        options.EnableEmailHyperlinks = false;
        options.ConvertTabsToSpaces = true;
        options.IndentationSize = 2;
        options.HighlightCurrentLine = false;
        options.EnableRectangularSelection = false;
        options.AllowScrollBelowDocument = false;

        var textArea = Editor.TextArea;
        textArea.SelectionBrush = (Brush)FindResource("AccentSoftBrush");
        textArea.SelectionForeground = null;
        textArea.SelectionBorder = null;
        textArea.SelectionCornerRadius = 3;
        textArea.Caret.CaretBrush = (Brush)FindResource("AccentBrush");
        textArea.FocusVisualStyle = null;

        Editor.TextChanged += OnEditorTextChanged;
        textArea.PreviewKeyDown += OnEditorPreviewKeyDown;
        CommandManager.AddPreviewExecutedHandler(textArea, OnTextAreaPreviewExecuted);

        ConfigureInlineImages();

        Editor.AllowDrop = true;
        Editor.PreviewDragOver += OnPreviewDragOver;
        Editor.PreviewDrop += OnPreviewDrop;
    }

    // ───────────────────────── inline images ─────────────────────────

    private void ConfigureInlineImages()
    {
        var textArea = Editor.TextArea;
        var textView = textArea.TextView;

        textView.ElementGenerators.Add(_imageGenerator);
        _imageGenerator.ImageClicked += path =>
        {
            if (OpenImageCommand?.CanExecute(path) == true) OpenImageCommand.Execute(path);
        };

        // The caret line shows raw Markdown (editable); other lines show the picture.
        textArea.Caret.PositionChanged += (_, _) => UpdateRawImageLine();
        textArea.GotKeyboardFocus += (_, _) => UpdateRawImageLine();
        textArea.LostKeyboardFocus += (_, _) => UpdateRawImageLine();

        // Re-fit images when the editor width changes.
        textView.SizeChanged += (_, e) =>
        {
            if (!e.WidthChanged || Math.Abs(e.NewSize.Width - _lastLayoutWidth) < 24) return;
            _lastLayoutWidth = e.NewSize.Width;
            _imageGenerator.MaxWidth = Math.Max(80, e.NewSize.Width - 16);
            Dispatcher.InvokeAsync(() => textView.Redraw(), System.Windows.Threading.DispatcherPriority.Background);
        };
    }

    private void UpdateRawImageLine()
    {
        var textArea = Editor.TextArea;
        var newLine = textArea.IsKeyboardFocusWithin ? textArea.Caret.Line : 0;
        var oldLine = _imageGenerator.RawLineNumber;
        if (newLine == oldLine) return;

        _imageGenerator.RawLineNumber = newLine;
        RedrawLineIfItHasImage(oldLine);
        RedrawLineIfItHasImage(newLine);
    }

    private void RedrawLineIfItHasImage(int lineNumber)
    {
        var document = Editor.Document;
        if (lineNumber < 1 || lineNumber > document.LineCount) return;

        var line = document.GetLineByNumber(lineNumber);
        if (InlineImageGenerator.ContainsImage(document.GetText(line)))
            Editor.TextArea.TextView.Redraw(line);
    }

    private static void OnAssetSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (MarkdownEditor)d;
        control._imageGenerator.Source = e.NewValue as IAssetSource;
        control.Editor.TextArea.TextView.Redraw();
    }

    private static void OnInlineImageMaxHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (MarkdownEditor)d;
        control._imageGenerator.MaxHeight = (double)e.NewValue;
        control.Editor.TextArea.TextView.Redraw();
    }

    private Dictionary<string, Action> CreateActions() => new(StringComparer.Ordinal)
    {
        ["h1"] = () => _formatter.ToggleBlock(_surface, BlockStyle.Heading1),
        ["h2"] = () => _formatter.ToggleBlock(_surface, BlockStyle.Heading2),
        ["h3"] = () => _formatter.ToggleBlock(_surface, BlockStyle.Heading3),
        ["bold"] = () => _formatter.ToggleWrap(_surface, "**", "**", "félkövér"),
        ["italic"] = () => _formatter.ToggleWrap(_surface, "*", "*", "dőlt"),
        ["strike"] = () => _formatter.ToggleWrap(_surface, "~~", "~~", "áthúzott"),
        ["code"] = () => _formatter.ToggleWrap(_surface, "`", "`", "kód"),
        ["bullet"] = () => _formatter.ToggleBlock(_surface, BlockStyle.BulletList),
        ["numbered"] = () => _formatter.ToggleBlock(_surface, BlockStyle.NumberedList),
        ["task"] = () => _formatter.ToggleBlock(_surface, BlockStyle.TaskList),
        ["toggleTask"] = () => _formatter.ToggleTaskAtCaret(_surface),
        ["quote"] = () => _formatter.ToggleQuote(_surface),
        ["link"] = () => _formatter.InsertLink(_surface),
        ["image"] = PickAndInsertImages,
        ["codeblock"] = () => _formatter.InsertCodeBlock(_surface),
        ["table"] = () => _formatter.InsertBlock(_surface, MarkdownFormatter.TableTemplate),
        ["hr"] = () => _formatter.InsertBlock(_surface, "---"),
    };

    private void Run(string action)
    {
        if (!_actions.TryGetValue(action, out var handler)) return;
        handler();
        FocusEditor();
    }

    // ───────────────────────── text sync ─────────────────────────

    private static void OnTextPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((MarkdownEditor)d).ApplyExternalText(e.NewValue as string ?? string.Empty);

    private void ApplyExternalText(string text)
    {
        if (_syncing) return;

        var current = Editor.Document.Text;
        if (current == text) return;

        _syncing = true;
        try
        {
            // Replace only the changed middle part so the caret and scroll position survive.
            var max = Math.Min(current.Length, text.Length);
            var prefix = 0;
            while (prefix < max && current[prefix] == text[prefix]) prefix++;
            var suffix = 0;
            while (suffix < max - prefix && current[current.Length - 1 - suffix] == text[text.Length - 1 - suffix]) suffix++;

            Editor.Document.Replace(prefix, current.Length - prefix - suffix, text.Substring(prefix, text.Length - prefix - suffix));
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_syncing) return;

        _syncing = true;
        try { SetCurrentValue(TextProperty, Editor.Document.Text); }
        finally { _syncing = false; }
    }

    private static void OnDocumentKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (MarkdownEditor)d;
        control.Editor.Document.UndoStack.ClearAll();
        control.Editor.TextArea.TextView.Redraw();
        control.Editor.Select(0, 0);
        control.Editor.ScrollToHome();
    }

    private static void OnEditorFontSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((MarkdownEditor)d).Editor.FontSize = (double)e.NewValue;

    private static void OnEditorPaddingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((MarkdownEditor)d).Editor.Padding = (Thickness)e.NewValue;

    private static void OnCompactToolbarChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (MarkdownEditor)d;
        var visibility = (bool)e.NewValue ? Visibility.Collapsed : Visibility.Visible;
        foreach (var element in new UIElement[] { control.H3Button, control.StrikeButton, control.CodeBlockButton, control.TableButton, control.RuleButton })
            element.Visibility = visibility;
        control.ToolbarHost.Padding = (bool)e.NewValue ? new Thickness(8, 4, 8, 4) : new Thickness(18, 6, 12, 6);
    }

    // ───────────────────────── input ─────────────────────────

    private void OnToolbarClick(object sender, RoutedEventArgs e)
    {
        if (e.Source is ButtonBase { Tag: string action }) Run(action);
    }

    private void OnEditorPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var modifiers = Keyboard.Modifiers;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        string? action = (modifiers, key) switch
        {
            (ModifierKeys.None, Key.Enter) => _formatter.TryContinueList(_surface) ? string.Empty : null,
            (ModifierKeys.Control, Key.B) => "bold",
            (ModifierKeys.Control, Key.I) => "italic",
            (ModifierKeys.Control, Key.K) => "link",
            (ModifierKeys.Control, Key.D1) => "h1",
            (ModifierKeys.Control, Key.D2) => "h2",
            (ModifierKeys.Control, Key.D3) => "h3",
            (ModifierKeys.Control, Key.D0) => "paragraph",
            (ModifierKeys.Control, Key.Oem3) => "code",
            (ModifierKeys.Control, Key.Enter) => "toggleTask",
            (ModifierKeys.Control | ModifierKeys.Shift, Key.X) => "strike",
            (ModifierKeys.Control | ModifierKeys.Shift, Key.L) => "task",
            (ModifierKeys.Control | ModifierKeys.Shift, Key.D8) => "bullet",
            (ModifierKeys.Control | ModifierKeys.Shift, Key.D7) => "numbered",
            (ModifierKeys.Control | ModifierKeys.Shift, Key.Q) => "quote",
            _ => null,
        };

        if (action is null) return;
        e.Handled = true;

        if (action == "paragraph") _formatter.ToggleBlock(_surface, BlockStyle.Paragraph);
        else if (action.Length > 0) Run(action);
    }

    /// <summary>Ctrl+V with an image (screenshot) or image files on the clipboard embeds them in the note.</summary>
    private void OnTextAreaPreviewExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Command != ApplicationCommands.Paste || AssetSink is not { CanAddAssets: true }) return;

        try
        {
            if (Clipboard.ContainsFileDropList())
            {
                var files = Clipboard.GetFileDropList().Cast<string>().Where(ImageFiles.IsImage).ToList();
                if (files.Count == 0) return;
                InsertImageFiles(files);
                e.Handled = true;
            }
            else if (Clipboard.ContainsImage() && Clipboard.GetImage() is { } image)
            {
                InsertImage($"kep-{DateTime.Now:yyyyMMdd-HHmmss}.png", ImageFiles.EncodePng(image));
                e.Handled = true;
            }
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or IOException)
        {
            // Clipboard busy – fall back to the default paste.
        }
    }

    private void OnPreviewDragOver(object sender, DragEventArgs e)
    {
        if (GetDroppedFiles(e).Count == 0) return;
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnPreviewDrop(object sender, DragEventArgs e)
    {
        var files = GetDroppedFiles(e);
        if (files.Count == 0) return;
        e.Handled = true;

        var images = files.Where(ImageFiles.IsImage).ToList();
        var others = files.Except(images).ToList();

        if (images.Count > 0 && AssetSink is { CanAddAssets: true })
        {
            var position = Editor.GetPositionFromPoint(e.GetPosition(Editor));
            if (position is { } p) Editor.Select(Editor.Document.GetOffset(p.Location), 0);
            InsertImageFiles(images);
        }

        if (others.Count > 0 && ImportFilesCommand?.CanExecute(others) == true)
            ImportFilesCommand.Execute(others);
    }

    private static IReadOnlyList<string> GetDroppedFiles(DragEventArgs e) =>
        e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] files
            ? files
            : Array.Empty<string>();

    // ───────────────────────── images ─────────────────────────

    private void PickAndInsertImages()
    {
        if (AssetSink is not { CanAddAssets: true }) return;

        var dialog = new OpenFileDialog
        {
            Title = "Kép beszúrása",
            Filter = ImageFiles.DialogFilter,
            Multiselect = true,
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
            InsertImageFiles(dialog.FileNames);
    }

    private void InsertImageFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            try
            {
                InsertImage(Path.GetFileName(path), File.ReadAllBytes(path));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(Window.GetWindow(this)!, $"A kép nem olvasható:\n{path}\n\n{ex.Message}", "openNotes",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void InsertImage(string fileName, byte[] data)
    {
        if (AssetSink is not { CanAddAssets: true } sink) return;

        var relativePath = sink.AddAsset(fileName, data);
        _formatter.InsertImage(_surface, Path.GetFileNameWithoutExtension(fileName), relativePath);
    }
}
