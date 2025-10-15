

Nie musisz dodawać metody `NotFound({*path})` w kontrakcie, żeby przechwytywać niezmapowane URI w WCF WebHttp. Da się to zrobić „niżej w pipeline”, korzystając z własnego zachowania (`Behavior`) albo hosta.

### Rozwiązanie: własny `WebHttpBehavior` z obsługą nieznanych URI

Najczystsza metoda to stworzenie własnego zachowania dziedziczącego po `WebHttpBehavior`, w którym zastąpisz domyślny `UnhandledDispatchOperation`. Dzięki temu wszystkie niezmapowane żądania będą trafiać do Twojej logiki, bez widocznej metody `NotFound` w kontrakcie.

#### Przykład szkicu implementacji:

```csharp
public class CustomWebHttpBehavior : WebHttpBehavior
{
    protected override void AddServerErrorHandlers(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
    {
        base.AddServerErrorHandlers(endpoint, endpointDispatcher);
        endpointDispatcher.ChannelDispatcher.ErrorHandlers.Clear(); // jeśli używasz własnych handlerów
    }

    protected override void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
    {
        base.ApplyDispatchBehavior(endpoint, endpointDispatcher);
        endpointDispatcher.DispatchRuntime.UnhandledDispatchOperation = new DispatchOperation(
            endpointDispatcher.DispatchRuntime,
            "UnknownOperation",
            "UnknownOperation",
            null
        )
        {
            Invoker = new UnknownOperationInvoker()
        };
    }
}

public class UnknownOperationInvoker : IOperationInvoker
{
    public object[] AllocateInputs() => new object[^0];

    public object Invoke(object instance, object[] inputs, out object returnValue)
    {
        var msg = WebOperationContext.Current.CreateTextResponse(
            "Resource not found",
            "text/plain",
            HttpStatusCode.NotFound
        );
        returnValue = null;
        return msg;
    }

    public IAsyncResult InvokeBegin(object instance, object[] inputs, AsyncCallback callback, object state) => null;
    public object InvokeEnd(object instance, out object[] outputs, IAsyncResult result) { outputs = null; return null; }
    public bool IsSynchronous => true;
}
```

Potem musisz tylko dodać zachowanie do swojego endpointu, np. przez `ServiceHostFactory` lub konfigurację:

```xml
<endpointBehaviors>
  <behavior name="RestBehavior">
    <customError />
  </behavior>
</endpointBehaviors>
```

To podejście opisano szczegółowo w [^1]. Tam `WebHttpExceptionBehavior` lub `WcfUnknownUriBehavior` zastępuje wewnętrzny handler WCF, pozwalając zwracać `WebFaultException` albo własny `Message` z JSON/XML odpowiedzią – bez dodawania śmieciowej metody w kontrakcie.

### Alternatywa: `IServiceBehavior` lub `Custom DispatchRuntime`

W bardziej elastycznych przypadkach możesz zarejestrować `IServiceBehavior`, który w `ApplyDispatchBehavior` ustawi własny `UnknownDispatcher` (lub zmodyfikuje `DispatchRuntime.UnhandledDispatchOperation`). Te rozwiązania również nie wpływają na kontrakt i zachowują kompletne formatowanie odpowiedzi (np. `WebFaultException<T>`).

### Podsumowanie

Najczystszy sposób, by przechwycić żądania do nieistniejących ścieżek w `WebHttpBinding`, to:

- stworzyć własny `WebHttpBehavior` lub `ServiceBehavior`,
- zamienić `UnhandledDispatchOperation` na własny `IOperationInvoker`,
- zwracać `WebFaultException` lub własny komunikat.

Nie wymaga to ani `UriTemplate {*path}` w kontrakcie, ani ujawniania sztucznej metody klientowi [^1][^2].
<span style="display:none">[^10][^11][^12][^13][^14][^15][^16][^3][^4][^5][^6][^7][^8][^9]</span>

<div align="center">⁂</div>

[^1]: https://stackoverflow.com/questions/6371856/handling-404-in-wcf-rest-service

[^2]: https://community.dynamics.com/blogs/post/?postid=51fdfcc6-643c-40fa-9382-96786a18b8f1

[^3]: https://forum.winhost.com/threads/wcf-web-service-method-resource-not-found.6189/

[^4]: https://learn.microsoft.com/en-us/answers/questions/1183177/wcf-service-is-not-running-over-the-https-on-multi

[^5]: https://github.com/symfony/symfony/issues/5320

[^6]: https://stackoverflow.com/questions/23750754/handing-unauthorized-requests-in-wcf

[^7]: https://stackoverflow.com/questions/33018220/how-can-i-use-strongly-typed-parameters-in-the-uri-path-in-wcf-with-webhttpbindi

[^8]: https://learn.microsoft.com/en-us/biztalk/core/wcf-webhttp-adapter

[^9]: https://www.biztalkgurus.com/blogs/msft-biztalk-community/error-handling-in-wcf-webhttp-services-with-webfaultexception/

[^10]: https://learn.microsoft.com/en-us/dotnet/framework/wcf/feature-details/uritemplate-and-uritemplatetable

[^11]: https://bauchart.be/2017/10/01/custom-404-pages-in-asp-net-core-for-routes-and-static-files/

[^12]: http://accorsi.net/docs/wcf_4.pdf

[^13]: https://stackoverflow.com/questions/tagged/uritemplate

[^14]: https://world.optimizely.com/forum/developer-forum/CMS/Thread-Container/2017/1/how-to-custom-a-friendly-404-error/rehow-to-custom-a-friendly-404-error2/

[^15]: https://cedarlogic.blob.core.windows.net/public/pacedps/pro-wcf-practical-microsoft-soa-implementation.9781590597026.27678.pdf

[^16]: https://www.scribd.com/document/221861767/70513

