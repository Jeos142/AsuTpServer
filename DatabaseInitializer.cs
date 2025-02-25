using System;
using System.Linq;
using AsuTpServer.Data;
using Microsoft.EntityFrameworkCore;

//Инициализация БД
public static class DatabaseInitializer
{
    public static void Initialize()
    {
        //  Вывод пути к базе
        string dbPath = Path.Combine(Directory.GetCurrentDirectory(), "devices.db");
        Console.WriteLine($" Путь к базе данных: {dbPath}");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=devices.db")
            .Options;

        using var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        if (!context.Interfaces.Any())
        {
            var interfaces = new[]
            {
                new Interface { Name = "Modbus TCP", Description = "Modbus TCP interface" },
                new Interface { Name = "OPC UA", Description = "OPC Unified Architecture interface" },
                new Interface { Name = "MQTT", Description = "MQTT broker interface" }
            };

            context.Interfaces.AddRange(interfaces);
            context.SaveChanges();

            foreach (var iface in context.Interfaces.ToList()) //  Загружаем ID после сохранения
            {
                var devices = new[]
                {
                    new Device { Name = $"Device 1", Description = "Первое устройство", InterfaceId = iface.Id },
                    new Device { Name = $"Device 2", Description = "Второе устройство", InterfaceId = iface.Id },
                    new Device { Name = $"Device 3", Description = "Третье устройство", InterfaceId = iface.Id }
                };

                context.Devices.AddRange(devices);
                context.SaveChanges();

                foreach (var device in context.Devices.Where(d => d.InterfaceId == iface.Id).ToList())
                {
                    var registers = new[]
                    {
                        new Register { Name = $"{device.Name} - Регистр 1", Description = "Температура", DeviceId = device.Id },
                        new Register { Name = $"{device.Name} - Регистр 2", Description = "Давление", DeviceId = device.Id },
                        new Register { Name = $"{device.Name} - Регистр 3", Description = "Скорость", DeviceId = device.Id }
                    };

                    context.Registers.AddRange(registers);
                }
            }

            context.SaveChanges();
        }
    }
}
