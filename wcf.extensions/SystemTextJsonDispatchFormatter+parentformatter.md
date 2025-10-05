Problem niemapowania parametrów z UriTemplate w customowym SystemTextJsonDispatchFormatter wynika z tego, że WCF standardowo rozdziela parametry z adresu (URI) i ciała (Body). Domyślny mechanizm deserializacji URI jest wykonywany przez standardowy `UriTemplateDispatchFormatter`, który odczytuje i mapuje parametry z UriTemplate do tablicy parametrów metody, a osobno deserializuje JSON z body.

### Najczęstszy problem w custom DispatchFormatter:

Całą mechanikę deserializacji requestu przejmuje Twój formatter, więc jeśli nadpiszesz `DeserializeRequest` i nie uwzględnisz istniejących parametrów UriTemplate, to parametry z URI nie zostaną wypełnione i pozostaną null.

### Jak rozwiązać ten problem?

1. **Przechowuj oryginalny domyślny `IDispatchMessageFormatter` (np. `UriTemplateDispatchFormatter`) jako pole.**
2. W metodzie `DeserializeRequest` wywołaj oryginalny formatter, by rozpakował parametry UriTemplate **z wyjątkiem** tego parametru, który jest czytany z body JSON.
3. Następnie, zdekoduj ręcznie JSON body z wiadomości przy pomocy `System.Text.Json` i uzupełnij ostatni (lub odpowiedni) parametr tablicy `parameters`.
4. W skrócie, wywołujesz domyślną deserializację pod kątem URI, a body deserializujesz samemu.

***

### Przykładowa implementacja fragmentu `DeserializeRequest`

```csharp
public class SystemTextJsonDispatchFormatter : IDispatchMessageFormatter
{
    private readonly IDispatchMessageFormatter _parentFormatter;
    private readonly OperationDescription _operationDescription;

    public SystemTextJsonDispatchFormatter(IDispatchMessageFormatter parentFormatter, OperationDescription operationDescription)
    {
        _parentFormatter = parentFormatter;
        _operationDescription = operationDescription;
    }

    public void DeserializeRequest(Message message, object[] parameters)
    {
        // Domyślna deserializacja URI - wszystkich parametrów oprócz tego z body (ostatni)
        if (parameters.Length > 1)
        {
            object[] uriParams = new object[parameters.Length - 1];
            _parentFormatter.DeserializeRequest(message, uriParams);

            // kopiujemy wypełnione URI parametry
            Array.Copy(uriParams, parameters, uriParams.Length);
        }
        else
        {
            // Jeśli tylko 1 parametr (body), wywołaj domyślną lub zignoruj
            _parentFormatter.DeserializeRequest(message, parameters);
        }

        // Manualne deserializowanie ciała JSON (ostatni parametr)
        var jsonParamIndex = parameters.Length - 1;
        if (jsonParamIndex >= 0)
        {
            var bodyType = _operationDescription.Messages[^0].Body.Parts.Last().Type;
            using (var bodyReader = message.GetReaderAtBodyContents())
            {
                string json = bodyReader.ReadOuterXml(); // lub lepsza metoda na pobranie surowego JSON z wiadomości
                parameters[jsonParamIndex] = JsonSerializer.Deserialize(json, bodyType);
            }
        }
    }

    public Message SerializeReply(MessageVersion messageVersion, object[] parameters, object result)
    {
        // Twoja logika serializacji odpowiedzi z System.Text.Json
        throw new NotImplementedException();
    }
}
```

Kluczowe jest tutaj:

- Wywołanie `_parentFormatter.DeserializeRequest` dla parametrów UriTemplate.
- Ręczne zdeserializowanie części ciała JSON, tylko dla odpowiedniego parametru.

***

### Podsumowanie:

- Trzymaj w polu oryginalny formatter, by użyć go do deserializacji UriTemplate.
- Nadpisz/deserializuj ręcznie w body JSON, tylko ten parametr w argumencie metody.
- W ten sposób zachowasz obsługę zarówno parametrów w Uri (adres URL), jak i JSON w body bez konfliktu i z konieczną customizacją serializacji.

Więcej tego podejścia i fragmentów znajdziesz w dyskusji i przykładach typu: "WCF custom DispatchMessageFormatter with UriTemplate and JSON body". Czy warto przygotować pełniejszy przykład tego wzorca?Problem z mapowaniem parametrów UriTemplate przy własnym SystemTextJsonDispatchFormatter wynika z tego, że nadpisując DeserializeRequest, musisz ręcznie obsłużyć parametry URL oraz ciało. Standardowo WCF rozdziela te parametry i mapuje URL przez UriTemplateDispatchFormatter.[^1][^2][^3]

Rozwiązanie to:

- Zachowaj w polu domyślny IDispatchMessageFormatter (np. UriTemplateDispatchFormatter).
- W DeserializeRequest najpierw wywołaj ten formatter dla wszystkich parametrów oprócz tego z body (czyli np. ostatni parametr pomiń).
- Następnie ręcznie zdekoduj JSON z body (wykorzystując System.Text.Json) i uzupełnij w tablicy parametrów ten ostatni element.

Przykład:

```csharp
public class SystemTextJsonDispatchFormatter : IDispatchMessageFormatter
{
    private readonly IDispatchMessageFormatter _parentFormatter;
    private readonly OperationDescription _operationDescription;

    public SystemTextJsonDispatchFormatter(IDispatchMessageFormatter parentFormatter, OperationDescription operationDescription)
    {
        _parentFormatter = parentFormatter;
        _operationDescription = operationDescription;
    }

    public void DeserializeRequest(Message message, object[] parameters)
    {
        if (parameters.Length > 1)
        {
            object[] uriParams = new object[parameters.Length - 1];
            _parentFormatter.DeserializeRequest(message, uriParams);
            Array.Copy(uriParams, parameters, uriParams.Length);
        }
        else
        {
            _parentFormatter.DeserializeRequest(message, parameters);
        }

        // Deserialize JSON body into last parameter
        var bodyType = _operationDescription.Messages[^0].Body.Parts.Last().Type;
        using (var bodyReader = message.GetReaderAtBodyContents())
        {
            var json = bodyReader.ReadOuterXml(); // trzeba się upewnić, że pobiera rzeczywisty JSON
            parameters[parameters.Length - 1] = JsonSerializer.Deserialize(json, bodyType);
        }
    }

    public Message SerializeReply(MessageVersion messageVersion, object[] parameters, object result)
    {
        // Implementacja serializacji odpowiedzi
        throw new NotImplementedException();
    }
}
```

Taki wzorzec pozwala utrzymać zgodność mapowania parametrów URI przez domyślny formatter, a JSON body deserializować zgodnie z własnym System.Text.Json. Dokładne pobieranie surowego JSON z `message.GetReaderAtBodyContents()` może wymagać dopracowania.

Więcej na ten temat na blogach i dyskusjach o custom DispatchFormatter z obsługą UriTemplate i JSON body.[^2][^3][^1]
<span style="display:none">[^10][^11][^12][^13][^14][^15][^16][^17][^18][^19][^20][^4][^5][^6][^7][^8][^9]</span>

<div align="center">⁂</div>

[^1]: https://www.rhyous.com/2019/04/12/how-to-replace-wcf-serialization-with-json-net-without-wrapping-and-with-uritemplate-support/

[^2]: https://stackoverflow.com/questions/33554997/using-custom-wcf-body-deserialization-without-changing-uri-template-deserializat

[^3]: https://learn.microsoft.com/en-us/dotnet/framework/wcf/samples/operation-formatter-and-operation-selector

[^4]: https://stackoverflow.com/questions/544038/why-is-this-wcf-service-not-recognizing-the-uritemplate-parameter

[^5]: https://learn.microsoft.com/en-us/dotnet/framework/wcf/feature-details/uritemplate-and-uritemplatetable

[^6]: https://learn.microsoft.com/en-us/dotnet/framework/wcf/feature-details/create-a-service-arbitrary-data-using-wcf

[^7]: https://dotnetfreakblog.wordpress.com/2015/07/04/2501/

[^8]: https://njrathod.wordpress.com/2016/11/28/how-to-create-wcf-application-which-accepts-parameter-in-json-format/

[^9]: https://debugmode.net/2010/06/02/urirest1/

[^10]: https://stackoverflow.com/questions/8214980/deserialize-a-string-from-the-uri-template-to-an-operation-type-parameter?rq=3

[^11]: https://www.c-sharpcorner.com/blogs/how-to-pass-parameters-to-wcf-rest-service-method-and-consuming-in-website1

[^12]: https://stackoverflow.com/questions/7710091/simple-wcf-post-with-uri-template

[^13]: https://learn.microsoft.com/en-us/dotnet/framework/wcf/feature-details/wcf-web-http-programming-model-overview

[^14]: https://www.codeproject.com/articles/Building-RESTful-Message-Based-Web-Services-with-W

[^15]: https://learn.microsoft.com/en-us/archive/msdn-magazine/2009/january/service-station-an-introduction-to-restful-services-with-wcf

[^16]: https://www.codemag.com/article/080014/REST-Based-Ajax-Services-with-WCF-in-.NET-3.5

[^17]: https://www.productiverage.com/wcf-with-json-and-nullable-types

[^18]: https://ikriv.com/blog/?p=1841

[^19]: https://www.youtube.com/watch?v=IT3bNLNwMuE

[^20]: https://www.codeproject.com/articles/Simple-Demo-WCF-RESTful-Web-Service

