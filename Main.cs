using System;
using System.Net;
using System.Text;

namespace SimpleHTTPServer
{
    interface ITest
    {
        void Counter();
    }
    class Test : ITest
    {
        private int counter = 0;
        public void Process(HttpListenerRequest request, HttpListenerResponse response)
        {
            var str = "Hello from Test, request URL: " + request.RawUrl;
            response.OutputStream.Write(Encoding.UTF8.GetBytes(str));
            Counter();
        }
        public void Counter()
        {
            counter++;
            Console.WriteLine($"Singleton: {counter}");
        }
    }

    interface ITest2
    {
        void Counter();
    }
    class Test2 : ITest2
    {
        private int counter = 0;
        public void Process(ITest test)
        {
            test.Counter();
            Counter();
        }
        public void Counter()
        {
            counter++;
            Console.WriteLine($"Scoped: {counter}");
        }
    }
    interface ITest3
    {
        void Counter();
    }
    class Test3 : ITest3
    {
        private int counter = 0;
        public void Process(ITest2 test)
        {
            test.Counter();
            Counter();
        }
        public void Counter()
        {
            counter++;
            Console.WriteLine($"Tranisent: {counter}");
        }
    }
    internal class Program
    {
        static void Main()
        {
            var server = new WebHostBuilder();
            server
                .AddSingleton<ITest, Test>()
                .AddScoped<ITest2, Test2>()
                .AddTranisent<ITest3, Test3>()
            .Run();

            Console.ReadLine();
            server.Shutdown();
        }
    }
}