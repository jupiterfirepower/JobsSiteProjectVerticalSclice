using MediatR;

namespace Jobs.CompanyApi.Features.Commands;

public record DeleteCompanyCommand(int Id) : IRequest<int>;