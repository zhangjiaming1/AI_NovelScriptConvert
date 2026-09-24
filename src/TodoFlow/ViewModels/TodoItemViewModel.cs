using System.Windows.Input;
using TodoFlow.Models;

namespace TodoFlow.ViewModels;

public sealed class TodoItemViewModel : ViewModelBase
{
    private readonly TodoItem _model;
    private readonly Action<TodoItemViewModel> _changed;
    private readonly Action<TodoItemViewModel> _deleteRequested;

    public TodoItemViewModel(
        TodoItem model,
        Action<TodoItemViewModel> changed,
        Action<TodoItemViewModel> deleteRequested)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _changed = changed ?? throw new ArgumentNullException(nameof(changed));
        _deleteRequested = deleteRequested ?? throw new ArgumentNullException(nameof(deleteRequested));
        DeleteCommand = new RelayCommand(() => _deleteRequested(this));
    }

    public Guid Id => _model.Id;

    public string Title => _model.Title;

    public string Notes => _model.Notes;

    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);

    public DateTimeOffset? DueDate => _model.DueDate;

    public TodoPriority Priority => _model.Priority;

    public DateTimeOffset CreatedAt => _model.CreatedAt;

    public bool IsCompleted
    {
        get => _model.IsCompleted;
        set
        {
            if (_model.IsCompleted == value)
            {
                return;
            }

            _model.IsCompleted = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsOverdue));
            OnPropertyChanged(nameof(StatusText));
            _changed(this);
        }
    }

    public bool IsOverdue =>
        !IsCompleted &&
        _model.DueDate is { } dueDate &&
        dueDate.ToLocalTime().Date < DateTime.Today;

    public string DueDateText
    {
        get
        {
            if (_model.DueDate is not { } rawDueDate)
            {
                return "未设置截止日期";
            }

            var dueDate = rawDueDate.ToLocalTime();
            if (IsOverdue)
            {
                return $"已逾期 · {dueDate:M月d日}";
            }

            var daysUntilDue = (dueDate.Date - DateTime.Today).Days;
            return daysUntilDue switch
            {
                0 => "今天截止",
                1 => "明天截止",
                _ => $"{dueDate:M月d日} 截止"
            };
        }
    }

    public string PriorityText => Priority switch
    {
        TodoPriority.High => "高优先级",
        TodoPriority.Normal => "普通",
        _ => "低优先级"
    };

    public string StatusText => IsCompleted
        ? "已完成"
        : IsOverdue
            ? "已逾期"
            : "进行中";

    public string CreatedAtText => $"创建于 {CreatedAt.ToLocalTime():M月d日 HH:mm}";

    public ICommand DeleteCommand { get; }

    public void Apply(TodoItemEditorViewModel editor)
    {
        _model.Title = editor.Title.Trim();
        _model.Notes = editor.Notes.Trim();
        _model.DueDate = editor.DueDate;
        _model.Priority = editor.Priority;

        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Notes));
        OnPropertyChanged(nameof(HasNotes));
        OnPropertyChanged(nameof(DueDate));
        OnPropertyChanged(nameof(IsOverdue));
        OnPropertyChanged(nameof(DueDateText));
        OnPropertyChanged(nameof(Priority));
        OnPropertyChanged(nameof(PriorityText));
        OnPropertyChanged(nameof(StatusText));

        _changed(this);
    }

    public TodoItem ToModel()
    {
        return new TodoItem
        {
            Id = _model.Id,
            Title = _model.Title,
            Notes = _model.Notes,
            IsCompleted = _model.IsCompleted,
            DueDate = _model.DueDate,
            Priority = _model.Priority,
            CreatedAt = _model.CreatedAt
        };
    }
}