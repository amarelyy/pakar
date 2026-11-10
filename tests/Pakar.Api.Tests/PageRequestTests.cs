using Pakar.Application.Common;

namespace Pakar.Api.Tests;

public class PageRequestTests
{
    [Fact]
    public void Defaults_to_first_page()
    {
        var paging = PageRequest.From(null, null);

        Assert.Equal(1, paging.Page);
        Assert.Equal(PageRequest.DefaultPageSize, paging.PageSize);
        Assert.Equal(0, paging.Skip);
    }

    [Theory]
    [InlineData(0, 5, 1, 5)]
    [InlineData(-3, 0, 1, 1)]
    [InlineData(2, 1000, 2, PageRequest.MaxPageSize)]
    public void Clamps_out_of_range_input(int page, int size, int expectedPage, int expectedSize)
    {
        var paging = PageRequest.From(page, size);

        Assert.Equal(expectedPage, paging.Page);
        Assert.Equal(expectedSize, paging.PageSize);
    }

    [Fact]
    public void Skip_follows_page_and_size()
    {
        Assert.Equal(40, PageRequest.From(3, 20).Skip);
    }
}
