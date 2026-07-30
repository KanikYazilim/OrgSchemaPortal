namespace OrgSchema.Api.Models;

public class ViewOrgAgacDto
{
    public string? BirimId { get; set; }
    public string? UstBirimId { get; set; }
    public string? Ad { get; set; }
    public string? HamAd { get; set; }
    public string? Seviye { get; set; }
    public string? Yol { get; set; }
    public string? Tip { get; set; }
}

public class ViewOrgBirimYoneticiDto
{
    public string? BirimId { get; set; }
    public string? YoneticiSicilno { get; set; }
    public string? YoneticiAdi { get; set; }
}

public class ViewOrgKisiAgacDto
{
    public string? SICILNO { get; set; }
    public string? ENAME { get; set; }
    public string? KMAIL { get; set; }
    public string? POZISYONADI { get; set; }
    public string? MANAGERSICILNO { get; set; }
    public string? BIRIMID { get; set; }
    public bool AKTIF { get; set; }
}
