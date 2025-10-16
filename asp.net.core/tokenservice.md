<img src="https://r2cdn.perplexity.ai/pplx-full-logo-primary-dark%402x.png" style="height:64px;margin-right:32px"/>

# zrób mi przykład z osobnym serwisem zarejestrowanym w DI, serwis odpowiada za pobieranie i cache tokena

Poniżej przykład implementacji w C\#, w której oddzielasz serwis odpowiedzialny za pobieranie i cache tokena oraz korzystanie z niego w `HttpClient` z `DelegatingHandler`. Wszystko rejestrowane w Dependency Injection (DI).

***

### 1. Serwis tokena z cache i pobieraniem nowego tokena

```csharp
public interface ITokenService
{
    Task<string> GetTokenAsync();
    Task<string> RefreshTokenAsync();
    void ClearToken();
}

public class TokenService : ITokenService
{
    private string _token;
    private DateTime _tokenExpiry;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1,1);

    public async Task<string> GetTokenAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (string.IsNullOrEmpty(_token) || DateTime.UtcNow >= _tokenExpiry)
            {
                await RefreshTokenAsync();
            }
            return _token;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<string> RefreshTokenAsync()
    {
        // Przykład pobrania tokena (np. HTTP request do endpointu autoryzacji)
        // Ustaw token i czas wygaśnięcia (np. 60 minut dalej)
        // Poniższy przykład to placeholder, podmień na własną logikę

        var newToken = await FetchTokenFromAuthServerAsync();
        _token = newToken.token;
        _tokenExpiry = DateTime.UtcNow.AddSeconds(newToken.expiresIn - 10); // z zapasem 10s
        return _token;
    }

    public void ClearToken()
    {
        _token = null;
        _tokenExpiry = DateTime.MinValue;
    }

    private async Task<(string token, int expiresIn)> FetchTokenFromAuthServerAsync()
    {
        // Tu faktycznie wykonaj żądanie HTTP do serwera autoryzacji
        await Task.Delay(100); // symulacja async
        return ("some_access_token", 3600); // token i czas życia w sekundach
    }
}
```


***

### 2. DelegatingHandler do dodawania tokena i odświeżania przy 401

```csharp
public class AuthenticatedHttpClientHandler : DelegatingHandler
{
    private readonly ITokenService _tokenService;

    public AuthenticatedHttpClientHandler(ITokenService tokenService, HttpMessageHandler innerHandler) 
        : base(innerHandler)
    {
        _tokenService = tokenService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _tokenService.GetTokenAsync();
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            // Token może wygasł, odśwież i ponów
            var newToken = await _tokenService.RefreshTokenAsync();
            if (string.IsNullOrEmpty(newToken)) 
            {
                return response; // nie udało się odświeżyć tokena
            }

            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", newToken);
            response = await base.SendAsync(request, cancellationToken);
        }

        return response;
    }
}
```


***

### 3. Rejestracja w DI (np. w ASP.NET Core Startup/Program)

```csharp
// Rejestracja serwisu tokena jako singleton
services.AddSingleton<ITokenService, TokenService>();

// Rejestracja HttpClient z delegatem
services.AddTransient<AuthenticatedHttpClientHandler>();

services.AddHttpClient("AuthorizedClient")
        .AddHttpMessageHandler<AuthenticatedHttpClientHandler>();
```


***

### 4. Użycie HttpClient

```csharp
public class SomeApiService
{
    private readonly HttpClient _client;

    public SomeApiService(IHttpClientFactory httpClientFactory)
    {
        _client = httpClientFactory.CreateClient("AuthorizedClient");
    }

    public async Task<string> GetDataAsync()
    {
        var response = await _client.GetAsync("https://twojeapi.com/data");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
```


***

### Podsumowanie

- Serwis `TokenService` odpowiada za cache i odświeżanie tokena, bezpośrednio komunikując się z serwerem autoryzacji.
- `AuthenticatedHttpClientHandler` dokłada token do każdego żądania i odświeża go przy 401.
- Rejestrujesz wszystko w DI, korzystasz ze standardowego `IHttpClientFactory`.
- Dzięki temu masz separację odpowiedzialności i czysty, łatwo testowalny kod.

Czy chcesz przykład implementacji faktycznego zapytania po token do endpointa autoryzacji lub testowego projektu?

