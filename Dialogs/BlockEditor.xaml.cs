using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OCCMissionGoals.Models;
using OCCMissionGoals.Services;

namespace OCCMissionGoals.Dialogs;

/// <summary>
/// 内容区区块编辑器：按类型编辑、拖拽排序、增删区块，多级列表通过「子块」递归展开。
/// 编辑作用在内容的副本上，取消编辑不会改动原条目。
/// </summary>
public partial class BlockEditor : UserControl
{
    private const string RowDataFormat = "OCCMissionGoals.BlockRow";

    private List<ContentBlock> _models = new();
    private Point _dragStart;
    private BlockRow? _pendingDrag;
    private BlockRow? _dropRow;

    public ObservableCollection<BlockRow> Blocks { get; } = new();

    /// <summary>内容有任何变化（增删 / 排序 / 字段编辑）时触发，供外部实时刷新派生信息。</summary>
    public event EventHandler? Changed;

    public BlockEditor()
    {
        InitializeComponent();
        RootList.ItemsSource = Blocks;
        Blocks.CollectionChanged += (_, _) =>
        {
            UpdateEmptyHint();
            RaiseChanged();
        };
        UpdateEmptyHint();
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>用一组区块填充编辑器（内部用副本）。</summary>
    public void Load(System.Collections.Generic.IEnumerable<ContentBlock>? contents)
    {
        Blocks.Clear();
        _models = ContentBlocks.CloneBlocks(contents);
        foreach (var model in _models)
            Blocks.Add(new BlockRow(model, _models, Blocks, inList: false, RaiseChanged));
        UpdateEmptyHint();
    }

    /// <summary>导出编辑结果（顶层区块，按当前顺序）。</summary>
    public List<ContentBlock> Build() => Blocks.Select(row => row.Model).ToList();

    private void UpdateEmptyHint() =>
        EmptyHint.Visibility = Blocks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    // ==================== 添加 ====================

    private void AddRoot_Click(object sender, RoutedEventArgs e) => OpenMenu(sender);

    private void AddChild_Click(object sender, RoutedEventArgs e) => OpenMenu(sender);

    private static void OpenMenu(object sender)
    {
        if (sender is not Button button || button.ContextMenu == null) return;
        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.IsOpen = true;
    }

    private void AddMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item) return;
        if (ParseKind(item.Tag as string) is not { } parsed) return;

        var target = (item.Parent as ContextMenu)?.PlacementTarget as FrameworkElement;

        if (target?.DataContext is BlockRow row)
            AddChild(row, parsed.Kind, parsed.Ordered);
        else
            AddRoot(parsed.Kind, parsed.Ordered);
    }

    private static (ContentBlockKind Kind, bool Ordered)? ParseKind(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        var parts = tag.Split(':');
        if (!Enum.TryParse<ContentBlockKind>(parts[0], out var kind)) return null;
        var ordered = parts.Length > 1 && bool.TryParse(parts[1], out var value) && value;
        return (kind, ordered);
    }

    private static ContentBlock CreateBlock(ContentBlockKind kind, bool ordered) => kind switch
    {
        ContentBlockKind.Divider => ContentBlock.NewDivider(),
        ContentBlockKind.Table => ContentBlock.NewTable(),
        ContentBlockKind.Code => ContentBlock.NewCode(),
        ContentBlockKind.FileRef => ContentBlock.NewFileRef(),
        ContentBlockKind.List => ContentBlock.NewList(ordered),
        ContentBlockKind.SubTask => ContentBlock.NewSubTask(),
        _ => ContentBlock.NewText(),
    };

    private void AddRoot(ContentBlockKind kind, bool ordered)
    {
        var model = CreateBlock(kind, ordered);
        _models.Add(model);
        Blocks.Add(new BlockRow(model, _models, Blocks, inList: false, RaiseChanged));
        UpdateEmptyHint();
    }

    private void AddChild(BlockRow parent, ContentBlockKind kind, bool ordered)
    {
        var model = CreateBlock(kind, ordered);
        parent.Model.Items.Add(model);
        parent.Children.Add(new BlockRow(
            model,
            parent.Model.Items,
            parent.Children,
            parent.Model.Kind == ContentBlockKind.List,
            RaiseChanged));
    }

    // ==================== 移动 / 删除 ====================

    private void MoveUp_Click(object sender, RoutedEventArgs e) => Move(sender, -1);

    private void MoveDown_Click(object sender, RoutedEventArgs e) => Move(sender, +1);

    private static void Move(object sender, int delta)
    {
        if ((sender as FrameworkElement)?.DataContext is not BlockRow row) return;

        var index = row.Owner.IndexOf(row);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= row.Owner.Count) return;

        row.Owner.Move(index, target);

        var from = row.ModelItems.IndexOf(row.Model);
        if (from >= 0)
        {
            row.ModelItems.RemoveAt(from);
            row.ModelItems.Insert(target, row.Model);
        }
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not BlockRow row) return;

        row.Owner.Remove(row);
        row.ModelItems.Remove(row.Model);
        UpdateEmptyHint();
    }

    // ==================== 同层拖拽排序 ====================

    private void Handle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not BlockRow row) return;
        _pendingDrag = row;
        _dragStart = e.GetPosition(this);
    }

    private void Handle_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_pendingDrag == null || e.LeftButton != MouseButtonState.Pressed) return;

        var position = e.GetPosition(this);
        if (Math.Abs(position.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var row = _pendingDrag;
        _pendingDrag = null;
        try
        {
            DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(RowDataFormat, row), DragDropEffects.Move);
        }
        finally
        {
            ClearDropIndicator();
        }
    }

    private void Row_PreviewDragOver(object sender, DragEventArgs e)
    {
        e.Handled = true;

        var target = (sender as FrameworkElement)?.DataContext as BlockRow;
        var source = e.Data.GetData(RowDataFormat) as BlockRow;

        // 只在同一层内排序；跨层（含拖入列表）用「＋」菜单添加子块。
        if (target == null || source == null || ReferenceEquals(target, source) ||
            !ReferenceEquals(target.Owner, source.Owner))
        {
            ClearDropIndicator();
            e.Effects = DragDropEffects.None;
            return;
        }

        if (!ReferenceEquals(_dropRow, target))
        {
            ClearDropIndicator();
            _dropRow = target;
        }

        var element = (FrameworkElement)sender;
        var insertBefore = e.GetPosition(element).Y < element.ActualHeight / 2;
        target.DropBefore = insertBefore;
        target.DropAfter = !insertBefore;

        e.Effects = DragDropEffects.Move;
    }

    private void Row_PreviewDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;

        var target = (sender as FrameworkElement)?.DataContext as BlockRow;
        var source = e.Data.GetData(RowDataFormat) as BlockRow;
        var insertBefore = target?.DropBefore ?? true;
        ClearDropIndicator();

        if (target == null || source == null || ReferenceEquals(target, source) ||
            !ReferenceEquals(target.Owner, source.Owner))
            return;

        source.Owner.Remove(source);
        source.ModelItems.Remove(source.Model);

        var index = source.Owner.IndexOf(target);
        if (index < 0)
            index = source.Owner.Count;
        else if (!insertBefore)
            index++;

        source.Owner.Insert(index, source);
        source.ModelItems.Insert(index, source.Model);
    }

    private void ClearDropIndicator()
    {
        if (_dropRow == null) return;
        _dropRow.DropBefore = false;
        _dropRow.DropAfter = false;
        _dropRow = null;
    }
}

/// <summary>
/// 区块行的视图模型：直接读写底层 <see cref="ContentBlock"/>，
/// 让编辑器不必在保存时再做一次树转换。
/// </summary>
public sealed class BlockRow : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public ContentBlock Model { get; }

    /// <summary>该行所在的 VM 集合（同层拖拽与增删都在这个集合里做）。</summary>
    public ObservableCollection<BlockRow> Owner { get; }

    /// <summary>该行对应的模型集合（与 <see cref="Owner"/> 保持同序）。</summary>
    public List<ContentBlock> ModelItems { get; }

    public ObservableCollection<BlockRow> Children { get; } = new();

    /// <summary>该行是否位于列表容器内（是则显示完成勾选框）。</summary>
    public bool InList { get; }

    private bool _dropBefore;
    private bool _dropAfter;
    private string? _headersText;
    private string? _rowsText;
    private readonly Action? _changed;

    public BlockRow(
        ContentBlock model,
        List<ContentBlock> modelItems,
        ObservableCollection<BlockRow> owner,
        bool inList,
        Action? changed = null)
    {
        Model = model;
        ModelItems = modelItems;
        Owner = owner;
        InList = inList;
        _changed = changed;

        Children.CollectionChanged += (_, _) => _changed?.Invoke();

        var childInList = model.Kind == ContentBlockKind.List;
        foreach (var child in model.Items)
            Children.Add(new BlockRow(child, model.Items, Children, childInList, changed));
    }

    // ==================== 显示开关 ====================

    public bool ShowTextEditor => Model.Kind == ContentBlockKind.Text;
    public bool ShowSubTaskEditor => Model.Kind == ContentBlockKind.SubTask;
    public bool ShowDividerLabel => Model.Kind == ContentBlockKind.Divider;
    public bool ShowTableEditor => Model.Kind == ContentBlockKind.Table;
    public bool ShowCodeEditor => Model.Kind == ContentBlockKind.Code;
    public bool ShowFileEditor => Model.Kind == ContentBlockKind.FileRef;
    public bool ShowListEditor => Model.Kind == ContentBlockKind.List;

    public string KindLabel => Model.Kind switch
    {
        ContentBlockKind.Table => LocalizationManager.T("表格"),
        ContentBlockKind.Divider => LocalizationManager.T("分割线"),
        ContentBlockKind.Code => LocalizationManager.T("代码块"),
        ContentBlockKind.FileRef => LocalizationManager.T("文件引用"),
        ContentBlockKind.List => Model.Ordered ? LocalizationManager.T("编号列表") : LocalizationManager.T("列表"),
        ContentBlockKind.SubTask => LocalizationManager.T("子任务"),
        _ => LocalizationManager.T("文本"),
    };

    public bool DropBefore
    {
        get => _dropBefore;
        set { if (_dropBefore == value) return; _dropBefore = value; OnPropertyChanged(); }
    }

    public bool DropAfter
    {
        get => _dropAfter;
        set { if (_dropAfter == value) return; _dropAfter = value; OnPropertyChanged(); }
    }

    // ==================== 各类型字段 ====================

    public string Text
    {
        get => Model.Text;
        set
        {
            if (Model.Text == value) return;
            Model.Text = value ?? string.Empty;
            OnPropertyChanged();
        }
    }

    public bool Ordered
    {
        get => Model.Ordered;
        set
        {
            if (Model.Ordered == value) return;
            Model.Ordered = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(KindLabel));
        }
    }

    /// <summary>子任务区块的勾选状态（保存条目时计入完成度）。</summary>
    public bool Done
    {
        get => Model.Done;
        set
        {
            if (Model.Done == value) return;
            Model.Done = value;
            OnPropertyChanged();
        }
    }

    // ---- 代码块 ----

    public string Language
    {
        get => Model.Code?.Language ?? string.Empty;
        set
        {
            EnsureCode();
            if (Model.Code!.Language == value) return;
            Model.Code.Language = value ?? string.Empty;
            OnPropertyChanged();
        }
    }

    public string Code
    {
        get => Model.Code?.Code ?? string.Empty;
        set
        {
            EnsureCode();
            if (Model.Code!.Code == value) return;
            Model.Code.Code = value ?? string.Empty;
            OnPropertyChanged();
        }
    }

    public bool ShowLineNumbers
    {
        get => Model.Code?.ShowLineNumbers ?? true;
        set
        {
            EnsureCode();
            if (Model.Code!.ShowLineNumbers == value) return;
            Model.Code.ShowLineNumbers = value;
            OnPropertyChanged();
        }
    }

    // ---- 表格 ----

    public string HeadersText
    {
        get => _headersText ??= Model.Table == null ? string.Empty : string.Join(" | ", Model.Table.Headers);
        set
        {
            _headersText = value;
            EnsureTable();
            Model.Table!.Headers = SplitCells(value);
            OnPropertyChanged();
        }
    }

    public string RowsText
    {
        get => _rowsText ??= Model.Table == null
            ? string.Empty
            : string.Join("\n", Model.Table.Rows.Select(row => string.Join(" | ", row)));
        set
        {
            _rowsText = value;
            EnsureTable();
            Model.Table!.Rows = (value ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n')
                .Select(SplitCells)
                .ToList();
            OnPropertyChanged();
        }
    }

    // ---- 文件引用 ----

    public string FilePath
    {
        get => Model.File?.Path ?? string.Empty;
        set
        {
            EnsureFile();
            if (Model.File!.Path == value) return;
            Model.File.Path = value ?? string.Empty;
            OnPropertyChanged();
        }
    }

    public string Function
    {
        get => Model.File?.Function ?? string.Empty;
        set
        {
            EnsureFile();
            if (Model.File!.Function == value) return;
            Model.File.Function = value ?? string.Empty;
            OnPropertyChanged();
        }
    }

    public int Line
    {
        get => Model.File?.Line ?? 0;
        set
        {
            EnsureFile();
            if (Model.File!.Line == value) return;
            Model.File.Line = value;
            OnPropertyChanged();
        }
    }

    public int Column
    {
        get => Model.File?.Column ?? 0;
        set
        {
            EnsureFile();
            if (Model.File!.Column == value) return;
            Model.File.Column = value;
            OnPropertyChanged();
        }
    }

    private void EnsureCode() => Model.Code ??= new CodeBlockData();

    private void EnsureTable() => Model.Table ??= new TableBlockData();

    private void EnsureFile() => Model.File ??= new FileRef();

    private static List<string> SplitCells(string? line) =>
        (line ?? string.Empty).Split('|').Select(cell => cell.Trim()).ToList();

    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        _changed?.Invoke();
    }
}
