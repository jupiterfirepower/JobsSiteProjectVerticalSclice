using Jobs.DTO.In;
using MediatR;

namespace Jobs.CompanyApi.Features.Commands;

public record UpdateCompanyCommand(CompanyInDto Company) : IRequest<int>;