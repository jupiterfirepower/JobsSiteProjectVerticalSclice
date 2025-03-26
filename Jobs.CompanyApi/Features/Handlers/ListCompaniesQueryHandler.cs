using Jobs.CompanyApi.Features.Queries;
using Jobs.CompanyApi.Services.Contracts;
using Jobs.DTO;
using MediatR;

namespace Jobs.CompanyApi.Features.Handlers;

public class ListCompaniesQueryHandler(IProcessingService service) : IRequestHandler<ListCompaniesQuery, List<CompanyDto>>
{
    public async Task<List<CompanyDto>> Handle(ListCompaniesQuery request, CancellationToken cancellationToken) => await service.GetCompanies();
}