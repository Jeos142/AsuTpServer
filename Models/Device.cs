using System;
using System.Collections.Generic;
using Microsoft.Win32;

//Модель устройства
public class Device
{
    public int Id { get; set; }
    public int InterfaceId { get; set; }
    public Interface Interface { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public DateTime EditingDate { get; set; } = DateTime.Now;

    public string FigureType { get; set; } = "circle";
    public int Size { get; set; } = 50;
    public int PosX { get; set; } = 0;
    public int PosY { get; set; } = 0;
    public string Color { get; set; } = "#000000";

    public List<Register> Registers { get; set; } = new();
}
