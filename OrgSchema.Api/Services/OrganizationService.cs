using Dapper;
using Microsoft.Data.SqlClient;
using OrgSchema.Api.Models;

namespace OrgSchema.Api.Services;

public class HRPositionDto
{
    public string USERID { get; set; } = string.Empty;
    public string DESCRIPTION { get; set; } = string.Empty;
}

public class UserOverrideDto
{
    public string USERID { get; set; } = string.Empty;
    public string MANAGERUSERID { get; set; } = string.Empty;
    public string POSITIONNAME { get; set; } = string.Empty;
    public string DEPARTMENTNAME { get; set; } = string.Empty;
}

public class OrganizationService
{
    private readonly string _connectionString;

    public OrganizationService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new ArgumentNullException("Connection string is missing.");
    }

    public async Task<List<OrgNodeDto>> BuildAsync()
    {
        // 1. SQL'den Oku
        var orgData = await FetchOrganizationDataAsync();
        var positions = await FetchPositionsAsync();
        var overrides = await FetchOverridesAsync();

        // 2. Override Uygula (Normalize & Correct)
        ApplyOverrides(orgData, overrides);

        // 3. Tree Oluştur (Pozisyon tabanlı organik hiyerarşi)
        var tree = BuildPositionTree(orgData, positions);

        return tree;
    }

    public async Task<List<HROrganizationDto>> GetFlatEmployeeListAsync()
    {
        var orgData = await FetchOrganizationDataAsync();
        var overrides = await FetchOverridesAsync();
        var positions = await FetchPositionsAsync();
        var positionDict = positions.GroupBy(p => p.USERID).ToDictionary(g => g.Key, g => g.First().DESCRIPTION);

        ApplyOverrides(orgData, overrides);
        
        foreach (var emp in orgData)
        {
            if (positionDict.TryGetValue(emp.USERID, out var desc))
            {
                emp.POSITIONNAME = desc;
            }
        }
        return orgData.OrderBy(x => x.ENAME).ToList();
    }

    private async Task<List<HROrganizationDto>> FetchOrganizationDataAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        // Tüm çalışanları çek
        var sql = "SELECT * FROM HROrganizationTable";
        var result = await connection.QueryAsync<HROrganizationDto>(sql);
        
        // Tekilleştirme (USERID'ye göre)
        return result.GroupBy(x => x.USERID).Select(g => g.First()).ToList();
    }

    private async Task<List<HRPositionDto>> FetchPositionsAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        try 
        {
            var sql = "SELECT USERID, DESCRIPTION FROM HRPositionsTable";
            var result = await connection.QueryAsync<HRPositionDto>(sql);
            return result.ToList();
        }
        catch 
        {
            // Tablo yoksa veya hata verirse boş dön
            return new List<HRPositionDto>();
        }
    }

    private async Task<List<UserOverrideDto>> FetchOverridesAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        try
        {
            // HierarchyOverrides tablosu, yanlış bilgilerin doğrusunu içerir
            var sql = "SELECT USERID, MANAGERUSERID, POSITIONNAME, DEPARTMENTNAME FROM HierarchyOverrides";
            var result = await connection.QueryAsync<UserOverrideDto>(sql);
            return result.ToList();
        }
        catch
        {
            // Tablo henüz açılmamışsa boş dön
            return new List<UserOverrideDto>();
        }
    }

    private void ApplyOverrides(List<HROrganizationDto> orgData, List<UserOverrideDto> overrides)
    {
        var overrideDict = overrides.GroupBy(x => x.USERID).ToDictionary(g => g.Key, g => g.First());

        foreach (var emp in orgData)
        {
            // "Huzur Hakkı" temizliği (Otomatik kural)
            if (!string.IsNullOrWhiteSpace(emp.DEPARTMENTNAME) && emp.DEPARTMENTNAME.Contains("Huzur Hakkı", StringComparison.OrdinalIgnoreCase))
            {
                emp.DEPARTMENTNAME = "Yönetim Kurulu";
            }
            if (!string.IsNullOrWhiteSpace(emp.POSITIONNAME) && emp.POSITIONNAME.Contains("Huzur Hakkı", StringComparison.OrdinalIgnoreCase))
            {
                emp.POSITIONNAME = "Yönetim Kurulu Üyesi";
            }

            // Tablodan gelen manuel ezmeler
            if (overrideDict.TryGetValue(emp.USERID, out var over))
            {
                if (!string.IsNullOrWhiteSpace(over.MANAGERUSERID))
                    emp.MANAGERUSERID = over.MANAGERUSERID;

                if (!string.IsNullOrWhiteSpace(over.POSITIONNAME))
                    emp.POSITIONNAME = over.POSITIONNAME;

                if (!string.IsNullOrWhiteSpace(over.DEPARTMENTNAME))
                    emp.DEPARTMENTNAME = over.DEPARTMENTNAME;
            }
            
            // Trim ve null koruması
            emp.MANAGERUSERID = emp.MANAGERUSERID?.Trim() ?? "";
            emp.POSITIONNAME = string.IsNullOrWhiteSpace(emp.POSITIONNAME) ? "Belirtilmemiş" : emp.POSITIONNAME.Trim();
        }
    }

    private List<OrgNodeDto> BuildPositionTree(List<HROrganizationDto> allEmployees, List<HRPositionDto> positions)
    {
        var nodeDictionary = new Dictionary<string, OrgNodeDto>();
        var rootNodes = new List<OrgNodeDto>();
        
        var positionDict = positions.GroupBy(p => p.USERID).ToDictionary(g => g.Key, g => g.First().DESCRIPTION);

        // 1. Her çalışan için Box ID belirle (ManagerUserId + PositionName)
        var userToBoxId = new Dictionary<string, string>();
        foreach (var emp in allEmployees)
        {
            string managerId = string.IsNullOrWhiteSpace(emp.MANAGERUSERID) || emp.MANAGERUSERID == emp.USERID ? "ROOT" : emp.MANAGERUSERID;
            string boxId = $"{managerId}_{emp.POSITIONNAME}";
            userToBoxId[emp.USERID] = boxId;
        }

        // 2. Kutuları oluştur ve doldur
        foreach (var emp in allEmployees)
        {
            string boxId = userToBoxId[emp.USERID];
            
            if (!nodeDictionary.ContainsKey(boxId))
            {
                var node = new OrgNodeDto 
                { 
                    Id = boxId, 
                    Name = emp.POSITIONNAME, 
                    Type = NodeType.Position 
                };
                nodeDictionary[boxId] = node;
            }

            // Kişinin unvanını (Title/Description) al
            string unvan = positionDict.TryGetValue(emp.USERID, out var desc) ? desc : emp.POSITIONNAME;

            nodeDictionary[boxId].Employees.Add(new EmployeeSummaryDto
            {
                SicilNo = emp.USERID,
                NameSurname = emp.ENAME,
                Email = unvan // Email alanına şimdilik unvanı koyalım, UI'da unvan göstermek için
            });
        }

        // 3. Parent-Child ilişkisi kur
        foreach (var kvp in nodeDictionary)
        {
            var node = kvp.Value;
            var representativeEmp = node.Employees.First();
            var empData = allEmployees.First(e => e.USERID == representativeEmp.SicilNo);
            
            string managerId = string.IsNullOrWhiteSpace(empData.MANAGERUSERID) || empData.MANAGERUSERID == empData.USERID ? "ROOT" : empData.MANAGERUSERID;

            if (managerId == "ROOT" || !userToBoxId.ContainsKey(managerId))
            {
                rootNodes.Add(node);
            }
            else
            {
                string parentBoxId = userToBoxId[managerId];
                if (nodeDictionary.TryGetValue(parentBoxId, out var parentNode))
                {
                    if (parentBoxId != node.Id)
                    {
                        node.ParentId = parentBoxId;
                        parentNode.Children.Add(node);
                    }
                    else 
                    {
                        rootNodes.Add(node);
                    }
                }
                else
                {
                    rootNodes.Add(node);
                }
            }
        }

        return rootNodes;
    }
}
