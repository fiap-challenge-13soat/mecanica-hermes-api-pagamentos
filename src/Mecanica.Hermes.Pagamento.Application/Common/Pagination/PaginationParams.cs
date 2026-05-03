namespace Mecanica.Hermes.Pagamento.Application.Common.Pagination;

public sealed class PaginationParams
{
    private const int MaxPageSize = 100;
    private const int DefaultPageSize = 10;

    private readonly int _page = 1;
    private readonly int _pageSize = DefaultPageSize;

    public int Page
    {
        get => _page;
        init => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        init
        {
            if (value > MaxPageSize)
                _pageSize = MaxPageSize;
            else if (value < 1)
                _pageSize = DefaultPageSize;
            else
                _pageSize = value;
        }
    }

    public int Skip => (Page - 1) * PageSize;
}
