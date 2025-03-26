using Jobs.CompanyApi.Features.Commands;
using Jobs.CompanyApi.Services.Contracts;
using Jobs.DTO;
using MediatR;

namespace Jobs.CompanyApi.Features.Handlers;

public class CreateCompanyCommandHandler(IProcessingService service) : IRequestHandler<CreateCompanyCommand, CompanyDto>
{
    public async Task<CompanyDto> Handle(CreateCompanyCommand command, CancellationToken cancellationToken) =>
        await service.CreateCompany(command.Company);
}