namespace OrgSchema.Api.Services;
using OrgSchema.Api.Models;
using OrgSchema.Api.Data;
using Microsoft.EntityFrameworkCore;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

public class OrgService : IOrgService
{
    private readonly OrgSchemaDbContext _dbContext;
    private readonly string _connectionString;

    public OrgService(OrgSchemaDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? "";
    }

    public async Task<List<OrgNodeDto>> GetProcessedOrganizationChartAsync()
    {
        var rawData = await GetRealSapDataAsync();
        
        // Şimdilik Override tablosu ve HiddenOrganization tablosu boş varsayıyoruz, 
        // veritabanına eklenince buraya entegre edilecek. (O(1) Merge Algoritması)
        var finalData = rawData; // İleride Merge(rawData, overrides) olacak

        // Ağacı İnşa Et
        var tree = BuildEnterpriseTree(finalData);

        return tree;
    }

    public async Task<List<FinalEmployeeDto>> GetFlatEmployeeListAsync()
    {
        var rawData = await GetRealSapDataAsync();
        return rawData.OrderBy(x => x.NameSurname).ToList();
    }

    private async Task<List<FinalEmployeeDto>> GetRealSapDataAsync()
    {
        var sql = @"
            SELECT 
                LTRIM(RTRIM(CAST(SICILNO AS NVARCHAR(100)))) AS SicilNo,
                LTRIM(RTRIM(ENAME)) AS NameSurname,
                LTRIM(RTRIM(KMAIL)) AS Email,
                LTRIM(RTRIM(POS)) AS PositionName,
                LTRIM(RTRIM(DEP)) AS DepartmentId,
                LTRIM(RTRIM(DEPAD)) AS DepartmentName,
                LTRIM(RTRIM(BIRIM)) AS UnitId,
                LTRIM(RTRIM(BIRIM)) AS UnitName,
                LTRIM(RTRIM(SIRKET)) AS CompanyId,
                LTRIM(RTRIM(SIRKET)) AS CompanyName,
                LTRIM(RTRIM(MANAGER)) AS Manager,
                LTRIM(RTRIM(CAST(MANAGERSICILNO AS NVARCHAR(100)))) AS ManagerSicilNo
            FROM KisiKart
            WHERE SICILNO IN (SELECT DISTINCT MANAGERSICILNO FROM KisiKart WHERE MANAGERSICILNO IS NOT NULL AND MANAGERSICILNO != '00000000')
        ";

        using var connection = new SqlConnection(_connectionString);
        var result = await connection.QueryAsync<FinalEmployeeDto>(sql);
        
        // Tekilleştirme (Sicil No'ya göre)
        var distinct = result
            .GroupBy(x => x.SicilNo)
            .Select(g => g.First())
            .ToList();

        return distinct;
    }

    private List<OrgNodeDto> BuildEnterpriseTree(List<FinalEmployeeDto> allEmployees)
    {
        var nodeDictionary = new Dictionary<string, OrgNodeDto>();
        var rootNodes = new List<OrgNodeDto>();

        // 1. Her çalışan için ait olacağı Pozisyon Kutusunun ID'sini belirle
        // Kutular, bağlı oldukları yöneticinin SicilNo'su ve Pozisyon Adı ile eşsiz hale gelir.
        var sicilToBoxId = new Dictionary<string, string>();
        foreach (var emp in allEmployees)
        {
            string managerSicil = string.IsNullOrWhiteSpace(emp.ManagerSicilNo) || emp.ManagerSicilNo == emp.SicilNo ? "00000000" : emp.ManagerSicilNo.Trim();
            string posName = string.IsNullOrWhiteSpace(emp.PositionName) ? "Belirtilmemiş" : emp.PositionName.Trim();
            
            string boxId = $"{managerSicil}_{posName}";
            sicilToBoxId[emp.SicilNo] = boxId;
        }

        // 2. Pozisyon Kutularını oluştur ve çalışanları içine doldur
        foreach (var emp in allEmployees)
        {
            string boxId = sicilToBoxId[emp.SicilNo];
            string posName = string.IsNullOrWhiteSpace(emp.PositionName) ? "Belirtilmemiş" : emp.PositionName.Trim();
            
            if (!nodeDictionary.ContainsKey(boxId))
            {
                var node = new OrgNodeDto 
                { 
                    Id = boxId, 
                    Name = posName, 
                    Type = NodeType.Position 
                };
                nodeDictionary[boxId] = node;
            }

            nodeDictionary[boxId].Employees.Add(new EmployeeSummaryDto
            {
                SicilNo = emp.SicilNo,
                NameSurname = emp.NameSurname,
                Email = emp.Email
            });
        }

        // 3. Kutuları Birbirine Bağla (Parent-Child İlişkisi)
        foreach (var kvp in nodeDictionary)
        {
            var node = kvp.Value;
            
            // Bu kutudaki herhangi bir çalışanı referans alarak yöneticisini bulalım
            var representativeEmp = node.Employees.First();
            var empData = allEmployees.First(e => e.SicilNo == representativeEmp.SicilNo);
            
            string managerSicil = string.IsNullOrWhiteSpace(empData.ManagerSicilNo) || empData.ManagerSicilNo == empData.SicilNo ? "00000000" : empData.ManagerSicilNo.Trim();
            
            // Eğer yöneticisi yoksa (00000000) veya yönetici veritabanında bulunamadıysa bu bir ROOT (Kök) düğümdür.
            if (managerSicil == "00000000" || !sicilToBoxId.ContainsKey(managerSicil))
            {
                rootNodes.Add(node);
            }
            else
            {
                // Yöneticinin kutusunu bul
                string parentBoxId = sicilToBoxId[managerSicil];
                if (nodeDictionary.TryGetValue(parentBoxId, out var parentNode))
                {
                    // Döngüsel referans kontrolü (Kendi kendine bağlanmasını engelle)
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

        // Her kutu zaten içindeki çalışanlar sayesinde var olduğu için boş kutu oluşma ihtimali SIFIRDIR.
        // Bu yüzden Prune (Budama) yapmaya gerek yoktur. Dümdüz organik hiyerarşi döner.
        return rootNodes;
    }
}
