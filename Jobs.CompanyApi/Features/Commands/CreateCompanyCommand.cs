using Jobs.DTO;
using Jobs.DTO.In;
using MediatR;

namespace Jobs.CompanyApi.Features.Commands;

public record CreateCompanyCommand(CompanyInDto Company) : IRequest<CompanyDto>;