namespace ELearning.Domain.Enums;

/// <summary>Sự kiện ghi nhận trong lượt thi.</summary>
public enum AttemptEventType
{
    VisibilityHidden = 1,
    VisibilityVisible = 2,
    WindowBlur = 3,
    WindowFocus = 4,
    FullscreenExit = 5,
    Offline = 6,
    Online = 7,
    MultiTabDetected = 8,
    PageReload = 9,
    Paste = 10,
}
