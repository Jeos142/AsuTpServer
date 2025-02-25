using System;
using System.Collections.Generic;

//Модель интерфейса
public class Interface
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime EditingDate { get; set; } = DateTime.Now;

    public List<Device> Devices { get; set; } = new();
}
