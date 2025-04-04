namespace LoxoneNet.SmartHome;

class SyncResponsePayload
{
    public string agentUserId { get; set; }
 
    public string errorCode { get; set; }
    
    public string debugString { get; set; }
    
    public Device[] devices { get; set; }
}