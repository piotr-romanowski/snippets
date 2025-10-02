<img src="https://r2cdn.perplexity.ai/pplx-full-logo-primary-dark%402x.png" style="height:64px;margin-right:32px"/>

# czy jest inne rozwiązanie które pozwoli podmienić tylko formatter

Tak, istnieje prostsze rozwiązanie, które pozwala podmienić **tylko formatter** JSON przy użyciu klasy `WebHttpBehavior` i bez konieczności reimplementacji całej logiki tego behawioru.

### Podejście: podklasa WebHttpBehavior z podmienionym formatterem

Możesz utworzyć własną klasę dziedziczącą po `WebHttpBehavior`, która nadpisze metody `GetRequestDispatchFormatter` i `GetReplyDispatchFormatter` tak, aby zwracały Twój `SystemTextJsonFormatter`, a cały pozostały mechanizm REST (selector operacji, obsługa HTTP verbs, błędów itd.) pozostaje bez zmian.

### Przykład:

```csharp
using System;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;

public class CustomJsonWebHttpBehavior : WebHttpBehavior
{
    protected override IDispatchMessageFormatter GetRequestDispatchFormatter(OperationDescription operationDescription, ServiceEndpoint endpoint)
    {
        var requestType = operationDescription.Messages[^0].Body.Parts.Count > 0
            ? operationDescription.Messages[^0].Body.Parts[^0].Type
            : typeof(void);
        var responseType = operationDescription.Messages[^1].Body.ReturnValue != null
            ? operationDescription.Messages[^1].Body.ReturnValue.Type
            : typeof(void);

        return new SystemTextJsonFormatter(requestType, responseType);
    }

    protected override IDispatchMessageFormatter GetReplyDispatchFormatter(OperationDescription operationDescription, ServiceEndpoint endpoint)
    {
        var requestType = operationDescription.Messages[^0].Body.Parts.Count > 0
            ? operationDescription.Messages[^0].Body.Parts[^0].Type
            : typeof(void);
        var responseType = operationDescription.Messages[^1].Body.ReturnValue != null
            ? operationDescription.Messages[^1].Body.ReturnValue.Type
            : typeof(void);

        return new SystemTextJsonFormatter(requestType, responseType);
    }
}
```


### Zalety tego rozwiązania:

- Nie tracisz żadnej oryginalnej funkcjonalności `WebHttpBehavior`.
- Podmieniasz jedynie serializer JSON na swój własny formatter.
- Proste do zaimplementowania i do podpięcia przez konfigurację lub programowo.
- Kompatybilne z całą infrastrukturą REST w WCF.


### Użycie:

- W `app.config` zastąp `webHttpBehavior` swoim `CustomJsonWebHttpBehavior` (z zarejestrowaną przestrzenią nazw CLR) lub
- Dodaj programowo ten behavior do endpointu.

***

To jest standardowa, najbezpieczniejsza i najczęściej rekomendowana metoda, jeśli chcesz tylko zmienić sposób serializacji JSON w WCF REST bez przepisywania całej logiki `WebHttpBehavior`.

Jeśli chcesz, mogę pomóc z konfiguracją lub kodem do programowego podpięcia tej klasy zamiast oryginalnego `WebHttpBehavior`.[^1][^2]

<div align="center">⁂</div>

[^1]: https://learn.microsoft.com/en-us/dotnet/api/system.servicemodel.description.webhttpbehavior?view=netframework-4.8.1

[^2]: https://www.dotnetframework.org/default.aspx/WCF/WCF/3@5@30729@1/untmp/Orcas/SP/ndp/cdf/src/NetFx35/System@ServiceModel@Web/System/ServiceModel/Description/WebHttpBehavior@cs/2/WebHttpBehavior@cs

