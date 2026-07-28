namespace OrgSchema.Api.Models;

public enum NodeType { Company, Department, Unit, Position }

public class OrgNodeDto
{
    public string Id { get; set; } = string.Empty;
    public string ParentId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public NodeType Type { get; set; }
    
    // Yalnızca Type == Position olduğunda dolu olacak
    public List<EmployeeSummaryDto> Employees { get; set; } = new();
    
    public List<OrgNodeDto> Children { get; set; } = new();
    public bool IsExpanded { get; set; } = true;
}

public class EmployeeSummaryDto
{
    public string SicilNo { get; set; } = string.Empty;
    public string NameSurname { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

// Tüm personelin düz (flat) listesi (Kime Danışabilirim ekranı için)
public class FinalEmployeeDto
{
    public string SicilNo { get; set; } = string.Empty;
    public string NameSurname { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string DepartmentId { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string UnitId { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
    public string PositionName { get; set; } = string.Empty;
    public string ManagerSicilNo { get; set; } = string.Empty;
    public string Manager { get; set; } = string.Empty;
}

public class HROrganizationDto
{
    public string SICILNO { get; set; } = string.Empty;
    public string ENAME { get; set; } = string.Empty;
    public string USERID { get; set; } = string.Empty;
    public string FIRSTNAME { get; set; } = string.Empty;
    public string LASTNAME { get; set; } = string.Empty;
    public string DEPARTMENT { get; set; } = string.Empty;
    public string DEPARTMENTNAME { get; set; } = string.Empty;
    public string PROFESSION { get; set; } = string.Empty;
    public string POSITIONNAME { get; set; } = string.Empty;
    public string MANAGERUSERID { get; set; } = string.Empty;
    public string COMPANY { get; set; } = string.Empty;
    public string COMPANYNAME { get; set; } = string.Empty;
}
