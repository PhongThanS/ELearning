using ELearning.Application.Questions;
using ELearning.Domain.Enums;

namespace ELearning.UnitTests.Questions;

/// <summary>Định dạng file đề "elearning-exam/1" (docs/02-nghiep-vu.md mục 1.5, D-29).</summary>
public class ExamMarkdownParserTests
{
    private const string Header = "@format: elearning-exam/1\n";

    [Fact]
    public void Sample_file_shipped_to_users_parses_without_issues()
    {
        var doc = ExamMarkdownParser.Parse(File.ReadAllText(PublicFile("mau-de-thi.md")));

        doc.FileIssues.Should().BeEmpty();
        doc.Questions.Should().HaveCount(8);
        doc.Questions.SelectMany(q => q.Issues).Should().BeEmpty();
        doc.Header.Title.Should().Be("Đề mẫu – Kiểm tra Toán 12");
        doc.Header.DurationMinutes.Should().Be(90);
        doc.Header.CategoryCode.Should().Be("TOAN12");
        doc.Questions.Select(q => q.Input!.QuestionType).Should().Equal(
            QuestionType.SingleChoice, QuestionType.SingleChoice, QuestionType.TrueFalse, QuestionType.FillIn,
            QuestionType.FillIn, QuestionType.MultipleChoice, QuestionType.FillIn, QuestionType.Essay);
        doc.Questions.Sum(q => q.Input!.DefaultScore).Should().Be(5.5m);
    }

    [Fact]
    public void Example_inside_ai_prompt_parses_without_issues()
    {
        const string fence = "```";
        var prompt = File.ReadAllText(PublicFile("prompt-chuyen-de-thi.md")).ReplaceLineEndings("\n");
        var start = prompt.IndexOf(fence + "markdown\n@format:", StringComparison.Ordinal);
        var end = prompt.IndexOf("\n" + fence + "\n", start, StringComparison.Ordinal);
        start.Should().BeGreaterThan(0, "prompt phải có ví dụ hoàn chỉnh trong khối ```markdown");

        var doc = ExamMarkdownParser.Parse(prompt[(start + fence.Length + "markdown\n".Length)..end]);

        doc.FileIssues.Should().BeEmpty();
        doc.Questions.Should().HaveCount(5);
        doc.Questions.SelectMany(q => q.Issues).Should().BeEmpty();
    }

    [Fact]
    public void Text_answers_accept_pipe_escaped_from_markdown_table()
    {
        var q = ExamMarkdownParser.Parse(Header + "@question: 1\n@type: TEXT\n@score: 1\n@content: Thủ đô?\n@answer: Hà Nội \\| Ha Noi\n")
            .Questions.Single();

        q.Input!.AcceptedAnswers.Should().Equal("Hà Nội", "Ha Noi");
    }

    [Fact]
    public void Single_choice_keeps_multiline_markdown_latex_and_image()
    {
        var doc = ExamMarkdownParser.Parse(Header + """
            @question: 1
            @type: SINGLE_CHOICE
            @score: 0,25
            @content:
            Cho $y = \dfrac{1}{x}$.

            ![Hình 1](hinh-1.png)
            @option A: $1$
            @option B: $2$
            @answer: B
            @explanation:
            Dòng 1
            @@ten-dang-nhap bắt đầu bằng @
            """);

        var q = doc.Questions.Single();
        q.Issues.Should().BeEmpty();
        q.Input!.Content.Should().Be("Cho $y = \\dfrac{1}{x}$.\n\n![Hình 1](hinh-1.png)");
        q.Input.DefaultScore.Should().Be(0.25m);
        q.Input.ContentFormat.Should().Be(ContentFormat.Markdown);
        q.Input.Options!.Select(o => (o.OptionCode, o.Content, o.IsCorrect)).Should().Equal(("A", "$1$", false), ("B", "$2$", true));
        q.Input.Explanation.Should().Be("Dòng 1\n@ten-dang-nhap bắt đầu bằng @");
        ExamMarkdownParser.ImageRefs(q.Input.Content).Should().ContainSingle(r => r.Target == "hinh-1.png");
    }

    [Fact]
    public void True_false_answers_map_to_statements()
    {
        var q = ExamMarkdownParser.Parse(Header + """
            @question: 1
            @type: TRUE_FALSE
            @score: 1
            @content: Cho hàm số.
            @statement a: Ý a
            @statement b: Ý b
            @statement c: Ý c
            @statement d: Ý d
            @answer: a=Đúng; b=S, c=đ, d=sai
            """).Questions.Single();

        q.Issues.Should().BeEmpty();
        q.Input!.QuestionType.Should().Be(QuestionType.TrueFalse);
        q.Input.Options!.Select(o => (o.OptionCode, o.IsCorrect)).Should().Equal(("a", true), ("b", false), ("c", true), ("d", false));
    }

    [Fact]
    public void Number_and_text_answers()
    {
        var doc = ExamMarkdownParser.Parse(Header + """
            @question: 1
            @type: NUMBER
            @score: 0.5
            @content: Tính.
            @answer: -2,5
            @tolerance: 0.01
            @question: 2
            @type: TEXT
            @score: 0.5
            @content: Thủ đô?
            @answer: Hà Nội | Ha Noi
            @ignore_accent: yes
            """);

        doc.Questions.SelectMany(q => q.Issues).Should().BeEmpty();
        var number = doc.Questions[0].Input!;
        number.AnswerDataType.Should().Be(AnswerDataType.Number);
        number.CorrectAnswerNumber.Should().Be(-2.5m);
        number.NumericTolerance.Should().Be(0.01m);
        var text = doc.Questions[1].Input!;
        text.AcceptedAnswers.Should().Equal("Hà Nội", "Ha Noi");
        text.IgnoreAccent.Should().BeTrue();
    }

    [Fact]
    public void Part_defaults_apply_until_next_part_and_question_overrides()
    {
        var doc = ExamMarkdownParser.Parse(Header + """
            @part: Phần I
            @type: NUMBER
            @score: 0.5
            @question: 1
            @content: A
            @answer: 1
            @question: 2
            @score: 1
            @content: B
            @answer: 2
            @part: Phần II
            @question: 1
            @content: C
            @answer: 3
            """);

        doc.Questions[0].Input!.DefaultScore.Should().Be(0.5m);
        doc.Questions[0].Part.Should().Be("Phần I");
        doc.Questions[1].Input!.DefaultScore.Should().Be(1m);
        doc.Questions[2].Issues.Should().Contain(i => i.Code == "TYPE_REQUIRED", "mặc định của phần trước không áp dụng cho phần sau");
    }

    [Fact]
    public void Mistakes_are_reported_with_line_numbers_instead_of_guessed()
    {
        var doc = ExamMarkdownParser.Parse(Header + """
            @question: 1
            @type: SINGLE_CHOICE
            @score: 0.25
            @content: Câu hỏi
            @option A: 1
            @option C: 2
            @answer: E
            @question: 2
            @type: TRUE_FALSE
            @score: 1
            @content: Câu 2
            @statement a: x
            @statement b: y
            @answer: a=Đ
            @question: 3
            @type: NUMBER
            @score: 0.5
            @content: Câu 3
            @answer: ?
            @colour: đỏ
            Dòng lạc ngoài trường
            """);

        var q1 = doc.Questions[0].Issues;
        q1.Should().Contain(i => i.Code == "OPTION_ORDER" && i.Line == 7);
        q1.Should().Contain(i => i.Code == "ANSWER_INVALID" && i.Line == 8);
        doc.Questions[1].Issues.Should().Contain(i => i.Code == "ANSWER_INCOMPLETE" && i.Message.Contains('b'));
        doc.Questions[2].Issues.Should().Contain(i => i.Code == "ANSWER_REQUIRED");
        doc.FileIssues.Should().Contain(i => i.Code == "UNKNOWN_FIELD" && i.Line == 21);
    }

    [Fact]
    public void File_must_declare_format()
    {
        var doc = ExamMarkdownParser.Parse("@question: 1\n@type: ESSAY\n@score: 1\n@content: X\n");

        doc.FileIssues.Should().Contain(i => i.Code == "FORMAT_MISSING");
        doc.Questions.Single().Issues.Should().BeEmpty();
    }

    [Fact]
    public void Code_fence_copied_from_ai_chat_is_ignored_but_inner_code_blocks_are_kept()
    {
        const string fence = "```";
        var text = fence + "markdown\n" + Header
            + "@question: 1\n@type: ESSAY\n@score: 1\n@content:\nGiải thích đoạn mã:\n"
            + fence + "python\nprint(1)\n" + fence + "\n" + fence + "\n";

        var doc = ExamMarkdownParser.Parse(text);

        doc.FileIssues.Should().BeEmpty();
        doc.Questions.Single().Input!.Content.Should().Be("Giải thích đoạn mã:\n" + fence + "python\nprint(1)\n" + fence);
    }

    private static string PublicFile(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "frontend", "elearning-web", "public", "huong-dan", name);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Không tìm thấy frontend/elearning-web/public/huong-dan/{name}");
    }
}
