namespace LoxoneNet.Loxone;

class MessageCenter
{
    public string name { get; set; }

    public Guid uuidAction { get; set; }

    public MessageCenterStates states { get; set; }
}