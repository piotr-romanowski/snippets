
# w bindingu mam webmessageencoding a w nim ContentTypeMapper - on nie jest ustawiony - może cos powinno tam byc ?

Tak, to bardzo ważne — `ContentTypeMapper` w `WebMessageEncodingBindingElement` decyduje o tym, jak WCF interpretuje nagłówek `Content-Type` nadchodzącego żądania HTTP i wybiera właściwy serializer.

### Rola `ContentTypeMapper`:

- Domyślny `ContentTypeMapper` w WCF rozpoznaje standardowe typy `Content-Type` (np. `application/xml`, `application/json`).
- Jeśli nie ustawisz własnego mappera, dla nieznanych lub niestandardowych typów content-type WCF może błędnie zakładać XML lub inną obsługę.
- Aby wymusić obsługę JSON zgodnie z Twoim formatterem, warto zastosować własny implementację `WebContentTypeMapper`.


### Przykład prostego `ContentTypeMapper` wymuszającego JSON

```csharp
using System.ServiceModel.Channels;

public class JsonOnlyContentTypeMapper : WebContentTypeMapper
{
    public override WebContentFormat GetMessageFormatForContentType(string contentType)
    {
        if (contentType != null && contentType.Contains("json", System.StringComparison.OrdinalIgnoreCase))
        {
            return WebContentFormat.Json;
        }
        // wszystko inne traktuj jako JSON (możesz zmienić na Raw albo Text jeśli chcesz)
        return WebContentFormat.Json;
    }
}
```


### Jak podpiąć `ContentTypeMapper` w bindingu programowo:

```csharp
var webHttpBinding = new WebHttpBinding();
var customBinding = new CustomBinding(webHttpBinding);

// Znajdź webMessageEncodingBindingElement i podmień jego mapper
var webMessageEncoding = customBinding.Elements.Find<WebMessageEncodingBindingElement>();
if (webMessageEncoding != null)
{
    webMessageEncoding.ContentTypeMapper = new JsonOnlyContentTypeMapper();
}

host.AddServiceEndpoint(typeof(IMyContract), customBinding, "");
```


### A w `app.config` niestety nie da się tego ustawić bezpośrednio — `ContentTypeMapper` trzeba podpiąć programowo.


***

### Podsumowanie

- Brak `ContentTypeMapper` lub użycie domyślnego to częsta przyczyna, dla której WCF nie rozpoznaje JSON i próbuje parsować XML.
- Własny `ContentTypeMapper` ustawiony w `WebMessageEncodingBindingElement` pozwala wymusić traktowanie requestów jako JSON na podstawie nagłówka `Content-Type`.
- W .NET Framework konfiguracja tego elementu wymaga dodania w kodzie, nie w `app.config`.

To jest bardzo ważny krok, który może rozwiązać Twój problem z XML zamiast JSON przy deserializacji. Jeśli chcesz, mogę pomóc wygenerować cały przykład ze stworzeniem hosta i tym mapperem.Tak, to kluczowy element. `ContentTypeMapper` w `WebMessageEncodingBindingElement` decyduje, jak WCF interpretuje nagłówek `Content-Type` w żądaniu HTTP i wybiera formatter.[^1][^2]

Domyślny mapper może nie rozpoznawać Twojego JSON albo traktować format jako XML, powodując błędy przy deserializacji.

Dobrą praktyką jest ustawienie własnego `ContentTypeMapper`, który będzie wymuszał interpretację `application/json` (i innych dopuszczalnych) jako formatu JSON. Przykład prostego mappera:

```csharp
using System.ServiceModel.Channels;

public class JsonOnlyContentTypeMapper : WebContentTypeMapper
{
    public override WebContentFormat GetMessageFormatForContentType(string contentType)
    {
        if (!string.IsNullOrEmpty(contentType) && contentType.IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0)
            return WebContentFormat.Json;
        return WebContentFormat.Json; // lub Raw, zależnie od potrzeb
    }
}
```

Aby podpiąć `ContentTypeMapper`, w .NET Framework trzeba zmodyfikować `CustomBinding` programowo, np.:

```csharp
var webHttpBinding = new WebHttpBinding();
var customBinding = new CustomBinding(webHttpBinding);

var webMessageEncoding = customBinding.Elements.Find<WebMessageEncodingBindingElement>();
if (webMessageEncoding != null)
{
    webMessageEncoding.ContentTypeMapper = new JsonOnlyContentTypeMapper();
}

host.AddServiceEndpoint(typeof(IMyContract), customBinding, "");
```

W `app.config` niestety ustawienie `ContentTypeMapper` nie jest bezpośrednio możliwe.

Podsumowując, jeśli nie ustawisz `ContentTypeMapper`, WCF może błędnie rozpoznawać format requestu jako XML i próbować go tak deserializować. Dodanie własnego `ContentTypeMapper` to klucz do poprawnej obsługi JSON.

Jeśli chcesz, mogę pomóc w wygenerowaniu pełnego gotowego przykładu z tym mapperem i bindingiem.

<div align="center">⁂</div>

[^1]: https://learn.microsoft.com/en-us/dotnet/framework/configure-apps/file-schema/wcf/webhttp

[^2]: https://learn.microsoft.com/en-us/dotnet/api/system.servicemodel.description.webhttpbehavior?view=netframework-4.8.1

