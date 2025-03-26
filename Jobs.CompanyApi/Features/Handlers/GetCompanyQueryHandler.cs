using Jobs.CompanyApi.Features.Queries;
using Jobs.CompanyApi.Services.Contracts;
using Jobs.DTO;
using MediatR;

namespace Jobs.CompanyApi.Features.Handlers;

public class GetCompanyQueryHandler(IProcessingService service) : IRequestHandler<GetCompanyQuery, CompanyDto>
{
    public async Task<CompanyDto> Handle(GetCompanyQuery request, CancellationToken cancellationToken) => await service.GetCompanyById(request.Id);
}