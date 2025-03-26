using Jobs.DTO;
using Jobs.VacancyApi.Contracts;
using Jobs.VacancyApi.Features.Queries;
using MediatR;

namespace Jobs.VacancyApi.Features.Handlers;

public class ListWorkTypesQueryHandler(IProcessingService service) : IRequestHandler<ListWorkTypesQuery, List<WorkTypeDto>>
{
    public async Task<List<WorkTypeDto>> Handle(ListWorkTypesQuery request, CancellationToken cancellationToken) =>
        await service.GetWorkTypesAsync();
}