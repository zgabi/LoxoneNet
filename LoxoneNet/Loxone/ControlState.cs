namespace LoxoneNet.Loxone;

internal record ControlState
{
    public Guid Id { get; }

    public Control Control { get; }
    
    public string Name { get; }
    
    public ControlState(Guid id, Control control, string name)
    {
        Id = id;
        Control = control;
        Name = name;
    }
}