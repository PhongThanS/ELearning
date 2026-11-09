using ELearning.Application.Questions;
using ELearning.Domain.Enums;

namespace ELearning.UnitTests.Questions;

/// <summary>Mức độ 1–4 ghi trong file nhập đề / Excel; giá trị cũ Dễ / Trung bình / Khó vẫn đọc được.</summary>
public class DifficultyLevelsTests
{
    [Theory]
    [InlineData("1", QuestionDifficulty.Level1)]
    [InlineData("2", QuestionDifficulty.Level2)]
    [InlineData("3", QuestionDifficulty.Level3)]
    [InlineData("4", QuestionDifficulty.Level4)]
    [InlineData("[MĐ4]", QuestionDifficulty.Level4)]
    [InlineData("MĐ 3", QuestionDifficulty.Level3)]
    [InlineData("Mức 2", QuestionDifficulty.Level2)]
    [InlineData("mức độ 1", QuestionDifficulty.Level1)]
    [InlineData("LEVEL4", QuestionDifficulty.Level4)]
    [InlineData("NB", QuestionDifficulty.Level1)]
    [InlineData("TH", QuestionDifficulty.Level2)]
    [InlineData("VD", QuestionDifficulty.Level3)]
    [InlineData("VDC", QuestionDifficulty.Level4)]
    [InlineData("Vận dụng cao", QuestionDifficulty.Level4)]
    [InlineData("EASY", QuestionDifficulty.Level1)]
    [InlineData("Trung bình", QuestionDifficulty.Level2)]
    [InlineData("Khó", QuestionDifficulty.Level3)]
    public void Reads_level_numbers_tags_and_old_values(string text, QuestionDifficulty expected)
    {
        DifficultyLevels.TryParse(text, out var difficulty).Should().BeTrue();
        difficulty.Should().Be(expected);
    }

    [Fact]
    public void Blank_is_no_difficulty_and_unknown_values_are_rejected()
    {
        DifficultyLevels.TryParse("  ", out var blank).Should().BeTrue();
        blank.Should().BeNull();
        DifficultyLevels.TryParse("5", out _).Should().BeFalse();
        DifficultyLevels.TryParse("rất khó", out _).Should().BeFalse();
    }
}
