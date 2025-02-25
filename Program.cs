using System;
class Program
{
    static void Main()
    {
        Console.WriteLine("Инициализация базы данных...");
        DatabaseInitializer.Initialize();
        Console.WriteLine("База данных готова!");

        Console.WriteLine("Запуск TCP-сервера...");

        //  Создаём объект сервера
        TcpServer serverInstance = new TcpServer();
       // serverInstance.GenerateTestData();
        //  Запускаем сервер через объект
        serverInstance.Start();
        while (true)
        {
            string request = Console.ReadLine();
            string response = serverInstance.ProcessRequest(request); //вызов через объект(для решения опр. проблем с статическими и нестатическими методами)
            Console.WriteLine(response);
        }

        Console.ReadLine(); // Чтобы консоль не закрывалась
    }

}
