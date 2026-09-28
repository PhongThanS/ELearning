using ELearning.Shared;
using ELearning.Shared.Paging;
using ELearning.Shared.Results;

namespace ELearning.UnitTests.Shared;

public class SharedPrimitivesTests
{
    [Theory]
    [InlineData(0, 0, 1, PageRequest.DefaultPageSize)]
    [InlineData(-5, 10, 1, 10)]
    [InlineData(3, 500, 3, PageRequest.MaxPageSize)]
    [InlineData(2, 100, 2, 100)]
    public void PageRequest_clamps_page_and_page_size(int page, int pageSize, int expectedPage, int expectedSize)
    {
        var request = new PageRequest { Page = page, PageSize = pageSize };

        request.Page.Should().Be(expectedPage);
        request.PageSize.Should().Be(expectedSize);
    }

    [Fact]
    public void PageRequest_computes_skip()
    {
        new PageRequest { Page = 3, PageSize = 20 }.Skip.Should().Be(40);
    }

    [Theory]
    [InlineData(0, 20, 0)]
    [InlineData(150, 20, 8)]
    [InlineData(20, 20, 1)]
    public void PagedResult_computes_total_pages(int total, int pageSize, int expected)
    {
        new PagedResult<int>([], 1, pageSize, total).TotalPages.Should().Be(expected);
    }

    [Fact]
    public void Result_success_exposes_value()
    {
        Result<int> result = 42;

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Result_failure_exposes_first_error_and_hides_value()
    {
        Result<int> result = Error.NotFound(ErrorCodes.ExamNotFound);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Code.Should().Be(ErrorCodes.ExamNotFound);
        var readValue = () => result.Value;
        readValue.Should().Throw<InvalidOperationException>();
    }
}
