using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace CrushIt.API
{
    public class RetryPolicy
    {
        private readonly int _maxRetries;
        private readonly TimeSpan _initialDelay;
        private readonly TimeSpan _maxDelay;
        private readonly double _backoffMultiplier;
        private readonly Func<HttpResponseMessage, bool> _shouldRetry;

        public RetryPolicy(
            int maxRetries = 3, 
            TimeSpan? initialDelay = null, 
            TimeSpan? maxDelay = null,
            double backoffMultiplier = 2.0,
            Func<HttpResponseMessage, bool>? shouldRetry = null)
        {
            _maxRetries = maxRetries;
            _initialDelay = initialDelay ?? TimeSpan.FromSeconds(1);
            _maxDelay = maxDelay ?? TimeSpan.FromSeconds(30);
            _backoffMultiplier = backoffMultiplier;
            _shouldRetry = shouldRetry ?? DefaultShouldRetry;
        }

        public async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
        {
            int retryCount = 0;
            TimeSpan currentDelay = _initialDelay;

            while (true)
            {
                try
                {
                    return await action();
                }
                catch (HttpRequestException ex) when (retryCount < _maxRetries)
                {
                    retryCount++;
                    Console.WriteLine($"Request failed (attempt {retryCount}/{_maxRetries}): {ex.Message}");
                    
                    if (retryCount < _maxRetries)
                    {
                        await Task.Delay(currentDelay);
                        currentDelay = TimeSpan.FromMilliseconds(
                            Math.Min(currentDelay.TotalMilliseconds * _backoffMultiplier, _maxDelay.TotalMilliseconds));
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (TaskCanceledException ex) when (retryCount < _maxRetries && !ex.CancellationToken.IsCancellationRequested)
                {
                    retryCount++;
                    Console.WriteLine($"Request timed out (attempt {retryCount}/{_maxRetries})");
                    
                    if (retryCount < _maxRetries)
                    {
                        await Task.Delay(currentDelay);
                        currentDelay = TimeSpan.FromMilliseconds(
                            Math.Min(currentDelay.TotalMilliseconds * _backoffMultiplier, _maxDelay.TotalMilliseconds));
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        public async Task<HttpResponseMessage> ExecuteWithRetryAsync(Func<Task<HttpResponseMessage>> action)
        {
            int retryCount = 0;
            TimeSpan currentDelay = _initialDelay;

            while (true)
            {
                try
                {
                    var response = await action();
                    
                    if (_shouldRetry(response) && retryCount < _maxRetries)
                    {
                        retryCount++;
                        Console.WriteLine($"Request returned retryable status {response.StatusCode} (attempt {retryCount}/{_maxRetries})");
                        
                        response.Dispose();
                        await Task.Delay(currentDelay);
                        currentDelay = TimeSpan.FromMilliseconds(
                            Math.Min(currentDelay.TotalMilliseconds * _backoffMultiplier, _maxDelay.TotalMilliseconds));
                    }
                    else
                    {
                        return response;
                    }
                }
                catch (HttpRequestException ex) when (retryCount < _maxRetries)
                {
                    retryCount++;
                    Console.WriteLine($"Request failed (attempt {retryCount}/{_maxRetries}): {ex.Message}");
                    
                    if (retryCount < _maxRetries)
                    {
                        await Task.Delay(currentDelay);
                        currentDelay = TimeSpan.FromMilliseconds(
                            Math.Min(currentDelay.TotalMilliseconds * _backoffMultiplier, _maxDelay.TotalMilliseconds));
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (TaskCanceledException ex) when (retryCount < _maxRetries && !ex.CancellationToken.IsCancellationRequested)
                {
                    retryCount++;
                    Console.WriteLine($"Request timed out (attempt {retryCount}/{_maxRetries})");
                    
                    if (retryCount < _maxRetries)
                    {
                        await Task.Delay(currentDelay);
                        currentDelay = TimeSpan.FromMilliseconds(
                            Math.Min(currentDelay.TotalMilliseconds * _backoffMultiplier, _maxDelay.TotalMilliseconds));
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        private bool DefaultShouldRetry(HttpResponseMessage response)
        {
            return response.StatusCode == HttpStatusCode.ServiceUnavailable ||
                   response.StatusCode == HttpStatusCode.GatewayTimeout ||
                   response.StatusCode == HttpStatusCode.RequestTimeout ||
                   (int)response.StatusCode >= 500;
        }

        public static RetryPolicy Default => new RetryPolicy();
        public static RetryPolicy Aggressive => new RetryPolicy(maxRetries: 5, initialDelay: TimeSpan.FromSeconds(0.5));
        public static RetryPolicy Conservative => new RetryPolicy(maxRetries: 2, initialDelay: TimeSpan.FromSeconds(2));
    }
}
