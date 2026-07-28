namespace OrgSchema.Api.Services;
using OrgSchema.Api.Models;

public interface IOrgService
{
    // Hiyerarşik Ağaç Yapısı (Şema için)
    Task<List<OrgNodeDto>> GetProcessedOrganizationChartAsync();
    
    // Düz Çalışan Listesi (Kime Danışabilirim için)
    Task<List<FinalEmployeeDto>> GetFlatEmployeeListAsync();
}
