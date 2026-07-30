namespace OrgSchema.Api.Services;
using OrgSchema.Api.Models;

public interface IOrgService
{
    // HiyerarÅŸik AÄŸaÃ§ YapÄ±sÄ± (Åema iÃ§in)
    Task<List<OrgNodeDto>> GetProcessedOrganizationChartAsync();
    
    // DÃ¼z Ã‡alÄ±ÅŸan Listesi (Kime DanÄ±ÅŸabilirim iÃ§in)
    Task<List<FinalEmployeeDto>> GetFlatEmployeeListAsync();
    Task<List<HierarchyResultDto>> SearchEmployeeHierarchyAsync(string query);
}


