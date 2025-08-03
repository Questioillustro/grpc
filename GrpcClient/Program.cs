using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using GrpcExample;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Formatting.Compact;
using Serilog.Context;

namespace GrpcClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.WithProperty("Application", "gRPC Client")
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} | Props: {Properties}{NewLine}{Exception}"
                )
                .WriteTo.File(
                    new CompactJsonFormatter(), 
                    "logs/grpc-client.json", 
                    rollingInterval: RollingInterval.Day
                )
                .CreateLogger();

            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddSerilog(Log.Logger);
            });

            using var channel = GrpcChannel.ForAddress("http://localhost:32768");
            var invoker = channel.Intercept(new LoggingInterceptor(loggerFactory));

            var client = new Greeter.GreeterClient(invoker);

            var correlationIdUnary = Guid.NewGuid().ToString();
            var headersUnary = new Metadata { { "correlation-id", correlationIdUnary } };
            var optionsUnary = new CallOptions(headers: headersUnary);
            
            using (LogContext.PushProperty("CorrelationId", correlationIdUnary))
            {
                var helloReply = await client.SayHelloAsync(new HelloRequest { Name = "Client" }, optionsUnary);
            }

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