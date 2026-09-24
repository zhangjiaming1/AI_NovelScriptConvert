using System.Collections.ObjectModel;
using TodoFlow.Infrastructure;
using TodoFlow.Models;

namespace TodoFlow.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly ITodoRepository _repository;
    private readonly ObservableCollection<TodoItemViewModel> _allTodos = new();

    private bool _initialized;
    private bool _isBusy;
    private string _newTitle = string.Empty;
    private string _newNotes = string.Empty;
    private DateTimeOffset? _newDueDate;
    private TodoPriority _newPriority = TodoPriority.Normal;
    private string _searchText = string.Empty;
    private TodoFilter _currentFilter = TodoFilter.All;
    private TodoItemViewModel? _selectedTodo;
    private TodoItemEditorViewModel? _editor;
    private string _statusMessage = "准备就绪";
    private int _saveVersion;

    public MainWindowViewModel(ITodoRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        PriorityOptions = Enum.GetValues<TodoPriority>();

        AddCommand = new RelayCommand(AddTodo, () => CanAdd);
        DeleteSelectedCommand = new RelayCommand(DeleteSelected, () => HasSelection && !IsBusy);
        ClearCompletedCommand = new RelayCommand(ClearCompleted, () => CompletedCount > 0 && !IsBusy);
        ShowAllCommand = new RelayCommand(() => SetFilter(TodoFilter.All));
        ShowActiveCommand = new RelayCommand(() => SetFilter(TodoFilter.Active));
        ShowCompletedCommand = new RelayCommand(() => SetFilter(TodoFilter.Completed));
        SetDueTodayCommand = new RelayCommand(() => NewDueDate = DateTimeOffset.Now.Date);
        SetDueTomorrowCommand = new RelayCommand(() => NewDueDate = DateTimeOffset.Now.Date.AddDays(1));
        ClearDueDateCommand = new RelayCommand(() => NewDueDate = null);
    }

    public ObservableCollection<TodoItemViewModel> VisibleTodos { get; } = new();

    public IReadOnlyList<TodoPriority> PriorityOptions { get; }

    public RelayCommand AddCommand { get; }

    public RelayCommand DeleteSelectedCommand { get; }

    public RelayCommand ClearCompletedCommand { get; }

    public RelayCommand ShowAllCommand { get; }

    public RelayCommand ShowActiveCommand { get; }

    public RelayCommand ShowCompletedCommand { get; }

    public RelayCommand SetDueTodayCommand { get; }

    public RelayCommand SetDueTomorrowCommand { get; }

    public RelayCommand ClearDueDateCommand { get; }

    public string DataFilePath => _repository.FilePath;

    public string TodayText => DateTime.Now.ToString("yyyy年M月d日 dddd");

    public string NewTitle
    {
        get => _newTitle;
        set
        {
            if (!SetProperty(ref _newTitle, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CanAdd));
            AddCommand.RaiseCanExecuteChanged();
        }
    }

    public string NewNotes
    {
        get => _newNotes;
        set => SetProperty(ref _newNotes, value);
    }

    public DateTimeOffset? NewDueDate
    {
        get => _newDueDate;
        set => SetProperty(ref _newDueDate, value);
    }

    public TodoPriority NewPriority
    {
        get => _newPriority;
        set => SetProperty(ref _newPriority, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value))
            {
                return;
            }

            RefreshVisibleTodos();
        }
    }

    public TodoItemViewModel? SelectedTodo
    {
        get => _selectedTodo;
        set
        {
            if (!SetProperty(ref _selectedTodo, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(HasNoSelection));
            DeleteSelectedCommand.RaiseCanExecuteChanged();

            Editor = value is null
                ? null
                : new TodoItemEditorViewModel(value, PriorityOptions, SaveEditor, CancelEditor);
        }
    }

    public TodoItemEditorViewModel? Editor
    {
        get => _editor;
        private set => SetProperty(ref _editor, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CanAdd));
            AddCommand.RaiseCanExecuteChanged();
            DeleteSelectedCommand.RaiseCanExecuteChanged();
            ClearCompletedCommand.RaiseCanExecuteChanged();
        }
    }

    public TodoFilter CurrentFilter => _currentFilter;

    public bool IsAllFilter => CurrentFilter == TodoFilter.All;

    public bool IsActiveFilter => CurrentFilter == TodoFilter.Active;

    public bool IsCompletedFilter => CurrentFilter == TodoFilter.Completed;

    public bool HasSelection => SelectedTodo is not null;

    public bool HasNoSelection => !HasSelection;

    public bool HasVisibleTodos => VisibleTodos.Count > 0;

    public bool HasNoVisibleTodos => !HasVisibleTodos;

    public int TotalCount => _allTodos.Count;

    public int ActiveCount => _allTodos.Count(item => !item.IsCompleted);

    public int CompletedCount => _allTodos.Count(item => item.IsCompleted);

    public double CompletionPercent =>
        TotalCount == 0 ? 0 : CompletedCount * 100d / TotalCount;

    public string ProgressText => $"{CompletedCount}/{TotalCount}";

    public string VisibleCountText => CurrentFilter == TodoFilter.All && string.IsNullOrWhiteSpace(SearchText)
        ? $"{TotalCount} 项任务"
        : $"显示 {VisibleTodos.Count} / {TotalCount} 项";

    public string EmptyStateTitle => TotalCount == 0 ? "还没有任务" : "没有匹配的任务";

    public string EmptyStateHint => TotalCount == 0
        ? "在上方输入一个标题，创建你的第一项待办。"
        : "尝试切换筛选条件，或修改搜索关键词。";

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool CanAdd => !IsBusy && !string.IsNullOrWhiteSpace(NewTitle);

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        IsBusy = true;

        try
        {
            var items = await _repository.LoadAsync();

            _allTodos.Clear();
            foreach (var item in items)
            {
                _allTodos.Add(CreateItemViewModel(item));
            }

            RefreshVisibleTodos();
            UpdateStats();
            StatusMessage = items.Count == 0
                ? "还没有任务，添加第一项吧。"
                : $"已加载 {items.Count} 项任务。";
        }
        catch (Exception exception)
        {
            StatusMessage = $"加载失败：{exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private TodoItemViewModel CreateItemViewModel(TodoItem item)
    {
        return new TodoItemViewModel(item, OnItemChanged, DeleteTodo);
    }

    private void AddTodo()
    {
        if (!CanAdd)
        {
            return;
        }

        var item = new TodoItem
        {
            Title = NewTitle.Trim(),
            Notes = NewNotes.Trim(),
            DueDate = NewDueDate,
            Priority = NewPriority
        };

        var itemViewModel = CreateItemViewModel(item);
        _allTodos.Insert(0, itemViewModel);

        _currentFilter = TodoFilter.All;
        _searchText = string.Empty;
        OnPropertyChanged(nameof(CurrentFilter));
        OnPropertyChanged(nameof(IsAllFilter));
        OnPropertyChanged(nameof(IsActiveFilter));
        OnPropertyChanged(nameof(IsCompletedFilter));
        OnPropertyChanged(nameof(SearchText));

        NewTitle = string.Empty;
        NewNotes = string.Empty;
        NewDueDate = null;
        NewPriority = TodoPriority.Normal;

        RefreshVisibleTodos();
        SelectedTodo = itemViewModel;
        UpdateStats();
        StatusMessage = "任务已添加。";
        _ = PersistAsync();
    }

    private void DeleteSelected()
    {
        if (SelectedTodo is { } selectedTodo)
        {
            DeleteTodo(selectedTodo);
        }
    }

    private void DeleteTodo(TodoItemViewModel item)
    {
        _allTodos.Remove(item);
        VisibleTodos.Remove(item);

        if (ReferenceEquals(SelectedTodo, item))
        {
            SelectedTodo = null;
        }

        UpdateStats();
        StatusMessage = "任务已删除。";
        _ = PersistAsync();
    }

    private void ClearCompleted()
    {
        var completedItems = _allTodos.Where(item => item.IsCompleted).ToList();
        if (completedItems.Count == 0)
        {
            return;
        }

        foreach (var item in completedItems)
        {
            _allTodos.Remove(item);
            VisibleTodos.Remove(item);

            if (ReferenceEquals(SelectedTodo, item))
            {
                SelectedTodo = null;
            }
        }

        RefreshVisibleTodos();
        UpdateStats();
        StatusMessage = $"已清除 {completedItems.Count} 项已完成任务。";
        _ = PersistAsync();
    }

    private void SetFilter(TodoFilter filter)
    {
        if (_currentFilter == filter)
        {
            return;
        }

        _currentFilter = filter;
        OnPropertyChanged(nameof(CurrentFilter));
        OnPropertyChanged(nameof(IsAllFilter));
        OnPropertyChanged(nameof(IsActiveFilter));
        OnPropertyChanged(nameof(IsCompletedFilter));
        RefreshVisibleTodos();
        UpdateStats();
    }

    private void OnItemChanged(TodoItemViewModel item)
    {
        UpdateStats();

        if (CurrentFilter != TodoFilter.All || !string.IsNullOrWhiteSpace(SearchText))
        {
            RefreshVisibleTodos();
        }

        _ = PersistAsync();
    }

    private void SaveEditor(TodoItemEditorViewModel editor)
    {
        if (SelectedTodo is not { } selectedTodo || !ReferenceEquals(Editor, editor))
        {
            return;
        }

        selectedTodo.Apply(editor);
        Editor = new TodoItemEditorViewModel(
            selectedTodo,
            PriorityOptions,
            SaveEditor,
            CancelEditor);

        StatusMessage = "修改已保存。";
    }

    private void CancelEditor()
    {
        if (SelectedTodo is not { } selectedTodo)
        {
            return;
        }

        Editor = new TodoItemEditorViewModel(
            selectedTodo,
            PriorityOptions,
            SaveEditor,
            CancelEditor);

        StatusMessage = "已放弃未保存的修改。";
    }

    private void RefreshVisibleTodos()
    {
        var selectedItem = SelectedTodo;

        VisibleTodos.Clear();
        foreach (var item in _allTodos.Where(MatchesCurrentView))
        {
            VisibleTodos.Add(item);
        }

        if (selectedItem is not null && VisibleTodos.Contains(selectedItem))
        {
            SelectedTodo = selectedItem;
        }
        else if (selectedItem is not null)
        {
            SelectedTodo = null;
        }

        OnPropertyChanged(nameof(HasVisibleTodos));
        OnPropertyChanged(nameof(HasNoVisibleTodos));
        OnPropertyChanged(nameof(VisibleCountText));
    }

    private bool MatchesCurrentView(TodoItemViewModel item)
    {
        var query = SearchText.Trim();
        if (query.Length > 0)
        {
            var matchesSearch =
                item.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                item.Notes.Contains(query, StringComparison.CurrentCultureIgnoreCase);

            if (!matchesSearch)
            {
                return false;
            }
        }

        return CurrentFilter switch
        {
            TodoFilter.Active => !item.IsCompleted,
            TodoFilter.Completed => item.IsCompleted,
            _ => true
        };
    }

    private void UpdateStats()
    {
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(ActiveCount));
        OnPropertyChanged(nameof(CompletedCount));
        OnPropertyChanged(nameof(CompletionPercent));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(VisibleCountText));
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateHint));
        ClearCompletedCommand.RaiseCanExecuteChanged();
    }

    private async Task PersistAsync()
    {
        var version = ++_saveVersion;
        var snapshot = _allTodos.Select(item => item.ToModel()).ToList();

        try
        {
            await _repository.SaveAsync(snapshot);

            if (version == _saveVersion)
            {
                StatusMessage = $"已自动保存 · {DateTime.Now:HH:mm}";
            }
        }
        catch (Exception exception)
        {
            StatusMessage = $"保存失败：{exception.Message}";
        }
    }
}