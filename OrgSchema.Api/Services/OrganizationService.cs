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
        var query = "SELECT * FROM View_Org_Agac";
        using var connection = new SqlConnection(_connectionString);
        var data = await connection.QueryAsync<ViewOrgAgacDto>(query);
        return data.ToList();
    }

    private string FormatPositionName(string positionName)
    {
        if (string.IsNullOrWhiteSpace(positionName)) return "Ekip Üyeleri";
        string pos = positionName.Trim();
        if (pos.IndexOf("Huzur", StringComparison.OrdinalIgnoreCase) >= 0 ||
            pos.IndexOf("Yönetim Kurulu", StringComparison.OrdinalIgnoreCase) >= 0 ||
            pos.StartsWith("YK", StringComparison.OrdinalIgnoreCase))
        {
            return "Yönetim Kurulu Üyesi";
        }
        return pos;
    }

    private int GetPositionPriority(string positionName)
    {
        if (string.IsNullOrWhiteSpace(positionName)) return 99;
        string lower = positionName.ToLowerInvariant();
        if (lower.Contains("yönetim kurulu") || lower.Contains("yk")) return 1;
        if (lower.Contains("genel başkan") || lower.Contains("genel baÃ…Å¸kan")) return 2;
        if (lower.Contains("genel müdür")) return 3;
        if (lower.Contains("direktör")) return 4;
        if (lower.Contains("müdür")) return 5;
        if (lower.Contains("şef")) return 6;
        if (lower.Contains("uzman")) return 7;
        return 50;
    }

    private Dictionary<string, string> _sicilNoMapping = new();

    private List<HROrganizationDto> FilterAndDeduplicate(List<HROrganizationDto> rawEmployees)
    {
        var hiddenDepts = _configuration.GetSection("HiddenDepartments").Get<List<string>>() ?? new List<string>();
        
        _sicilNoMapping.Clear();

        var filtered = rawEmployees.Where(e => !hiddenDepts.Any(hd => 
            (e.DEPARTMENTNAME != null && e.DEPARTMENTNAME.Contains(hd, StringComparison.OrdinalIgnoreCase))
        )).ToList();

        // 2. İsim temizliği (İNŞAAT vb. ekleri kaldır - yine de isimleri temizleyelim ki görsel olarak güzel dursun)
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

        // 3. Sadece Ekip Üyelerini filtrele (Kullanıcı talebi: Müşteriler görünmesin)
        filtered = filtered.Where(e => FormatPositionName(e.POSITIONNAME) != "Ekip Üyeleri").ToList();

        // Daha önceden YK üyeleri için yaptığımız hayalet "Yönetici" mantığı burada geçerli mi?
        // Kullanıcı tüm kartların görünmesini istiyor. 
        // Ancak YK üyeleri tek bir kutuda toplanacağı için (departman id vb. yok sayılıp en tepeye ekleneceği için),
        // o YK üyesine raporlayan bir departman yöneticisi vs. varsa, onun yönetici kutusunu bulabilmesi lazım.
        // Ama eğer YK üyesinin ZATEN bir departman (non-yk) kaydı varsa, onun altında yöneticilik yapacaktır.
        // Yoksa phantom kayıt oluşturmalıyız ki o ağaç oluşabilsin.
        
        var finalRecords = new List<HROrganizationDto>();
        var grouped = filtered.GroupBy(e => e.ENAME);
        
        foreach (var g in grouped)
        {
            finalRecords.AddRange(g); // Add all records without deduplication or phantom managers
        }
        
        return finalRecords;
    }

    public async Task<List<OrgNodeDto>> GetProcessedOrganizationChartAsync()
    {
        var rawEmployees = await GetRawOrganizationAsync();
        var allEmployees = FilterAndDeduplicate(rawEmployees);
        var orgAgac = await GetViewOrgAgacAsync();
        return BuildPositionTree(allEmployees, orgAgac);
    }

    public async Task<List<FinalEmployeeDto>> GetFlatEmployeeListAsync()
    {
        var rawEmployees = await GetRawOrganizationAsync();
        var allEmployees = FilterAndDeduplicate(rawEmployees);
        
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
                   .Replace("ı", "i")
                   .Replace("ş", "s")
                   .Replace("ğ", "g")
                   .Replace("ü", "u")
                   .Replace("ö", "o")
                   .Replace("ç", "c")
                   .Replace("i̇", "i");
    }

    private string NormalizePersonName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        return string.Join(" ", name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
    }

    private List<OrgNodeDto> BuildPositionTree(List<HROrganizationDto> allEmployees, List<ViewOrgAgacDto> orgAgac)
    {
        var nodeDictionary = new Dictionary<string, OrgNodeDto>();
        var rootNodes = new List<OrgNodeDto>();
        var userToBoxIds = new Dictionary<string, List<string>>();
        var userIdToSicilNo = new Dictionary<string, string>();

        string ultimateRootId = "ROOT_KANIK_SIRKETLER_GRUBU";
        var ultimateRootNode = new OrgNodeDto
        {
            Id = ultimateRootId,
            Name = "Kanık Şirketler Grubu",
            Type = NodeType.Department, 
            Company = "Kanık Şirketler Grubu",
            Department = "Yönetim",
            Unit = "" 
        };
        
        nodeDictionary[ultimateRootId] = ultimateRootNode;
        rootNodes.Add(ultimateRootNode);

        // 1. YK Kutusunu oluştur ve Root'a bağla
        string ykBoxId = $"{ultimateRootId}_____Yönetim Kurulu Üyesi";
        var ykNode = new OrgNodeDto
        {
            Id = ykBoxId,
            Name = "Yönetim Kurulu Üyesi",
            Type = NodeType.Position,
            Company = "",
            Department = "",
            Unit = "",
            ParentId = ultimateRootId
        };
        nodeDictionary[ykBoxId] = ykNode;
        ultimateRootNode.Children.Add(ykNode);

        // 2. View_Org_Agac yapısını kur
        var structuralNodes = new Dictionary<string, OrgNodeDto>();
        var departmentMergeMap = new Dictionary<string, string>(); // Kopya -> Ana
        var normalizedNamesByParent = new Dictionary<string, string>(); // UstBirimId_NormalizedName -> BirimId

        // Grup seviyesini atlıyoruz ve "Yönetim Kurulu" vb. isimli kayıtları filtreliyoruz.
        var validOrgAgac = orgAgac.Where(o => o.Tip != "Grup" && !o.Ad.Contains("Yönetim Kurulu", StringComparison.OrdinalIgnoreCase)).ToList();

        // Önce eşleştirme / Merge haritasını oluştur
        foreach (var org in validOrgAgac.OrderBy(o => o.Seviye).ThenBy(o => o.Ad))
        {
            string parentKey = org.UstBirimId ?? "ROOT";
            string normName = NormalizeDepartmentName(org.Ad);
            string dictKey = $"{parentKey}_{normName}";

            if (normalizedNamesByParent.ContainsKey(dictKey))
            {
                // Bu kopya bir departman, daha önce eklenen Ana Departmana yönlendir
                departmentMergeMap[org.BirimId] = normalizedNamesByParent[dictKey];
            }
            else
            {
                // İlk defa görüyoruz, Ana Departman olarak kaydet
                normalizedNamesByParent[dictKey] = org.BirimId;
                
                string structId = $"STRUCT_{org.BirimId}";
                var node = new OrgNodeDto
                {
                    Id = structId,
                    Name = org.Ad,
                    Type = NodeType.Department,
                    Company = org.Tip == "Şirket" ? org.Ad : "", // Şirketleri ayırt etmek için
                    Department = org.Tip == "Departman" ? org.Ad : "",
                    Unit = org.Tip == "Birim" ? org.Ad : ""
                };
                structuralNodes[org.BirimId] = node;
                nodeDictionary[structId] = node;
            }
        }

        // View_Org_Agac ilişkilerini kur (Sadece Ana Departmanlar için)
        foreach (var org in validOrgAgac.Where(o => !departmentMergeMap.ContainsKey(o.BirimId)))
        {
            string structId = $"STRUCT_{org.BirimId}";
            var node = structuralNodes[org.BirimId];
            
            // Eğer üst birim kopyaysa, üst birimin Ana Departmanına bağlan
            string resolvedParentId = org.UstBirimId;
            if (!string.IsNullOrWhiteSpace(resolvedParentId) && departmentMergeMap.ContainsKey(resolvedParentId))
            {
                resolvedParentId = departmentMergeMap[resolvedParentId];
            }

            if (string.IsNullOrWhiteSpace(resolvedParentId) || org.Seviye == "1" || !structuralNodes.ContainsKey(resolvedParentId))
            {
                // Kullanıcı "yk üyeleri->firmalar" dediği için şirketleri YK kutusunun altına bağlıyoruz
                node.ParentId = ykBoxId;
                ykNode.Children.Add(node);
            }
            else
            {
                var parentNode = structuralNodes[resolvedParentId];
                node.ParentId = parentNode.Id;
                parentNode.Children.Add(node);
            }
        }

        // 3. Çalışanların departmanlarını Ana Departmanlara (Merge Map) yönlendir
        foreach (var emp in allEmployees)
        {
            if (!string.IsNullOrWhiteSpace(emp.DEPARTMENT) && departmentMergeMap.ContainsKey(emp.DEPARTMENT))
            {
                emp.DEPARTMENT = departmentMergeMap[emp.DEPARTMENT];
            }
        }

        // 4. Kullanıcı-Sicil eşleşmesi
        foreach (var e in allEmployees)
        {
            if (!string.IsNullOrWhiteSpace(e.USERID) && !string.IsNullOrWhiteSpace(e.SICILNO))
            {
                userIdToSicilNo[e.USERID] = e.SICILNO;
            }
        }

        // 4. Çalışan kutularını oluştur
        foreach (var emp in allEmployees)
        {
            if (string.IsNullOrWhiteSpace(emp.SICILNO)) continue;

            string position = FormatPositionName(emp.POSITIONNAME);
            
            // Eğer YK ise, doğrudan YK kutusuna at ve kendi kutusunu yaratma
            if (position == "Yönetim Kurulu Üyesi" || position == "Genel Başkan")
            {
                if (!userToBoxIds.ContainsKey(emp.SICILNO)) userToBoxIds[emp.SICILNO] = new List<string>();
                if (!userToBoxIds[emp.SICILNO].Contains(ykBoxId)) userToBoxIds[emp.SICILNO].Add(ykBoxId);
                
                if (!ykNode.Employees.Any(e => NormalizePersonName(e.NameSurname) == NormalizePersonName(emp.ENAME)))
                {
                    ykNode.Employees.Add(new EmployeeSummaryDto
                    {
                        SicilNo = emp.SICILNO,
                        NameSurname = emp.ENAME,
                        Email = position
                    });
                }
                continue;
            }

            string resolvedManagerSicilNo = ultimateRootId;
            if (!string.IsNullOrWhiteSpace(emp.MANAGERSICILNO) && emp.MANAGERSICILNO != emp.SICILNO)
            {
                resolvedManagerSicilNo = emp.MANAGERSICILNO;
            }
            else if (!string.IsNullOrWhiteSpace(emp.MANAGERUSERID) && emp.MANAGERUSERID != emp.USERID)
            {
                if (userIdToSicilNo.TryGetValue(emp.MANAGERUSERID, out var mappedSicilNo))
                    resolvedManagerSicilNo = mappedSicilNo;
                else
                    resolvedManagerSicilNo = emp.MANAGERUSERID;
            }
            
            if (_sicilNoMapping.ContainsKey(resolvedManagerSicilNo))
            {
                resolvedManagerSicilNo = _sicilNoMapping[resolvedManagerSicilNo];
            }

            string companyName = string.IsNullOrWhiteSpace(emp.COMPANYNAME) ? "Belirtilmemiş Firma" : emp.COMPANYNAME.Trim();
            string deptName = string.IsNullOrWhiteSpace(emp.DEPARTMENTNAME) ? "Belirtilmemiş Departman" : emp.DEPARTMENTNAME.Trim();
            string unitName = string.IsNullOrWhiteSpace(emp.UNITNAME) ? "Belirtilmemiş Birim" : emp.UNITNAME.Trim();

            string boxId = $"{resolvedManagerSicilNo}_{companyName}_{deptName}_{unitName}_{position}";

            if (!userToBoxIds.ContainsKey(emp.SICILNO))
                userToBoxIds[emp.SICILNO] = new List<string>();

            if (!userToBoxIds[emp.SICILNO].Contains(boxId))
                userToBoxIds[emp.SICILNO].Add(boxId);

            if (!nodeDictionary.ContainsKey(boxId))
            {
                var node = new OrgNodeDto
                {
                    Id = boxId,
                    Name = position,
                    Type = NodeType.Position,
                    Company = companyName,
                    Department = deptName,
                    Unit = unitName
                };
                nodeDictionary[boxId] = node;
            }

            if (!nodeDictionary[boxId].Employees.Any(e => NormalizePersonName(e.NameSurname) == NormalizePersonName(emp.ENAME)))
            {
                nodeDictionary[boxId].Employees.Add(new EmployeeSummaryDto
                {
                    SicilNo = emp.SICILNO,
                    NameSurname = emp.ENAME,
                    Email = position
                });
            }
        }

        // 5. Çalışan kutularını hiyerarşiye (Structural Node veya Manager Node) bağla
        foreach (var kvp in nodeDictionary)
        {
            if (kvp.Key == ultimateRootId || kvp.Key == ykBoxId || kvp.Key.StartsWith("STRUCT_")) continue;

            var node = kvp.Value;
            if (!node.Employees.Any()) continue;

            var representativeEmp = node.Employees.First();
            var empData = allEmployees.First(e => e.SICILNO == representativeEmp.SicilNo && FormatPositionName(e.POSITIONNAME) == node.Name);

            string resolvedManagerSicilNo = ultimateRootId;
            if (!string.IsNullOrWhiteSpace(empData.MANAGERSICILNO) && empData.MANAGERSICILNO != empData.SICILNO)
            {
                resolvedManagerSicilNo = empData.MANAGERSICILNO;
            }
            else if (!string.IsNullOrWhiteSpace(empData.MANAGERUSERID) && empData.MANAGERUSERID != empData.USERID)
            {
                if (userIdToSicilNo.TryGetValue(empData.MANAGERUSERID, out var mappedSicilNo))
                    resolvedManagerSicilNo = mappedSicilNo;
                else
                    resolvedManagerSicilNo = empData.MANAGERUSERID;
            }
            
            if (_sicilNoMapping.ContainsKey(resolvedManagerSicilNo))
            {
                resolvedManagerSicilNo = _sicilNoMapping[resolvedManagerSicilNo];
            }

            // Yöneticiyi bul ve departman kontrolü yap
            bool sameDepartmentAsManager = false;
            string managerBoxId = null;

            if (resolvedManagerSicilNo != ultimateRootId && userToBoxIds.ContainsKey(resolvedManagerSicilNo))
            {
                var managerData = allEmployees.FirstOrDefault(e => e.SICILNO == resolvedManagerSicilNo && FormatPositionName(e.POSITIONNAME) != "Yönetim Kurulu Üyesi");
                if (managerData != null && managerData.DEPARTMENT == empData.DEPARTMENT)
                {
                    sameDepartmentAsManager = true;
                    // Yöneticinin kutusunu bul
                    var sortedBoxes = userToBoxIds[resolvedManagerSicilNo].OrderBy(b => b.Contains("Yönetim Kurulu") ? 1 : 0).ToList();
                    managerBoxId = sortedBoxes.First();
                }
            }

            if (sameDepartmentAsManager && managerBoxId != null && nodeDictionary.TryGetValue(managerBoxId, out var parentNode))
            {
                if (managerBoxId != node.Id)
                {
                    node.ParentId = managerBoxId;
                    parentNode.Children.Add(node);
                }
                else
                {
                    // Döngü oluşmaması için departmana bağla
                    AttachToStructuralNode(node, empData, structuralNodes, ultimateRootNode, ykNode);
                }
            }
            else
            {
                // Yönetici farklı departmanda, yok veya YK üyesi ise -> Kendi departman kutusuna bağla
                AttachToStructuralNode(node, empData, structuralNodes, ultimateRootNode, ykNode);
            }
        }

        return rootNodes;
    }

    private void AttachToStructuralNode(OrgNodeDto node, HROrganizationDto empData, Dictionary<string, OrgNodeDto> structuralNodes, OrgNodeDto ultimateRootNode, OrgNodeDto ykNode)
    {
        OrgNodeDto targetNode = null;
        
        string normDeptName = NormalizeDepartmentName(empData.DEPARTMENTNAME);
        string normUnitName = NormalizeDepartmentName(empData.UNITNAME);
        
        // Önce departman adıyla eşleşen ve bağlı olduğu şirket eşleşen structural node'u bul
        foreach (var structNode in structuralNodes.Values)
        {
            if (structNode.Type == NodeType.Department && structNode.ParentId == "STRUCT_" + empData.DEPARTMENT)
            {
                string normStructName = NormalizeDepartmentName(structNode.Name);
                if (normStructName == normDeptName || normStructName == normUnitName)
                {
                    targetNode = structNode;
                    break;
                }
            }
        }
        
        // Eğer isimden bulamazsak, direkt Company ID'sine (departmana) düşelim
        if (targetNode == null)
        {
            if (!string.IsNullOrWhiteSpace(empData.DEPARTMENT) && structuralNodes.TryGetValue(empData.DEPARTMENT, out var companyNode))
            {
                targetNode = companyNode;
            }
        }
        
        if (targetNode != null)
        {
            node.ParentId = targetNode.Id;
            targetNode.Children.Add(node);
        }
        else
        {
            // Eğer departmanı yapısal listede yoksa, YK'nın altına at
            node.ParentId = ykNode.Id;
            ykNode.Children.Add(node);
        }
    }
}
