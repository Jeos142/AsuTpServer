using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace AsuTpServer.Services
{
    public class HttpServer
    {
        private TcpServer _tcpServer; //  Доступ к TcpServer для логов

        public HttpServer(TcpServer tcpServer)
        {
            _tcpServer = tcpServer;
        }

        public void Start()
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:8080/"); //  Сервер работает на http://localhost:8080/
            listener.Start();

            Console.WriteLine("HTTP-сервер запущен на порту 8080...");

            Task.Run(() =>
            {
                while (true)
                {
                    HttpListenerContext context = listener.GetContext();
                    HttpListenerRequest request = context.Request;
                    HttpListenerResponse response = context.Response;

                    if (request.Url.AbsolutePath == "/logs")
                    {
                        string logData = _tcpServer.GetLastLogs(1000); //  Запрос логов у TCP-сервера
                        byte[] buffer = Encoding.UTF8.GetBytes(logData);

                        response.ContentType = "text/plain; charset=UTF-8";
                        response.ContentLength64 = buffer.Length;
                        response.OutputStream.Write(buffer, 0, buffer.Length);
                        response.OutputStream.Close();
                    }
                    else
                    {
                        response.StatusCode = 404;
                        byte[] buffer = Encoding.UTF8.GetBytes("404 Not Found");
                        response.OutputStream.Write(buffer, 0, buffer.Length);
                        response.OutputStream.Close();
                    }
                }
            });
        }
    }
}
