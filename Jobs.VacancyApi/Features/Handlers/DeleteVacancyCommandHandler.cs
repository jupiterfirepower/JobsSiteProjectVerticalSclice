using Jobs.VacancyApi.Contracts;
using Jobs.VacancyApi.Features.Commands;
using MediatR;

namespace Jobs.VacancyApi.Features.Handlers;

public class DeleteVacancyCommandHandler(IProcessingService service) : IRequestHandler<DeleteVacancyCommand, int>
{
    public async Task<int> Handle(DeleteVacancyCommand command, CancellationToken cancellationToken) =>
        await service.DeleteVacancy(command.Id);
}