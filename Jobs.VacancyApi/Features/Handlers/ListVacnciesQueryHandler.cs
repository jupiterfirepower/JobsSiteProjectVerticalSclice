using Jobs.DTO;
using Jobs.VacancyApi.Contracts;
using Jobs.VacancyApi.Features.Queries;
using MediatR;

namespace Jobs.VacancyApi.Features.Handlers;

public class ListVacanciesQueryHandler(IProcessingService service) : IRequestHandler<ListVacanciesQuery, List<VacancyDto>>
{
    public async Task<List<VacancyDto>> Handle(ListVacanciesQuery request, CancellationToken cancellationToken) => await service.GetVacancies();
}


