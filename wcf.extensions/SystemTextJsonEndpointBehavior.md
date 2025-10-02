Zgadza się, przy użyciu `webHttpBinding` i REST w WCF, właściwsze jest dodanie customowego **EndpointBehavior**, który podmienia formatter na poziomie endpointu, a nie całego serwisu. Pozwala to na bardziej precyzyjną kontrolę i zgodność z modelem webHttpBinding.

Poniżej wygenerowany kod EndpointBehavior, który ustawia `SystemTextJsonFormatter` dla wszystkich operacji endpointu, oraz przykładowa konfiguracja, jak go dodać w `app.config`:


- Zamień `yourNamespace` na rzeczywistą przestrzeń nazw klasy `SystemTextJsonEndpointBehavior`.
- Usuń lub nie stosuj `webHttpBehavior` jednocześnie, aby uniknąć konfliktów.

Dzięki temu konfigurujesz formatter na poziomie endpointu, co jest zalecane przy `webHttpBinding` i REST w WCF.Poniżej kod customowego endpoint behavior, który podmienia formatter na `SystemTextJsonFormatter` dla wszystkich operacji endpointu:

```csharp
using System;
using System.Linq;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.ServiceModel.Channels;

public class SystemTextJsonEndpointBehavior : Attribute, IEndpointBehavior
{
    public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
    {
        // brak implementacji
    }

    public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
    {
        // brak implementacji klienta
    }

    public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
    {
        var contract = endpoint.Contract;
        foreach (var operation in contract.Operations)
        {
            var dispatchOperation = endpointDispatcher.DispatchRuntime.Operations
                .FirstOrDefault(op => op.Name == operation.Name);

            if (dispatchOperation != null)
            {
                var requestType = operation.Messages[0].Body.Parts.Count > 0
                    ? operation.Messages[0].Body.Parts[0].Type
                    : typeof(void);

                var responseType = operation.Messages[1].Body.ReturnValue != null
                    ? operation.Messages[1].Body.ReturnValue.Type
                    : typeof(void);

                dispatchOperation.Formatter = new SystemTextJsonFormatter(requestType, responseType);
            }
        }
    }

    public void Validate(ServiceEndpoint endpoint)
    {
        // opcjonalna walidacja
    }
}
```


### Dodanie w `app.config`:

```xml
<system.serviceModel>
  <behaviors>
    <endpointBehaviors>
      <behavior name="JsonEndpointBehavior">
        <yourNamespace:SystemTextJsonEndpointBehavior />
      </behavior>
    </endpointBehaviors>
  </behaviors>

  <services>
    <service name="YourNamespace.YourService">
      <endpoint address="" binding="webHttpBinding" contract="YourNamespace.IYourServiceContract"
                behaviorConfiguration="JsonEndpointBehavior" />
    </service>
  </services>
</system.serviceModel>
```

- Zamień `yourNamespace` na rzeczywistą przestrzeń nazw klasy `SystemTextJsonEndpointBehavior`.
- Usuń lub nie stosuj `webHttpBehavior` jednocześnie, aby uniknąć konfliktów.

Dzięki temu konfigurujesz formatter na poziomie endpointu, co jest zalecane przy `webHttpBinding` i REST w WCF.

