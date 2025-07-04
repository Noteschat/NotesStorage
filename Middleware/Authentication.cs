using System.Text.Json;
using System.Net;

namespace NotesStorage.Middlewares
{
    public class Authentication
    {
        private readonly RequestDelegate _next;
        private readonly IdentityCache<User> _idCache;

        public Authentication(RequestDelegate next, IdentityCache<User> idCache)
        {
            _next = next;
            _idCache = idCache;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var sessionId = context.Request.Cookies["sessionId"];
            if(sessionId == null)
            {
                Logger.Warn("Unauthenticated Connection attempt.");

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";

                await context.Response.WriteAsync(JsonSerializer.Serialize(new { cause = "not logged in" }));
                return;
            }

            var cacheEntry = _idCache.Get(sessionId);
            if (!cacheEntry.IsSuccess)
            {
                var cookies = new CookieContainer();
                cookies.Add(new Uri("http://localhost/"), new Cookie("sessionId", sessionId));
                var handler = new HttpClientHandler
                {
                    CookieContainer = cookies
                };

                var _httpClient = new HttpClient(handler);
                var apiResponse = await _httpClient.GetAsync($"http://localhost/api/identity/login/valid");

                if (!apiResponse.IsSuccessStatusCode)
                {
                    Logger.Warn("Unknown Session Connection attempt.");

                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json";

                    await context.Response.WriteAsync(JsonSerializer.Serialize(new { cause = "not logged in" }));
                    return;
                }

                var jsonResponse = await apiResponse.Content.ReadAsStringAsync();
                var user = JsonSerializer.Deserialize<User>(jsonResponse);
                context.Items["user"] = user;

                _idCache.Add(sessionId, user);
            }
            else
            {
                context.Items["user"] = cacheEntry.Success;
            }

            // Forward the request to the next middleware
            await _next(context);
        }
    }
}
