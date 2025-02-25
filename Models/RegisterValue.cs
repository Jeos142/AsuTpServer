using System;

//Модель значения регистра
public class RegisterValue
{
    public int Id { get; set; }
    public int RegisterId { get; set; }
    public Register Register { get; set; } = null!;
    public double Value { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
