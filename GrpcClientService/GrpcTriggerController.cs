using Grpc.Core;
using GrpcExample;  
using Microsoft.AspNetCore.Mvc;
using Serilog.Context;

[ApiController]
[Route("api/[controller]")]
public class GrpcTriggerController : ControllerBase
{
    private readonly Greeter.GreeterClient _client;
    private readonly ILogger<GrpcTriggerController> _logger;

    public GrpcTriggerController(Greeter.GreeterClient client, ILogger<GrpcTriggerController> logger)
    {
        _client = client;
        _logger = logger;
    }

    [HttpPost("unary")]
    public async Task<IActionResult> TriggerUnaryCall([FromBody] HelloRequest request)
    {
        var correlationId = Guid.NewGuid().ToString();
        var headers = new Metadata { { "correlation-id", correlationId } };
        var options = new CallOptions(headers: headers);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            try
            {
                var reply = await _client.SayHelloAsync(request ?? new HelloRequest { Name = "Client" }, options);
                return Ok(reply);
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Unary gRPC call failed");
                return StatusCode(500, ex.Message);
            }
        }
    }

    [HttpPost("server-stream")]
    public async Task<IActionResult> TriggerServerStream([FromBody] HelloRequest request)
    {
        request ??= new HelloRequest { Name = "Stream to Client" };
        var callOptions = new CallOptions(deadline: DateTime.UtcNow.AddSeconds(1));

        try
        {
            using var call = _client.SayHelloServerStream(request, callOptions);
            var responses = new List<HelloReply>();

            while (await call.ResponseStream.MoveNext(default))
            {
                responses.Add(call.ResponseStream.Current);
            }

            return Ok(responses);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.DeadlineExceeded)
        {
            _logger.LogWarning("Server streaming call timed out.");
            return StatusCode(504, "Timeout");
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "Server streaming gRPC call failed");
            return StatusCode(500, ex.Message);
        }
    }
}