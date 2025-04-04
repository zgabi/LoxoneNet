using Microsoft.Extensions.Logging;

namespace LoxoneNet;

public sealed class Settings
{
    public required LogLevel LogLevel { get; set; }
    
    public required string Hostname { get; set; }

    public required string GoogleAccessToken { get; set; }
    
    public required string GoogleRefreshToken { get; set; }
    
    public required string LetsEncryptEmail { get; set; }

    public required string CertCountry { get; set; }
    
    public required string CertState { get; set; }
    
    public required string CertLocality { get; set; }
    
    public required string CertOrganization { get; set; }
    
    public required string CertOrganizationUnit { get; set; }

    public required string? DsmrReaderAddress { get; set; }
    
    public required ushort DsmrReaderPort { get; set; }

    public required string? GrowattServerHost { get; set; }
    
    public required string GrowattUserName { get; set; }
    
    public required string GrowattPassword { get; set; }

    public required string LoxoneUrl { get; set; }
    
    public required string LoxoneUserName { get; set; }
    
    public required string LoxonePassword { get; set; }
}