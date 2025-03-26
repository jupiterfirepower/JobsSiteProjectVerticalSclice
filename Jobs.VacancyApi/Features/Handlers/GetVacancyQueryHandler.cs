using Jobs.DTO;
using Jobs.VacancyApi.Contracts;
using Jobs.VacancyApi.Features.Queries;
using MediatR;

namespace Jobs.VacancyApi.Features.Handlers;

public class GetVacancyQueryHandler(IProcessingService service) : IRequestHandler<GetVacancyQuery, VacancyDto>
{
    public async Task<VacancyDto> Handle(GetVacancyQuery request, CancellationToken cancellationToken) => await service.GetVacancyById(request.Id);
}


