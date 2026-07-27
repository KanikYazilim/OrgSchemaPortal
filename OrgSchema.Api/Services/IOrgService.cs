namespace OrgSchema.Api.Services;
using OrgSchema.Api.Models;

public interface IOrgService
{
    Task<List<EmployeeDto>> GetProcessedOrganizationChartAsync();
}
