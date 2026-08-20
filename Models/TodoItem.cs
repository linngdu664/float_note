using CommunityToolkit.Mvvm.ComponentModel;

namespace FloatNote.Models;

public sealed class TodoItem : ObservableObject
{
    private string _title = string.Empty;
    private string _content = string.Empty;
    private bool _isCompleted;
    private bool _isExpanded;
    private bool _isCurrent;
    private DateTimeOffset? _completedAt;

    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public int Order { get; set; }

    public string? Text
    {
        get => Title;
        set
        {
            if (!string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            Title = value;
        }
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }

    public bool IsCompleted
    {
        get => _isCompleted;
        set
        {
            if (!SetProperty(ref _isCompleted, value))
            {
                return;
            }

            CompletedAt = value ? DateTimeOffset.Now : null;

            if (value)
            {
                IsCurrent = false;
            }
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public bool IsCurrent
    {
        get => _isCurrent;
        set => SetProperty(ref _isCurrent, value);
    }

    public DateTimeOffset? CompletedAt
    {
        get => _completedAt;
        set => SetProperty(ref _completedAt, value);
    }
}
