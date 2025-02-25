using System;

//Модель логов
public class Log
{

    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "Info";
}
