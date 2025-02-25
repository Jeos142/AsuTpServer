using System;
using System.Collections.Generic;

//Модель регистра
public class Register
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime EditingDate { get; set; } = DateTime.Now;

    public List<RegisterValue> RegisterValues { get; set; } = new();
}
