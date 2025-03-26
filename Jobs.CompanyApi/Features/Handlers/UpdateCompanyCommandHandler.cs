using Jobs.CompanyApi.Features.Commands;
using Jobs.CompanyApi.Services.Contracts;
using MediatR;

namespace Jobs.CompanyApi.Features.Handlers;

public class UpdateCompanyCommandHandler(IProcessingService service) : IRequestHandler<UpdateCompanyCommand, int>
{
    public async Task<int> Handle(UpdateCompanyCommand command, CancellationToken cancellationToken) =>
        await service.UpdateCompany(command.Company);
}