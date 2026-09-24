using TodoFlow.Models;

namespace TodoFlow.ViewModels;

public sealed class TodoItemEditorViewModel : ViewModelBase
{
    private readonly Action<TodoItemEditorViewModel> _save;
    private readonly Action _cancel;
    private string _title;
    private string _notes;
    private DateTimeOffset? _dueDate;
    private TodoPriority _priority;

    public TodoItemEditorViewModel(
        TodoItemViewModel item,
        IReadOnlyList<TodoPriority> priorityOptions,
        Action<TodoItemEditorViewModel> save,
        Action cancel)
    {
        ArgumentNullException.ThrowIfNull(item);

        _title = item.Title;
        _notes = item.Notes;
        _dueDate = item.DueDate;
        _priority = item.Priority;
        PriorityOptions = priorityOptions;
        _save = save ?? throw new ArgumentNullException(nameof(save));
        _cancel = cancel ?? throw new ArgumentNullException(nameof(cancel));

        SaveCommand = new RelayCommand(() => _save(this), () => CanSave);
        CancelCommand = new RelayCommand(() => _cancel());
    }

    public IReadOnlyList<TodoPriority> PriorityOptions { get; }

    public RelayCommand SaveCommand { get; }

    public RelayCommand CancelCommand { get; }

    public string Title
    {
        get => _title;
        set
        {
            if (!SetProperty(ref _title, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CanSave));
            SaveCommand.RaiseCanExecuteChanged();
        }
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public DateTimeOffset? DueDate
    {
        get => _dueDate;
        set => SetProperty(ref _dueDate, value);
    }

    public TodoPriority Priority
    {
        get => _priority;
        set => SetProperty(ref _priority, value);
    }

    public bool CanSave => !string.IsNullOrWhiteSpace(Title);
}