using Jobs.DTO;
using Jobs.VacancyApi.Contracts;
using Jobs.VacancyApi.Features.Queries;
using MediatR;

namespace Jobs.VacancyApi.Features.Handlers;

public class GetEmploymentTypeQueryHandler(IProcessingService service) : IRequestHandler<GetEmploymentTypeQuery, EmploymentTypeDto>
{
    public async Task<EmploymentTypeDto> Handle(GetEmploymentTypeQuery request, CancellationToken cancellationToken) => 
        await service.GetEmploymentTypeByIdAsync(request.Id);
}