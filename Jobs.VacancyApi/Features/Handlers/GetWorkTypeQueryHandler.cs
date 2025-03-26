using Jobs.DTO;
using Jobs.VacancyApi.Contracts;
using Jobs.VacancyApi.Features.Queries;
using MediatR;

namespace Jobs.VacancyApi.Features.Handlers;

public class GetWorkTypeQueryHandler(IProcessingService service) : IRequestHandler<GetWorkTypeQuery, WorkTypeDto>
{
    public async Task<WorkTypeDto> Handle(GetWorkTypeQuery request, CancellationToken cancellationToken) => await service.GetWorkTypeByIdAsync(request.Id);
}