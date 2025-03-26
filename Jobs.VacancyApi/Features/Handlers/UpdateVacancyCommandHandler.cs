using Jobs.VacancyApi.Contracts;
using Jobs.VacancyApi.Features.Commands;
using MediatR;

namespace Jobs.VacancyApi.Features.Handlers;

public class UpdateVacancyCommandHandler(IProcessingService service) : IRequestHandler<UpdateVacancyCommand, int>
{
    public async Task<int> Handle(UpdateVacancyCommand command, CancellationToken cancellationToken) =>
        await service.UpdateVacancy(command.Vacancy);
}