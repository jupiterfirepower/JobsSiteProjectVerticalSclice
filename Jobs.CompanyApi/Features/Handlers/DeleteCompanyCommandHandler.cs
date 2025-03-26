using Jobs.CompanyApi.Features.Commands;
using Jobs.CompanyApi.Services.Contracts;
using MediatR;

namespace Jobs.CompanyApi.Features.Handlers;

public class DeleteCompanyCommandHandler(IProcessingService service) : IRequestHandler<DeleteCompanyCommand, int>
{
    public async Task<int> Handle(DeleteCompanyCommand command, CancellationToken cancellationToken) =>
        await service.DeleteCompany(command.Id);
}