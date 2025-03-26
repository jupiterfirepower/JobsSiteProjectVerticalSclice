using Jobs.DTO;
using MediatR;

namespace Jobs.CompanyApi.Features.Queries;

public record ListCompaniesQuery : IRequest<List<CompanyDto>>;

