using MediatR;

namespace Jobs.VacancyApi.Features.Commands;

public record DeleteVacancyCommand(int Id) : IRequest<int>;