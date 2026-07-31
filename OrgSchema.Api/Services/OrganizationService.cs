using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using OrgSchema.Api.Models;

namespace OrgSchema.Api.Services;

public class OrganizationService : IOrgService
{
    private readonly string _connectionString;
    private readonly IConfiguration _configuration;

    public OrganizationService(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new ArgumentNullException("Connection string is missing.");
    }

    public async Task<List<HROrganizationDto>> GetRawOrganizationAsync()
    {
        var query = @"
            SELECT 
                COALESCE(o.SICILNO, k.SICILNO) as SICILNO,
                COALESCE(o.ENAME, k.ENAME) as ENAME,
                COALESCE(o.UserId, UPPER(SUBSTRING(k.KMAIL, 1, CHARINDEX('@', k.KMAIL + '@') - 1))) as USERID,
                '' as FIRSTNAME,
                '' as LASTNAME,
                CAST(k.DEPARTMANFIRMAID AS VARCHAR) as DEPARTMENT,
                k.DEPARTMANFIRMAADI as DEPARTMENTNAME,
                k.BIRIMADI as UNITNAME,
                COALESCE(o.PROFESSION, k.POZISYONADI) as PROFESSION,
                COALESCE(o.POSITIONNAME, k.POZISYONADI) as POSITIONNAME,
                COALESCE(o.MANAGERUSERID, '') as MANAGERUSERID,
                COALESCE(o.MANAGERSICILNO, CASE WHEN k.MANAGERSICILNO = k.SICILNO THEN NULL ELSE k.MANAGERSICILNO END) as MANAGERSICILNO,
                CAST(k.SIRKET AS VARCHAR) as COMPANY,
                k.SICILFIRMAADI as COMPANYNAME,
                ISNULL(o.IsHidden, 0) as IsHidden,
                ISNULL(o.SortOrder, 999) as SortOrder
            FROM KanikUserManagement_OrgSchema.dbo.KisiKart2 k
            LEFT JOIN KanikUserManagement_OrgSchema.dbo.HierarchyOverrides o 
                ON (o.SICILNO = k.SICILNO AND ISNULL(o.SICILNO, '') <> '')
                OR (o.UserId = UPPER(SUBSTRING(k.KMAIL, 1, CHARINDEX('@', k.KMAIL + '@') - 1))) AND ISNULL(o.UserId, '') <> ''
            WHERE ISNULL(o.IsHidden, 0) = 0
              AND k.AKTIF = 1
        ";

        using var connection = new SqlConnection(_connectionString);
        var data = await connection.QueryAsync<HROrganizationDto>(query);
        return data.ToList();
    }

    private async Task<List<ViewOrgAgacDto>> GetViewOrgAgacAsync()
    {
        var query = "SELECT * FROM View_ORG_Agac";
        using var connection = new SqlConnection(_connectionString);
        var data = await connection.QueryAsync<ViewOrgAgacDto>(query);
        return data.ToList();
    }

    private async Task<List<ViewOrgBirimYoneticiDto>> GetViewOrgBirimYoneticiAsync()
    {
        var query = "SELECT * FROM View_ORG_BirimYonetici";
        using var connection = new SqlConnection(_connectionString);
        var data = await connection.QueryAsync<ViewOrgBirimYoneticiDto>(query);
        return data.ToList();
    }

    private async Task<List<ViewOrgKisiAgacDto>> GetViewOrgKisiAgacAsync()
    {
        var query = "SELECT * FROM View_ORG_KisiAgac WHERE AKTIF = 1";
        using var connection = new SqlConnection(_connectionString);
        var data = await connection.QueryAsync<ViewOrgKisiAgacDto>(query);
        return data.ToList();
    }

    private async Task<List<UnitHierarchyOverrideDto>> GetUnitHierarchyOverridesAsync()
    {
        var query = "SELECT * FROM Org_UstBirimBakim WHERE Aktif = 1";
        using var connection = new SqlConnection(_connectionString);
        var data = await connection.QueryAsync<UnitHierarchyOverrideDto>(query);
        return data.ToList();
    }

    private async Task<List<string>> GetHiddenKeywordsAsync()
    {
        try {
            var query = "SELECT DepartmentName FROM HiddenDepartments";
            using var connection = new SqlConnection(_connectionString);
            var data = await connection.QueryAsync<string>(query);
            return data.ToList();
        } catch {
            return new List<string>();
        }
    }

    private string FormatPositionName(string positionName)
    {
        if (string.IsNullOrWhiteSpace(positionName)) return "Ekip \u00DCyeleri";
        string pos = positionName.Trim();
        if (pos.IndexOf("Huzur", StringComparison.OrdinalIgnoreCase) >= 0 ||
            pos.IndexOf("Y\u00F6netim Kurulu", StringComparison.OrdinalIgnoreCase) >= 0 ||
            pos.StartsWith("YK", StringComparison.OrdinalIgnoreCase))
        {
            return "Y\u00F6netim Kurulu \u00DCyesi";
        }
        return pos;
    }

    private int GetPositionPriority(string positionName)
    {
        if (string.IsNullOrWhiteSpace(positionName)) return 99;
        string lower = positionName.ToLowerInvariant();
        if (lower.Contains("y\u00F6netim kurulu") || lower.Contains("yk")) return 1;
        if (lower.Contains("genel ba\u015Fkan")) return 2;
        if (lower.Contains("genel m\u00FCd\u00FCr")) return 3;
        if (lower.Contains("direkt\u00F6r")) return 4;
        if (lower.Contains("m\u00FCd\u00FCr")) return 5;
        if (lower.Contains("\u015Fef")) return 6;
        if (lower.Contains("uzman")) return 7;
        return 50;
    }

    private Dictionary<string, string> _sicilNoMapping = new();

    private List<HROrganizationDto> FilterAndDeduplicate(List<HROrganizationDto> rawEmployees, List<string> hiddenKeywords)
    {
        _sicilNoMapping.Clear();

        var filtered = rawEmployees.Where(e => !hiddenKeywords.Any(hd => 
            (e.DEPARTMENTNAME != null && e.DEPARTMENTNAME.Contains(hd, StringComparison.OrdinalIgnoreCase)) ||
            (e.POSITIONNAME != null && e.POSITIONNAME.Contains(hd, StringComparison.OrdinalIgnoreCase)) ||
            (e.PROFESSION != null && e.PROFESSION.Contains(hd, StringComparison.OrdinalIgnoreCase))
        )).ToList();

        foreach (var e in filtered)
        {
            if (!string.IsNullOrWhiteSpace(e.ENAME))
            {
                e.ENAME = e.ENAME.Replace(" İNŞAAT", "")
                                 .Replace(" PALM", "")
                                 .Replace(" ÖZKA HUZUR", "")
                                 .Replace(" ÖZKA", "")
                                 .Trim();
            }
        }

        filtered = filtered.Where(e => FormatPositionName(e.POSITIONNAME) != "Ekip \u00DCyeleri").ToList();
        
        var finalRecords = new List<HROrganizationDto>();
        var grouped = filtered.GroupBy(e => e.ENAME);
        
        foreach (var g in grouped)
        {
            finalRecords.AddRange(g);
        }
        
        return finalRecords;
    }

    public async Task<List<OrgNodeDto>> GetProcessedOrganizationChartAsync()
    {
        var orgAgac = await GetViewOrgAgacAsync();
        var birimYoneticileri = await GetViewOrgBirimYoneticiAsync();
        var kisiAgac = await GetViewOrgKisiAgacAsync();
        var hiddenKeywords = await GetHiddenKeywordsAsync();
        
        kisiAgac = kisiAgac.Where(k => !hiddenKeywords.Any(hd => k.POZISYONADI != null && k.POZISYONADI.Contains(hd, StringComparison.OrdinalIgnoreCase))).ToList();

        try {
            var overrides = await GetUnitHierarchyOverridesAsync();
            foreach (var over in overrides)
            {
                if (over.BirimId <= 0 || over.YeniUstBirimId <= 0) continue;
                var target = orgAgac.FirstOrDefault(o => o.BirimId == over.BirimId.ToString());
                if (target != null)
                {
                    target.UstBirimId = over.YeniUstBirimId.ToString();
                }
            }
        } catch { } 

        var tree = BuildPositionTree(orgAgac, birimYoneticileri, kisiAgac, hiddenKeywords);
        
        PruneEmptyNodes(tree);

        return tree;
    }

    public async Task<List<FinalEmployeeDto>> GetFlatEmployeeListAsync()
    {
        var rawEmployees = await GetRawOrganizationAsync();
        var hiddenKeywords = await GetHiddenKeywordsAsync();
        var allEmployees = FilterAndDeduplicate(rawEmployees, hiddenKeywords);
        
        var finalList = allEmployees.Select(emp => new FinalEmployeeDto
        {
            SicilNo = emp.SICILNO,
            NameSurname = emp.ENAME,
            Email = $"{emp.USERID}@kanik.com",
            CompanyId = emp.COMPANY ?? "",
            CompanyName = emp.COMPANYNAME ?? "",
            DepartmentId = emp.DEPARTMENT ?? "",
            DepartmentName = emp.DEPARTMENTNAME ?? "",
            UnitId = emp.DEPARTMENT ?? "",
            UnitName = emp.UNITNAME ?? "",
            PositionName = FormatPositionName(emp.POSITIONNAME),
            ManagerSicilNo = emp.MANAGERSICILNO ?? emp.MANAGERUSERID ?? "",
            Manager = ""
        }).OrderBy(x => x.NameSurname).ToList();
        
        return finalList;
    }

    private string NormalizeDepartmentName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        return name.ToLowerInvariant()
                   .Replace(" ", "")
                   .Replace("\u0131", "i")
                   .Replace("\u015F", "s")
                   .Replace("\u011F", "g")
                   .Replace("\u00FC", "u")
                   .Replace("\u00F6", "o")
                   .Replace("\u00E7", "c");
    }

    private string NormalizePersonName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        return string.Join(" ", name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
    }

    private void PruneEmptyNodes(List<OrgNodeDto> nodes)
    {
        for (int i = nodes.Count - 1; i >= 0; i--)
        {
            var node = nodes[i];
            
            if (node.Children != null && node.Children.Count > 0)
            {
                PruneEmptyNodes(node.Children);
            }
            
            bool hasEmployees = node.Employees != null && node.Employees.Count > 0;
            bool hasChildren = node.Children != null && node.Children.Count > 0;
            
            if (!hasEmployees && !hasChildren)
            {
                if (node.Id != "50000000") 
                {
                    nodes.RemoveAt(i);
                }
            }
        }
    }

    private List<OrgNodeDto> BuildPositionTree(
        List<ViewOrgAgacDto> orgAgac, 
        List<ViewOrgBirimYoneticiDto> birimYoneticileri, 
        List<ViewOrgKisiAgacDto> kisiAgac,
        List<string> hiddenKeywords)
    {
        var nodeDictionary = new Dictionary<string, OrgNodeDto>();
        var rootNodes = new List<OrgNodeDto>();

        foreach (var e in kisiAgac)
        {
            if (!string.IsNullOrWhiteSpace(e.ENAME))
            {
                e.ENAME = e.ENAME.Replace(" \u0130N\u015EAAT", "")
                                 .Replace(" PALM", "")
                                 .Replace(" \u00D6ZKA HUZUR", "")
                                 .Replace(" \u00D6ZKA", "")
                                 .Trim();
            }
        }

        string ultimateRootId = "50000000"; 
        var ultimateRootNode = new OrgNodeDto
        {
            Id = ultimateRootId,
            Name = "Kan\u0131k \u015Eirketler Grubu",
            Type = NodeType.Department, 
            Company = "Kan\u0131k \u015Eirketler Grubu",
            Department = "Y\u00F6netim",
            Unit = "" 
        };
        nodeDictionary[ultimateRootId] = ultimateRootNode;
        rootNodes.Add(ultimateRootNode);

        var validOrgAgac = orgAgac.Where(o => o.BirimId != ultimateRootId && !hiddenKeywords.Any(hd => o.Ad != null && o.Ad.Contains(hd, StringComparison.OrdinalIgnoreCase))).ToList();

        foreach (var org in validOrgAgac)
        {
            if (string.IsNullOrWhiteSpace(org.BirimId)) continue;
            
            var node = new OrgNodeDto
            {
                Id = org.BirimId,
                Name = org.Ad,
                Type = NodeType.Department,
                Company = org.Tip == "\u015Eirket" ? org.Ad : "", 
                Department = org.Tip == "Departman" ? org.Ad : "",
                Unit = org.Tip == "Birim" ? org.Ad : ""
            };
            nodeDictionary[org.BirimId] = node;
        }

        var ykBoxId = "YK_ROOT";
        var ykNode = new OrgNodeDto
        {
            Id = ykBoxId,
            Name = "Y\u00F6netim Kurulu",
            Type = NodeType.Position,
            Company = "",
            Department = "",
            Unit = ""
        };
        nodeDictionary[ykBoxId] = ykNode;
        ultimateRootNode.Children.Add(ykNode);


        foreach (var org in validOrgAgac)
        {
            if (string.IsNullOrWhiteSpace(org.BirimId)) continue;
            if (nodeDictionary.TryGetValue(org.BirimId, out var node))
            {
                if (string.IsNullOrWhiteSpace(org.UstBirimId) || org.UstBirimId == ultimateRootId || !nodeDictionary.ContainsKey(org.UstBirimId))
                {
                    node.ParentId = ykBoxId;
                    ykNode.Children.Add(node);
                }
                else
                {
                    var parentNode = nodeDictionary[org.UstBirimId];
                    node.ParentId = parentNode.Id;
                    parentNode.Children.Add(node);
                }
            }
        }

        var userToBoxId = new Dictionary<string, string>();

        foreach (var yonetici in birimYoneticileri)
        {
            if (string.IsNullOrWhiteSpace(yonetici.BirimId) || string.IsNullOrWhiteSpace(yonetici.YoneticiSicilno)) continue;

            var empData = kisiAgac.FirstOrDefault(k => k.SICILNO == yonetici.YoneticiSicilno);
            string position = empData != null && !string.IsNullOrWhiteSpace(empData.POZISYONADI) ? FormatPositionName(empData.POZISYONADI) : "Y\u00F6netici";
            string yoneticiAdi = yonetici.YoneticiAdi ?? "";
            if (empData != null && !string.IsNullOrWhiteSpace(empData.ENAME))
            {
                yoneticiAdi = empData.ENAME;
            }

            if (position == "Y\u00F6netim Kurulu \u00DCyesi" || position == "Genel Ba\u015Fkan")
            {
                if (!ykNode.Employees.Any(e => e.SicilNo == yonetici.YoneticiSicilno))
                {
                    ykNode.Employees.Add(new EmployeeSummaryDto
                    {
                        SicilNo = yonetici.YoneticiSicilno,
                        NameSurname = yoneticiAdi,
                        Email = position
                    });
                }
                userToBoxId[yonetici.YoneticiSicilno] = ykBoxId;
                continue;
            }

            if (nodeDictionary.TryGetValue(yonetici.BirimId, out var node))
            {
                if (!node.Employees.Any(e => e.SicilNo == yonetici.YoneticiSicilno))
                {
                    node.Employees.Add(new EmployeeSummaryDto
                    {
                        SicilNo = yonetici.YoneticiSicilno,
                        NameSurname = yoneticiAdi,
                        Email = position
                    });
                }
                userToBoxId[yonetici.YoneticiSicilno] = yonetici.BirimId;
            }
        }

        var createdPositionBoxes = new List<OrgNodeDto>();

        foreach (var emp in kisiAgac)
        {
            if (string.IsNullOrWhiteSpace(emp.SICILNO)) continue;

            string position = FormatPositionName(emp.POZISYONADI);
            if (position == "Ekip \u00DCyeleri") continue;

            if (position == "Y\u00F6netim Kurulu \u00DCyesi" || position == "Genel Ba\u015Fkan")
            {
                if (!userToBoxId.ContainsKey(emp.SICILNO))
                {
                    if (!ykNode.Employees.Any(e => NormalizePersonName(e.NameSurname) == NormalizePersonName(emp.ENAME)))
                    {
                        ykNode.Employees.Add(new EmployeeSummaryDto
                        {
                            SicilNo = emp.SICILNO,
                            NameSurname = emp.ENAME,
                            Email = position
                        });
                    }
                    userToBoxId[emp.SICILNO] = ykBoxId;
                }
                continue;
            }

            if (userToBoxId.ContainsKey(emp.SICILNO)) continue; 

            string managerKey = string.IsNullOrWhiteSpace(emp.MANAGERSICILNO) ? "NOMANAGER" : emp.MANAGERSICILNO;
            string boxId = $"{emp.BIRIMID}_{position}_{managerKey}";

            if (!nodeDictionary.ContainsKey(boxId))
            {
                var node = new OrgNodeDto
                {
                    Id = boxId,
                    Name = position,
                    Type = NodeType.Position,
                    Company = "",
                    Department = "",
                    Unit = ""
                };
                nodeDictionary[boxId] = node;
                createdPositionBoxes.Add(node);
            }

            var targetNode = nodeDictionary[boxId];
            if (!targetNode.Employees.Any(e => NormalizePersonName(e.NameSurname) == NormalizePersonName(emp.ENAME)))
            {
                targetNode.Employees.Add(new EmployeeSummaryDto
                {
                    SicilNo = emp.SICILNO,
                    NameSurname = emp.ENAME,
                    Email = position
                });
            }
            
            userToBoxId[emp.SICILNO] = boxId;
        }

        foreach (var posNode in createdPositionBoxes)
        {
            var representativeEmp = posNode.Employees.First();
            var empData = kisiAgac.FirstOrDefault(e => e.SICILNO == representativeEmp.SicilNo);

            if (empData == null) continue;

            string targetParentId = empData.BIRIMID ?? ultimateRootId;

            if (!string.IsNullOrWhiteSpace(empData.MANAGERSICILNO) && userToBoxId.TryGetValue(empData.MANAGERSICILNO, out var managerBoxId))
            {
                if (managerBoxId != posNode.Id && managerBoxId != ykBoxId)
                {
                    targetParentId = managerBoxId;
                }
            }

            if (!string.IsNullOrWhiteSpace(targetParentId) && nodeDictionary.TryGetValue(targetParentId, out var parentNode))
            {
                posNode.ParentId = parentNode.Id;
                parentNode.Children.Add(posNode);
            }
            else
            {
                posNode.ParentId = ultimateRootId;
                ultimateRootNode.Children.Add(posNode);
            }
        }

        return rootNodes;
    }

    public async Task<List<HierarchyResultDto>> SearchEmployeeHierarchyAsync(string query)
    {
        var allEmployees = await GetFlatEmployeeListAsync();
        
        var targetEmployees = allEmployees.Where(e => 
            e.NameSurname.Contains(query, StringComparison.OrdinalIgnoreCase) || 
            e.SicilNo.Equals(query, StringComparison.OrdinalIgnoreCase)).ToList();
            
        if (!targetEmployees.Any())
            return new List<HierarchyResultDto>();
            
        var results = new List<HierarchyResultDto>();
        
        foreach (var target in targetEmployees)
        {
            var hierarchy = new HierarchyResultDto
            {
                DepartmentName = target.DepartmentName,
                TargetEmployee = target,
                Managers = new List<FinalEmployeeDto>(),
                Subordinates = new List<FinalEmployeeDto>()
            };
            
            target.Relation = "Aranan Kişi";

            // Find subordinates (1 level down)
            var subordinates = allEmployees.Where(e => e.ManagerSicilNo == target.SicilNo).ToList();
            foreach(var sub in subordinates) {
                // clone to avoid modifying shared references
                var subClone = new FinalEmployeeDto {
                    SicilNo = sub.SicilNo, NameSurname = sub.NameSurname, Email = sub.Email,
                    CompanyId = sub.CompanyId, CompanyName = sub.CompanyName,
                    DepartmentId = sub.DepartmentId, DepartmentName = sub.DepartmentName,
                    UnitId = sub.UnitId, UnitName = sub.UnitName,
                    PositionName = sub.PositionName, Profession = sub.Profession,
                    ManagerSicilNo = sub.ManagerSicilNo, Manager = sub.Manager,
                    Relation = "Alt Çalışanı"
                };
                hierarchy.Subordinates.Add(subClone);
            }
            
            // Find managers (2 levels up)
            if (!string.IsNullOrWhiteSpace(target.ManagerSicilNo))
            {
                var manager1 = allEmployees.FirstOrDefault(e => e.SicilNo == target.ManagerSicilNo);
                if (manager1 != null)
                {
                    var m1Clone = new FinalEmployeeDto {
                        SicilNo = manager1.SicilNo, NameSurname = manager1.NameSurname, Email = manager1.Email,
                        CompanyId = manager1.CompanyId, CompanyName = manager1.CompanyName,
                        DepartmentId = manager1.DepartmentId, DepartmentName = manager1.DepartmentName,
                        UnitId = manager1.UnitId, UnitName = manager1.UnitName,
                        PositionName = manager1.PositionName, Profession = manager1.Profession,
                        ManagerSicilNo = manager1.ManagerSicilNo, Manager = manager1.Manager,
                        Relation = "1. Yöneticisi"
                    };
                    hierarchy.Managers.Add(m1Clone);
                    
                    if (!string.IsNullOrWhiteSpace(manager1.ManagerSicilNo))
                    {
                        var manager2 = allEmployees.FirstOrDefault(e => e.SicilNo == manager1.ManagerSicilNo);
                        if (manager2 != null)
                        {
                            var m2Clone = new FinalEmployeeDto {
                                SicilNo = manager2.SicilNo, NameSurname = manager2.NameSurname, Email = manager2.Email,
                                CompanyId = manager2.CompanyId, CompanyName = manager2.CompanyName,
                                DepartmentId = manager2.DepartmentId, DepartmentName = manager2.DepartmentName,
                                UnitId = manager2.UnitId, UnitName = manager2.UnitName,
                                PositionName = manager2.PositionName, Profession = manager2.Profession,
                                ManagerSicilNo = manager2.ManagerSicilNo, Manager = manager2.Manager,
                                Relation = "2. Yöneticisi"
                            };
                            // Insert at beginning so order is TopManager -> DirectManager
                            hierarchy.Managers.Insert(0, m2Clone); 
                        }
                    }
                }
            }
            results.Add(hierarchy);
        }
        
        return results;
    }
}



    
