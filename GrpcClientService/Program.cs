using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using GrpcExample;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.WithProperty("Application", "gRPC Client Service")
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} | Props: {Properties}{NewLine}{Exception}")
    .WriteTo.File(new CompactJsonFormatter(), "logs/grpc-client.json", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();  // Integrate Serilog with the host

// Add services to the container (e.g., controllers)
builder.Services.AddControllers();

// If needed, add your gRPC channel as a singleton (configurable via appsettings.json)
builder.Services.AddSingleton(sp =>
{
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var channel = GrpcChannel.ForAddress("http://grpcserver:8080");  // Or from config: builder.Configuration["GrpcServerAddress"]
    var invoker = channel.Intercept(new LoggingInterceptor(loggerFactory));
    return new Greeter.GreeterClient(invoker);
});

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();