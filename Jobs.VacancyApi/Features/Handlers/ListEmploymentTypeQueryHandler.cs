using Jobs.DTO;
using Jobs.VacancyApi.Contracts;
using Jobs.VacancyApi.Features.Queries;
using MediatR;

namespace Jobs.VacancyApi.Features.Handlers;

public class ListEmploymentTypeQueryHandler(IProcessingService service) : IRequestHandler<ListEmploymentTypesQuery, List<EmploymentTypeDto>>
{
    public async Task<List<EmploymentTypeDto>> Handle(ListEmploymentTypesQuery request, CancellationToken cancellationToken) => 
        await service.GetEmploymentTypesAsync();
}