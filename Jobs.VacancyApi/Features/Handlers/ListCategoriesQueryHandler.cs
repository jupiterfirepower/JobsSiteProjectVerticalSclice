using Jobs.DTO;
using Jobs.VacancyApi.Contracts;
using Jobs.VacancyApi.Features.Queries;
using MediatR;

namespace Jobs.VacancyApi.Features.Handlers;

public class ListCategoriesQueryHandler(IProcessingService service) : IRequestHandler<ListCategoriesQuery, List<CategoryDto>>
{
    public async Task<List<CategoryDto>> Handle(ListCategoriesQuery request, CancellationToken cancellationToken) =>
        await service.GetCategoriesAsync();
}