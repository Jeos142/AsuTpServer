using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using AsuTpServer.Data;
using Microsoft.EntityFrameworkCore;
using System.Timers;
using AsuTpServer.Services;

public  class TcpServer
{
    private static bool _isRunning = true;

    private System.Timers.Timer dataGenerationTimer;
    private List<RegisterValue> dataBuffer = new List<RegisterValue>();
    private const int bufferThreshold = 200;
    private readonly object lockObject = new object();
    private bool isTimerRunning = false;

    public TcpServer()
    {
        dataGenerationTimer = new System.Timers.Timer(2000); //  Таймер срабатывает каждые 2 секунды
        dataGenerationTimer.Elapsed += GenerateData;
        dataGenerationTimer.AutoReset = true;
    }

    //Генерация данных
    private void GenerateData(object sender, ElapsedEventArgs e)
    {
        lock (lockObject)
        {
            using (var context = new AppDbContext())
            {
                var enabledDeviceIds = context.Devices
                .Where(d => d.IsEnabled)
                .Select(d => d.Id)
                .ToList(); 

                
                var registers = context.Registers
                    .AsEnumerable() 
                    .Where(r => enabledDeviceIds.Contains(r.DeviceId)) 
                    .ToList();

                Random random = new Random();

                foreach (var register in registers)
                {
                    int value = random.Next(0, 1000); //  Генерируем случайное значение
                    dataBuffer.Add(new RegisterValue
                    {
                        RegisterId = register.Id,
                        Value = value,
                        Timestamp = DateTime.Now
                    });
                }

                Console.WriteLine($"[Таймер] Сгенерировано {registers.Count} записей, буфер: {dataBuffer.Count}/200");

                if (dataBuffer.Count >= bufferThreshold)
                {
                    Console.WriteLine($"[Таймер] Достигнут лимит буфера! Сохранение в БД...");

                    context.RegisterValues.AddRange(dataBuffer);
                    context.SaveChanges();
                    dataBuffer.Clear();
                }
            }
        }
    }
    //Включение генерации данных
    public string StartDataGeneration()
    {
        if (isTimerRunning) return "ERROR: Таймер уже запущен";

        lock (lockObject)
        {
            dataGenerationTimer.Start();
            isTimerRunning = true;
            Console.WriteLine("[Таймер] Запущен генератор данных!");
            LogMessage("Генерация данных запущена", "INFO");
        }

        return "SUCCESS: Генерация данных включена";
    }
    //Выключение генерации данных
    public string StopDataGeneration()
    {
        if (!isTimerRunning) return "ERROR: Таймер уже выключен";

        lock (lockObject)
        {
            dataGenerationTimer.Stop();
            isTimerRunning = false;
            Console.WriteLine("[Таймер] Генератор данных выключен!");
        }

        return "SUCCESS: Генерация данных выключена";
    }
    //Получение статуса таймера генерации данных
    public string GetTimerStatus()
    {
        return isTimerRunning ? "ON" : "OFF";
    }

    public  void Start()
    {
        Task.Run(() =>
        {
            TcpListener listener = new TcpListener(IPAddress.Any, 5000);
            listener.Start();
            Console.WriteLine("TCP-сервер запущен на порту 5000.");
            HttpServer httpServer = new HttpServer(this);
            httpServer.Start();
            while (_isRunning)
            {
                TcpClient client = listener.AcceptTcpClient();
                Console.WriteLine("Клиент подключился.");

                //  Передаём `this` (экземпляр сервера) в `HandleClient()`
                Thread clientThread = new Thread(() => HandleClient(client, this));
                clientThread.Start();
            }
        });
    }

    private  void HandleClient(TcpClient client, TcpServer serverInstance)
    {
        try
        {
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[1024];

            while (true) 
            {
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0) break; //  Если клиент закрыл соединение — выходим

                string request = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Console.WriteLine($"Получен запрос: {request}");

                string response = serverInstance.ProcessRequest(request);
                byte[] responseData = Encoding.UTF8.GetBytes(response);
                stream.Write(responseData, 0, responseData.Length);
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Ошибка при обработке клиента: {ex.Message}", "ERROR");
            Console.WriteLine($"Ошибка обработки запроса: {ex.Message}");
        }
        finally
        {
            client.Close();
            Console.WriteLine("Соединение с клиентом закрыто.");
            LogMessage("Клиент отключился", "INFO");
        }
    }

    //Обработка запросов с клиентской стороны
    public  string ProcessRequest(string request)
    {
        using var context = new AppDbContext();
        //  запрос на получение интерфейсов
        if (request == "GET_INTERFACES")
        {
            var interfaces = context.Interfaces.Select(i => i.Name).ToList();

            
            return string.Join(";", interfaces);
        }

        //  запрос на получение устройств
        if (request.StartsWith("GET_DEVICES:"))
        {
            string interfaceName = request.Replace("GET_DEVICES:", "").Trim();

            var devices = context.Devices
                .Where(d => d.Interface.Name == interfaceName)
                .Select(d => $"{d.Name}|{d.FigureType}|{d.Size}|{d.PosX}|{d.PosY}|{d.Color}|{(d.IsEnabled ? "1" : "0")}")
                .ToList();

            return devices.Any() ? string.Join(";", devices) : "NO_DEVICES";
        }


        //  запрос на получение регистров
        if (request.StartsWith("GET_REGISTERS:"))
        {
            string deviceName = request.Replace("GET_REGISTERS:", "").Trim();
            var registers = context.Registers
                .Where(r => r.Device.Name == deviceName)
                .Select(r => new { r.Id, r.Name }) //  Передаём ID и имя
                .Distinct()
                .ToList();

            return registers.Any()
                ? string.Join(";", registers.Select(r => $"{r.Id}|{r.Name}")) //  Формат "ID|Имя"
                : "NO_REGISTERS";
        }

        //  запрос на добавление интерфейса
        if (request.StartsWith("ADD_INTERFACE:"))
        {
            string[] parts = request.Replace("ADD_INTERFACE:", "").Trim().Split('|');
          //  string interfaceName = request.Replace("ADD_INTERFACE:", "").Trim();
            if (context.Interfaces.Any(i => i.Name == parts[0]))
                return "ERROR: Интерфейс уже существует";

            var newInterface = new Interface { Name = parts[0], Description = parts[1] };
            context.Interfaces.Add(newInterface);
            context.SaveChanges();
            LogMessage($"Интерфейс {parts[0]} добавлен", "SUCCESS");
            return "SUCCESS: Интерфейс добавлен";
        }
        //  запрос на добавление устройства
        if (request.StartsWith("ADD_DEVICE:"))
        {
            string[] parts = request.Replace("ADD_DEVICE:", "").Trim().Split('|');
            if (parts.Length < 9) return "ERROR: Неверный формат команды";

            string interfaceName = parts[0];
            string deviceName = parts[1];
            string figureType = parts[2];
            int size = int.Parse(parts[3]);
            int posX = int.Parse(parts[4]);
            int posY = int.Parse(parts[5]);
            string color = parts[6];
            string description= parts[7];
            bool isEnabled = parts[8] == "1";

            var parentInterface = context.Interfaces.FirstOrDefault(i => i.Name == interfaceName);
            if (parentInterface == null) return "ERROR: Интерфейс не найден";

            if (context.Devices.Any(d => d.Name == deviceName))
                return "ERROR: Устройство уже существует";

            var newDevice = new Device
            {
                Name = deviceName,
                Description = description,
                InterfaceId = parentInterface.Id,
                FigureType = figureType,
                Size = size,
                PosX = posX,
                PosY = posY,
                Color = color,
                IsEnabled = isEnabled
            };

            context.Devices.Add(newDevice);
            context.SaveChanges();
            LogMessage($"Устройство {deviceName} добавлено", "SUCCESS");
            return "SUCCESS: Устройство добавлено";
        }

        //  запрос на добавление регистра
        if (request.StartsWith("ADD_REGISTER:"))
        {
            string[] parts = request.Replace("ADD_REGISTER:", "").Trim().Split('|');
            if (parts.Length < 3) return "ERROR: Неверный формат команды";

            string deviceName = parts[0];
            string registerName = parts[1];
            string registerDescription = parts[2];

            var parentDevice = context.Devices.FirstOrDefault(d => d.Name == deviceName);
            if (parentDevice == null) return "ERROR: Устройство не найдено";

            if (context.Registers.Any(r => r.Name == registerName&& parentDevice.Id == r.DeviceId))
                return "ERROR: Регистр с таким названием уже существует";

            var newRegister = new Register { Name = registerName, Description = registerDescription, DeviceId = parentDevice.Id };
            context.Registers.Add(newRegister);
            context.SaveChanges();
            LogMessage($"Регистр {registerName} добавлен", "SUCCESS");
            return "SUCCESS: Регистр добавлен";
        }


        //  запрос на удаление интерфейса
        if (request.StartsWith("DELETE_INTERFACE:"))
        {
            string interfaceName = request.Replace("DELETE_INTERFACE:", "").Trim();

            var selectedInterface = context.Interfaces.FirstOrDefault(i => i.Name == interfaceName);
            if (selectedInterface == null) return "ERROR: Интерфейс не найден";

            context.Interfaces.Remove(selectedInterface);
            context.SaveChanges();
            LogMessage($"Интерфейс {interfaceName} удален", "SUCCESS");
            return "SUCCESS: Интерфейс удалён";
        }
        //  запрос на удаление устройства
        if (request.StartsWith("DELETE_DEVICE:"))
        {
            string deviceName = request.Replace("DELETE_DEVICE:", "").Trim();

            var selectedDevice = context.Devices.FirstOrDefault(d => d.Name == deviceName);
            if (selectedDevice == null) return "ERROR: Устройство не найдено";

            context.Devices.Remove(selectedDevice);
            context.SaveChanges();
            LogMessage($"Устройство {deviceName} удалено", "SUCCESS");
            return "SUCCESS: Устройство удалено";
        }
        //  запрос на удаление регистра
        if (request.StartsWith("DELETE_REGISTER:"))
        {
            string registerName = request.Replace("DELETE_REGISTER:", "").Trim();

            var selectedRegister = context.Registers.FirstOrDefault(r => r.Name == registerName);
            if (selectedRegister == null) return "ERROR: Регистр не найден";

            context.Registers.Remove(selectedRegister);
            context.SaveChanges();
            LogMessage($"Регистр {registerName} удален", "SUCCESS");
            return "SUCCESS: Регистр удалён";
        }
        //  запрос на редактирование интерфейса
        if (request.StartsWith("EDIT_INTERFACE:"))
        {
            string[] parts = request.Replace("EDIT_INTERFACE:", "").Trim().Split('|');
            var item = context.Interfaces.FirstOrDefault(i => i.Name == parts[0]);
            if (item != null) 
            { 
                item.Name = parts[1]; 
                item.Description = parts[2];
                item.EditingDate = DateTime.Now;
            }

            context.SaveChanges();
            LogMessage($"Интерфейс {item.Name} обновлён", "SUCCESS");
            return "SUCCESS: Интерфейс обновлён";
        }
        //  запрос на редактирование устройства
        if (request.StartsWith("EDIT_DEVICE:"))
        {
            string[] parts = request.Replace("EDIT_DEVICE:", "").Trim().Split('|');
            if (parts.Length < 7) return "ERROR: Неверный формат команды";

            string oldName = parts[0];
            string newName = parts[1];
            string figureType = parts[2];
            int size = int.Parse(parts[3]);
            string color = parts[4];
            string description = parts[5];
            bool isEnabled = parts[6]=="1";

            var device = context.Devices.FirstOrDefault(d => d.Name == oldName);
            if (device == null) return "ERROR: Устройство не найдено";

            device.Name = newName;
            device.FigureType = figureType;
            device.Size = size;
            device.Color = color;
            device.Description = description;
            device.IsEnabled = isEnabled;
            device.EditingDate = DateTime.Now;

            context.SaveChanges();
            LogMessage($"Регистр {newName} обновлён", "SUCCESS");
            return "SUCCESS: Устройство обновлено";

        }
        //  запрос на редактирование регистра
        if (request.StartsWith("EDIT_REGISTER:"))
        {
            string[] parts = request.Replace("EDIT_REGISTER:", "").Trim().Split('|');
            if (parts.Length < 3) return "ERROR: Неверный формат команды";

            int id = int.Parse(parts[0]);
            string newName = parts[1];
            string description = parts[2];

            var register = context.Registers.FirstOrDefault(r => r.Id == id);
            if (register == null) return "ERROR: Регистр не найден";

            register.Name = newName;
            register.Description = description;
            register.EditingDate = DateTime.Now;
            context.SaveChanges();
            LogMessage($"Регистр {newName} обновлён", "SUCCESS");
            return "SUCCESS: Регистр обновлён";
        }
        //  запрос на получение информации об устройстве
        if (request.StartsWith("GET_DEVICE_INFO:"))
        {
            string deviceName = request.Replace("GET_DEVICE_INFO:", "").Trim();

            var device = context.Devices.FirstOrDefault(d => d.Name == deviceName);
            if (device == null) return "ERROR: Устройство не найдено";
            int isEnabled = device.IsEnabled ? 1 : 0;

            return $"{device.Name}|{device.FigureType}|{device.Size}|{device.Color}|{device.Description}|{isEnabled}";
        }
        //  запрос на получение информации об интерфейсе
        if (request.StartsWith("GET_INTERFACE_INFO:"))
        {
            string interfaceName = request.Replace("GET_INTERFACE_INFO:", "").Trim();

            var item = context.Interfaces.FirstOrDefault(d => d.Name == interfaceName);
            if (item == null) return "ERROR: Интерфейс не найден";

            return $"{item.Name}|{item.Description}";
        }

        //  запрос на получение информации об регистре
        if (request.StartsWith("GET_REGISTER_INFO:"))
        {
            int registerId = int.Parse(request.Replace("GET_REGISTER_INFO:", "").Trim());


            var item = context.Registers.FirstOrDefault(d => d.Id == registerId);
            if (item == null) return "ERROR: Регистр не найден";

            return $"{item.Name}|{item.Description}";
        }
        // Запросы на работу с генерацией данных
        if (request == "START_GENERATION") return this.StartDataGeneration();
        if (request == "STOP_GENERATION") return this.StopDataGeneration();
        if (request == "GET_GENERATION_STATUS") return this.GetTimerStatus();

        //  запрос на получение истории значений выбранного регистра
        if (request.StartsWith("GET_REGISTER_HISTORY:"))
        {
                string[] parts = request.Replace("GET_REGISTER_HISTORY:", "").Split('|');
                if (parts.Length < 3) return "ERROR: Неверный формат команды";

                int registerId = int.Parse(parts[0]);
                DateTime startDate = DateTime.Parse(parts[1]);
                DateTime endDate = DateTime.Parse(parts[2]);

    
                {
                    var history = context.RegisterValues
                        .Where(r => r.RegisterId == registerId && r.Timestamp >= startDate && r.Timestamp <= endDate)
                        .OrderBy(r => r.Timestamp)
                        .ToList();

                    if (history.Count == 0)
                        return "ERROR: Нет данных за выбранный период";

                    return string.Join(";", history.Select(r => $"{r.Timestamp:yyyy-MM-dd HH:mm:ss}|{r.Value}"));
                }
        }






        return "Неизвестная команда";
    }

    //  Логирование сервера (запись в таблицу Logs)
    private void LogMessage(string message, string type)
    {
        try
        {
            using (var context = new AppDbContext())
            {
                context.Logs.Add(new Log
                {
                    Timestamp = DateTime.Now, 
                    Message = message,        
                    Type = type               
                });

                context.SaveChanges();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($" Ошибка записи лога: {ex.Message}");
        }
    }
    //	Запрос на вывод 1000 последних сообщений (логов) 
    public string GetLastLogs(int count)
    {
        using (var context = new AppDbContext())
        {
            var logs = context.Logs
                .OrderByDescending(l => l.Timestamp) //  Последние логи вверху
                .Take(count)
                .ToList();

            StringBuilder logText = new StringBuilder();
            foreach (var log in logs)
            {
                logText.AppendLine($"[{log.Timestamp:yyyy-MM-dd HH:mm:ss}] [{log.Type}] {log.Message}");
            }

            return logText.ToString();
        }
    }



}
