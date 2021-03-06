﻿using System;
using System.Net;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;

namespace SimpleHTTPServer
{
    public enum ServiceMode
    {
        Scoped, Tranisent, Singleton
    }
    public class ServiceDescriptor : ICloneable
    {
        public Type InterfaceType { get; set; }
        public Type InstanceType { get; set; }
        public object Instance { get; set; }
        public ServiceMode Mode { get; set; }

        public object Clone()
        {
            return MemberwiseClone();
        }
    }
    public class WebHostBuilder
    {
        private List<ServiceDescriptor> serviceCollection = new List<ServiceDescriptor>();
        private HttpListener listener = new HttpListener();
        private bool isShutdown;
        public WebHostBuilder AddScoped<I, T>(Action<T> config = null) where T : I
        {
            serviceCollection.Add(new ServiceDescriptor
            {
                InterfaceType = typeof(I),
                InstanceType = typeof(T),
                Mode = ServiceMode.Scoped
            });
            return this;
        }
        public WebHostBuilder AddSingleton<I, T>(Action<T> config = null) where T : I, new()
        {
            I instance = new T();
            serviceCollection.Add(new ServiceDescriptor
            {
                InterfaceType = typeof(I),
                InstanceType = typeof(T),
                Instance = instance,
                Mode = ServiceMode.Singleton
            });
            return this;
        }

        public WebHostBuilder AddTranisent<I, T>(Action<T> config = null) where T : I, new()
        {
            serviceCollection.Add(new ServiceDescriptor
            {
                InterfaceType = typeof(I),
                InstanceType = typeof(T),
                Mode = ServiceMode.Tranisent
            });
            return this;
        }

        object CreateInstance(ServiceDescriptor service, HttpListenerRequest request, HttpListenerResponse response)
        {
            if (service.Mode == ServiceMode.Singleton) return null;
            var cons = service.InstanceType.GetConstructors()[0];
            var paramCons = cons.GetParameters();
            var paramConsList = new List<object>();
            foreach (var p in paramCons)
            {
                var type = p.ParameterType.AssemblyQualifiedName;
                if (type == typeof(HttpListenerRequest).AssemblyQualifiedName) paramConsList.Add(request);
                else if (type == typeof(HttpListenerResponse).AssemblyQualifiedName) paramConsList.Add(response);
                else paramConsList.Add(serviceCollection.FirstOrDefault(i => i.InterfaceType.AssemblyQualifiedName == type)?.Instance ?? throw new Exception($"No instance for parameter: {p.Name}"));
            }
            switch (service.Mode)
            {
                case ServiceMode.Scoped:
                    return service.Instance = cons.Invoke(paramConsList.ToArray());
                case ServiceMode.Tranisent:
                    return cons.Invoke(paramConsList.ToArray());
                default:
                    return null;
            }
        }
        public async void Run()
        {
            listener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
            listener.Prefixes.Add("http://*:80/");
            listener.Start();

            while (!isShutdown)
            {
                await listener.GetContextAsync().ContinueWith((con, obj) =>
                {
                    Task.Run(async () =>
                    {
                        Console.WriteLine($"Received HTTP request");
                        var context = await con;
                        var request = context.Request;
                        var response = context.Response;
                        response.StatusCode = 200;
                        var collection = serviceCollection.ConvertAll(i => i.Clone() as ServiceDescriptor);
                        foreach (var i in collection.Where(i => i.Mode != ServiceMode.Singleton))
                        {
                            i.Instance = null;
                        }
                        foreach (var service in collection)
                        {
                            var param = service.InstanceType.GetMethod("Process").GetParameters();
                            var paramList = new List<object>();
                            foreach (var p in param)
                            {
                                var type = p.ParameterType.AssemblyQualifiedName;
                                if (type == typeof(HttpListenerRequest).AssemblyQualifiedName) paramList.Add(request);
                                else if (type == typeof(HttpListenerResponse).AssemblyQualifiedName) paramList.Add(response);
                                else
                                {
                                    switch (service.Mode)
                                    {
                                        case ServiceMode.Tranisent:
                                        case ServiceMode.Scoped:
                                            paramList.Add(collection.FirstOrDefault(i => i.InterfaceType.AssemblyQualifiedName == type)?.Instance ?? CreateInstance(service, request, response));
                                            break;
                                        case ServiceMode.Singleton:
                                            Debug.Assert(service.Instance != null);
                                            paramList.Add(collection.FirstOrDefault(i => i.InterfaceType.AssemblyQualifiedName == type)?.Instance ?? throw new InvalidOperationException($"No instance for parameter: {p.Name}"));
                                            break;
                                    }

                                }
                            }
                            try
                            {
                                object instance = service.Instance;
                                if (instance == null) instance = CreateInstance(service, request, response);
                                instance.GetType().GetMethod("Process").Invoke(instance, paramList.ToArray());
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.Message);
                                response.StatusCode = 500;
                                break;
                            }
                        }
                        response.Close();
                        Console.WriteLine($"HTTP request finished: {response.StatusCode}");
                    });
                }, null);
            }
            listener.Stop();
        }

        public void Shutdown()
        {
            isShutdown = true;
        }
    }
    public class Server
    {
        public static WebHostBuilder CreateWebHost()
        {
            var host = new WebHostBuilder();
            return host;
        }
    }
}
