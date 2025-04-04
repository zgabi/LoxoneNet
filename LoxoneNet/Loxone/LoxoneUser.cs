namespace LoxoneNet.Loxone;

class LoxoneUser
{
    public string name { get; set; }

    public Guid uuid { get; set; }
    
    public bool isAdmin { get; set; }
    
    public bool changePassword { get; set; }
    
    public uint userRights { get; set; }
}