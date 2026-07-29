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

    public OrganizationService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new ArgumentNullException("Connection string is missing.");
    }

    public async Task<List<HROrganizationDto>> GetRawOrganizationAsync()
    {
        var query = @"
            SELECT 
                COALESCE(o.SICILNO, h.SICILNO) as SICILNO,
                COALESCE(o.ENAME, h.ENAME) as ENAME,
                COALESCE(o.UserId, h.USERID) as USERID,
                COALESCE(o.FIRSTNAME, h.FIRSTNAME) as FIRSTNAME,
                COALESCE(o.LASTNAME, h.LASTNAME) as LASTNAME,
                COALESCE(o.DEPARTMENT, h.DEPARTMENT) as DEPARTMENT,
                COALESCE(o.DEPARTMENTNAME, h.DEPARTMENTNAME) as DEPARTMENTNAME,
                COALESCE(o.PROFESSION, h.PROFESSION) as PROFESSION,
                COALESCE(o.POSITIONNAME, h.POSITIONNAME) as POSITIONNAME,
                COALESCE(o.MANAGERUSERID, h.MANAGERUSERID) as MANAGERUSERID,
                o.MANAGERSICILNO as MANAGERSICILNO,
                COALESCE(o.COMPANY, h.COMPANY) as COMPANY,
                COALESCE(o.COMPANYNAME, h.COMPANYNAME) as COMPANYNAME,
                ISNULL(o.IsHidden, 0) as IsHidden,
                ISNULL(o.SortOrder, 999) as SortOrder
            FROM HROrganizationTable h
            FULL OUTER JOIN HierarchyOverrides o ON h.USERID = o.UserId
            WHERE ISNULL(o.IsHidden, 0) = 0
        ";

        using var connection = new SqlConnection(_connectionString);
        var data = await connection.QueryAsync<HROrganizationDto>(query);
        return data.ToList();
    }

    public async Task<List<OrgNodeDto>> GetProcessedOrganizationChartAsync()
    {
        var allEmployees = await GetRawOrganizationAsync();
        return BuildPositionTree(allEmployees);
    }

    private string FormatPositionName(string positionName)
    {
        if (string.IsNullOrWhiteSpace(positionName)) return "Belirtilmemiþ";
        string pos = positionName.Trim();
        if (pos.Contains("Huzur Hakký", StringComparison.OrdinalIgnoreCase))
        {
            return "Yönetim Kurulu Üyesi";
        }
        return pos;
    }

    public async Task<List<FinalEmployeeDto>> GetFlatEmployeeListAsync()
    {
        var allEmployees = await GetRawOrganizationAsync();
        
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
            UnitName = emp.DEPARTMENTNAME ?? "",
            PositionName = FormatPositionName(emp.POSITIONNAME),
            ManagerSicilNo = emp.MANAGERSICILNO ?? emp.MANAGERUSERID ?? "",
            Manager = ""
        }).OrderBy(x => x.NameSurname).ToList();
        
        return finalList;
    }

    private List<OrgNodeDto> BuildPositionTree(List<HROrganizationDto> allEmployees)
    {
        var nodeDictionary = new Dictionary<string, OrgNodeDto>();
        var rootNodes = new List<OrgNodeDto>();
        
        var userToBoxIds = new Dictionary<string, List<string>>();
        var userIdToSicilNo = new Dictionary<string, string>();
        foreach(var e in allEmployees) {
            if (!string.IsNullOrWhiteSpace(e.USERID) && !string.IsNullOrWhiteSpace(e.SICILNO)) {
                userIdToSicilNo[e.USERID] = e.SICILNO;
            }
        }

        // 1. Kutularý oluþtur
        foreach (var emp in allEmployees)
        {
            if (string.IsNullOrWhiteSpace(emp.SICILNO)) continue; 

            string resolvedManagerSicilNo = "ROOT";
            if (!string.IsNullOrWhiteSpace(emp.MANAGERSICILNO)) 
            {
                resolvedManagerSicilNo = emp.MANAGERSICILNO; 
            } 
            else if (!string.IsNullOrWhiteSpace(emp.MANAGERUSERID) && emp.MANAGERUSERID != emp.USERID) 
            {
                if (userIdToSicilNo.TryGetValue(emp.MANAGERUSERID, out var mappedSicilNo)) 
                {
                    resolvedManagerSicilNo = mappedSicilNo;
                }
                else
                {
                    resolvedManagerSicilNo = emp.MANAGERUSERID; 
                }
            }
            
            string position = FormatPositionName(emp.POSITIONNAME);
            string boxId = $"{resolvedManagerSicilNo}_{position}";
            
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
                    Type = NodeType.Position 
                };
                nodeDictionary[boxId] = node;
            }

            if (!nodeDictionary[boxId].Employees.Any(e => e.SicilNo == emp.SICILNO))
            {
                nodeDictionary[boxId].Employees.Add(new EmployeeSummaryDto
                {
                    SicilNo = emp.SICILNO,
                    NameSurname = emp.ENAME,
                    Email = position
                });
            }
        }

        // 2. Parent-Child iliþkisi kur
        foreach (var kvp in nodeDictionary)
        {
            var node = kvp.Value;
            var representativeEmp = node.Employees.First();
            var empData = allEmployees.First(e => e.SICILNO == representativeEmp.SicilNo && FormatPositionName(e.POSITIONNAME) == node.Name);
            
            string resolvedManagerSicilNo = "ROOT";
            if (!string.IsNullOrWhiteSpace(empData.MANAGERSICILNO)) 
            {
                resolvedManagerSicilNo = empData.MANAGERSICILNO;
            } 
            else if (!string.IsNullOrWhiteSpace(empData.MANAGERUSERID) && empData.MANAGERUSERID != empData.USERID) 
            {
                if (userIdToSicilNo.TryGetValue(empData.MANAGERUSERID, out var mappedSicilNo)) 
                {
                    resolvedManagerSicilNo = mappedSicilNo;
                }
                else
                {
                    resolvedManagerSicilNo = empData.MANAGERUSERID; 
                }
            }

            if (resolvedManagerSicilNo == "ROOT" || !userToBoxIds.ContainsKey(resolvedManagerSicilNo))
            {
                rootNodes.Add(node);
            }
            else
            {
                var sortedBoxes = userToBoxIds[resolvedManagerSicilNo]
                    .OrderBy(b => b.Contains("Yönetim Kurulu") ? 1 : 0)
                    .ToList();
                    
                string parentBoxId = sortedBoxes.First();
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
