Aby uzyskać w WCF zwracanie błędów w formacie JSON zgodnym z modelem ASP.NET Core (zamiast domyślnej strony HTML), należy obsłużyć błędy uniwersalnie i serializować odpowiedzi w ustandaryzowany sposób, niezależnie czy kontrakt definiuje FaultContract. Można to osiągnąć implementując własny handler błędów (IErrorHandler) oraz rzucając WebFaultException — oba te mechanizmy mogą zadbać o kod HTTP oraz model JSON.[^1][^2][^3]

### Kiedy rzucać 4xx, a kiedy 5xx?

- Błędy 4xx (np. 400, 401, 403, 404) zwracaj, gdy to klient zawinił — błędne dane, brak uprawnień, nieprawidłowy token.
- 5xx (500, 502, 503) to błędy serwera, np. wyjątki aplikacyjne, niespodziewane sytuacje.

Np. zgodnie z ASP.NET Core najczęściej API zwraca w obu przypadkach obiekt ProblemDetails — można ten sam model zaimplementować także w WCF.[^4][^5]

### Universalny handler błędów w WCF

1. **Dodaj własny IErrorHandler** — przechwytuje WSZYSTKIE nieobsłużone wyjątki.
2. **W metodzie ProvideFault** budujesz wiadomość JSON i ustawiasz Content-Type, status HTTP oraz ciała odpowiedzi.
3. **Opcjonalnie**: jeśli rzucasz WebFaultException<MyErrorModel>, WCF potrafi automatycznie serializować obiekt do JSON, jeśli ustawisz WebMessageFormat.Json.

#### Przykład własnego handlera

```csharp
public class JsonErrorHandler : IErrorHandler
{
    public bool HandleError(Exception error)
    {
        // opcjonalnie logowanie
        return true;
    }

    public void ProvideFault(Exception error, MessageVersion version, ref Message fault)
    {
        // Tworzenie modelu w stylu ProblemDetails ASP.NET Core
        var problemDetails = new
        {
            type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            title = "An error occurred",
            status = 500,
            detail = error.Message
        };

        var json = System.Text.Json.JsonSerializer.Serialize(problemDetails);

        // Tworzenie wiadomości z treścią JSON
        var responseMessage = Message.CreateMessage(version, "", json);

        responseMessage.Properties.Add(
            WebBodyFormatMessageProperty.Name, new WebBodyFormatMessageProperty(WebContentFormat.Raw));

        var httpResponse = new HttpResponseMessageProperty
        {
            StatusCode = HttpStatusCode.InternalServerError,
            Headers = { [HttpResponseHeader.ContentType] = "application/json" }
        };
        responseMessage.Properties.Add(HttpResponseMessageProperty.Name, httpResponse);

        fault = responseMessage;
    }
}
```

Taki handler dodajesz do zachowań (behaviors) swojego serwisu — najlepiej serwisowo, by obsłużyć każde żadne wyjątki.[^2][^3][^1]

### Automatyzacja dla braku FaultContract

Dzięki handlerowi IErrorHandler wszystko co nie jest obsłużone FaultContract (czyli także nieprzewidziane błędy) zostanie automatycznie opakowane z kodem HTTP (np. 500) i ustandaryzowanym modelem JSON — identycznie jak robi to ASP.NET Core, gdzie domyślnie ProblemDetails pojawi się przy błędach.

Możesz inspirować się modelem ProblemDetails ([RFC 7807](https://tools.ietf.org/html/rfc7807)) również w WCF, zachowując spójność API.

***

**Podsumowanie:**
Implementacja IErrorHandler pozwala na spójne i uniwersalne zwracanie błędów w formacie JSON, w stylu ASP.NET Core, niezależnie czy kontrakt przewiduje FaultContract. Kod HTTP oraz treść JSON możesz opisać zgodnie ze standardem ProblemDetails dla pełnej zgodności.[^3][^1][^2]
<span style="display:none">[^10][^11][^12][^13][^14][^15][^16][^17][^18][^19][^20][^6][^7][^8][^9]</span>

<div align="center">⁂</div>

[^1]: https://stackoverflow.com/questions/1149037/how-to-make-custom-wcf-error-handler-return-json-response-with-non-ok-http-code

[^2]: https://adaptivepatchwork.com/2009/10/07/exception-shielding-for-json-wcf-services/

[^3]: https://learn.microsoft.com/en-us/dotnet/framework/wcf/feature-details/wcf-web-http-error-handling

[^4]: https://www.milanjovanovic.tech/blog/problem-details-for-aspnetcore-apis

[^5]: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-9.0

[^6]: https://stackoverflow.com/questions/69055754/wcf-svc-returns-a-html-error-page-instead-of-json-when-streamreader-used

[^7]: https://stackoverflow.com/questions/13753815/return-html-format-on-wcf-service-instead-of-json-or-xml/13753928

[^8]: https://www.codemag.com/article/1308041/Reuse-Your-WCF-Services-from-jQuery

[^9]: https://www.c-sharpcorner.com/UploadFile/b365a8/ajax-enabled-wcf-service-using-json/

[^10]: https://weblog.west-wind.com/posts/2008/jun/23/crashing-wcf-35-json-services-with-datetimeminvalue

[^11]: https://stackify.com/wcf-exception-handling-tutorial/

[^12]: https://www.youtube.com/watch?v=uOEDM0c9BNI

[^13]: https://pradeepl.com/blog/serialize-exceptions-as-json-using-custom-middleware-in-net-core/

[^14]: https://community.postman.com/t/how-return-a-json-response-instead-of-an-html-error-code-like-401-or-400-when-the-jwt-token-is-not-valid-expired-or-not-used/50111

[^15]: https://www.kjeldby.dk/2009/12/wcf-exception-handling/

[^16]: https://stackoverflow.com/questions/38630076/asp-net-core-web-api-exception-handling

[^17]: https://devblogs.microsoft.com/dotnet/custom-asp-net-core-middleware-example/

[^18]: https://www.reddit.com/r/dotnet/comments/193432s/what_causes_apis_to_return_html_content_instead/

[^19]: https://codejack.com/2024/11/json-parsing-error-handling-in-net/

[^20]: https://wizardsofsmart.wordpress.com/2011/02/04/middleware-in-the-wcf-web-apis/

