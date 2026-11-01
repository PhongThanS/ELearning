namespace ELearning.Domain.Videos;

/// <summary>Lớp học được xem một bài giảng (nhiều-nhiều giữa <see cref="VideoLesson"/> và lớp học).</summary>
public sealed class VideoLessonClassroom
{
    private VideoLessonClassroom()
    {
    }

    internal VideoLessonClassroom(Guid videoLessonId, Guid classroomId)
    {
        VideoLessonId = videoLessonId;
        ClassroomId = classroomId;
    }

    public Guid VideoLessonId { get; private set; }

    public Guid ClassroomId { get; private set; }
}
