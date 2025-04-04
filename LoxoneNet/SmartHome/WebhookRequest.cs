namespace LoxoneNet.SmartHome;

class WebhookRequest
{
    public string requestId { get; set; }
     
    public Input[] inputs { get; set; }
}