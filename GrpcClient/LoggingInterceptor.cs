using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

public class LoggingInterceptor : Interceptor
{
    private readonly ILogger<LoggingInterceptor> _logger;

    public LoggingInterceptor(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<LoggingInterceptor>();
    }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        // Log the start of the call and request details
        _logger.LogInformation("Starting gRPC unary call. Host: {Host}, Method: {Method}, Request: {@Request}",
            context.Host, context.Method.FullName, request);

        var stopwatch = Stopwatch.StartNew();

        // Invoke the next interceptor or the actual call
        var call = continuation(request, context);

        // Wrap the response to log after completion
        return new AsyncUnaryCall<TResponse>(
            HandleResponseAsync(call.ResponseAsync, stopwatch),
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            call.Dispose);
    }

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        // Log the start of the call and request details
        _logger.LogInformation("Starting gRPC server-streaming call. Host: {Host}, Method: {Method}, Request: {@Request}",
            context.Host, context.Method.FullName, request);

        var stopwatch = Stopwatch.StartNew();

        AsyncServerStreamingCall<TResponse> call;
        try
        {
            // Invoke the next interceptor or the actual call
            call = continuation(request, context);
        }
        catch (RpcException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "gRPC server-streaming call failed during initialization. Status: {StatusCode}, Detail: {Detail}, Elapsed: {Elapsed}ms",
                ex.StatusCode, ex.Status.Detail, stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Unexpected error during gRPC server-streaming call initialization. Elapsed: {Elapsed}ms",
                stopwatch.ElapsedMilliseconds);
            throw;
        }

        // Wrap the response stream to log each message and completion/errors
        var wrappedStream = new LoggingAsyncStreamReader<TResponse>(call.ResponseStream, _logger, stopwatch);

        return new AsyncServerStreamingCall<TResponse>(
            wrappedStream,
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            call.Dispose);
    }

    private async Task<TResponse> HandleResponseAsync<TResponse>(Task<TResponse> responseTask, Stopwatch stopwatch)
    {
        try
        {
            var response = await responseTask;
            stopwatch.Stop();
            _logger.LogInformation("gRPC unary call completed successfully. Response: {@Response}, Elapsed: {Elapsed}ms",
                response, stopwatch.ElapsedMilliseconds);
            return response;
        }
        catch (RpcException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "gRPC unary call failed. Status: {StatusCode}, Detail: {Detail}, Elapsed: {Elapsed}ms",
                ex.StatusCode, ex.Status.Detail, stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Unexpected error in gRPC unary call. Elapsed: {Elapsed}ms", stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    // Helper class to wrap the stream reader for logging
    private class LoggingAsyncStreamReader<TResponse> : IAsyncStreamReader<TResponse>
    {
        private readonly IAsyncStreamReader<TResponse> _innerReader;
        private readonly ILogger _logger;
        private readonly Stopwatch _stopwatch;
        private int _messageCount = 0;

        public LoggingAsyncStreamReader(IAsyncStreamReader<TResponse> innerReader, ILogger logger, Stopwatch stopwatch)
        {
            _innerReader = innerReader;
            _logger = logger;
            _stopwatch = stopwatch;
        }

        public TResponse Current => _innerReader.Current;

        public async Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            try
            {
                var hasNext = await _innerReader.MoveNext(cancellationToken);
                if (hasNext)
                {
                    _messageCount++;
                    _logger.LogInformation("Received streamed message #{MessageCount}: {@Response}",
                        _messageCount, Current);
                }
                else
                {
                    _stopwatch.Stop();
                    _logger.LogInformation("gRPC server-streaming call completed. Total Messages: {MessageCount}, Elapsed: {Elapsed}ms",
                        _messageCount, _stopwatch.ElapsedMilliseconds);
                }
                return hasNext;
            }
            catch (RpcException ex)
            {
                _stopwatch.Stop();
                _logger.LogError(ex, "gRPC server-streaming call failed. Status: {StatusCode}, Detail: {Detail}, Elapsed: {Elapsed}ms",
                    ex.StatusCode, ex.Status.Detail, _stopwatch.ElapsedMilliseconds);
                throw;
            }
            catch (Exception ex)
            {
                _stopwatch.Stop();
                _logger.LogError(ex, "Unexpected error in gRPC server-streaming call. Elapsed: {Elapsed}ms", _stopwatch.ElapsedMilliseconds);
                throw;
            }
        }
    }
}