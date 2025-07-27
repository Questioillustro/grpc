using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using GrpcExample;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace GrpcClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole(); 
                builder.SetMinimumLevel(LogLevel.Information);
            });

            using var channel = GrpcChannel.ForAddress("http://localhost:5285");
            var invoker = channel.Intercept(new LoggingInterceptor(loggerFactory));

            var client = new Greeter.GreeterClient(invoker);

            var helloReply = await client.SayHelloAsync(new HelloRequest { Name = "Client" });

            var request = new HelloRequest { Name = "Stream to Client" };
            var callOptions = new CallOptions(deadline: DateTime.UtcNow.AddSeconds(1));

            using var call = client.SayHelloServerStream(request, callOptions);
            try
            {
                while (await call.ResponseStream.MoveNext(default))
                {
                    var response = call.ResponseStream.Current;
                }
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
            {
                Console.WriteLine("Streaming call timed out.");  // Optional: Handle specifically in client code
                                                                 // The interceptor will already log this
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}