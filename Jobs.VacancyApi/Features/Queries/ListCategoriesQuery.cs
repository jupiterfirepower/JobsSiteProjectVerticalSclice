using Jobs.DTO;
using MediatR;

namespace Jobs.VacancyApi.Features.Queries;

public record ListCategoriesQuery : IRequest<List<CategoryDto>>;