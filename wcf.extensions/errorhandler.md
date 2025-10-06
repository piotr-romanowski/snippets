
# czy da sie to bard,ie generycznie zrobic? w sensie może być faultexception, webfaultexception

```
Tak, można zrobić bardziej generyczną obsługę wyjątków w `ProvideFault`, tak by rozpoznawać różne generyczne typy wyjątków (np. `WebFaultException<T>`, `FaultException<T>`, a nawet inne podobne), wyciągać ich generyczny argument `T` i serializować go do JSON automatycznie.
```


### Jak to zrobić?

- Sprawdzasz, czy wyjątek jest generyczny i czy jego typ jest zgodny z którymkolwiek z akceptowanych typów generycznych (np. `WebFaultException<>` lub `FaultException<>`),
- Wyciągasz typ generyczny (np. `ProblemDetails` lub inny typ błędu),
- Pobierasz z instancji obiektu ten generyczny argument (np. `Body` lub `Detail`),
- Serializujesz go do JSON.


### Przykład bardziej generycznej metody `ProvideFault`

```csharp
private static readonly Type[] SupportedGenericExceptionTypes = new[]
{
    typeof(WebFaultException<>),
    typeof(FaultException<>)
    // możesz tu dodać inne generyczne typy wyjątków, które chcesz obsłużyć
};

public void ProvideFault(Exception error, MessageVersion version, ref Message fault)
{
    object bodyObject = null;
    HttpStatusCode statusCode = HttpStatusCode.InternalServerError;

    Type errorType = error.GetType();

    // Sprawdź, czy to któryś z generycznych typów wyjątków
    if (errorType.IsGenericType && SupportedGenericExceptionTypes.Contains(errorType.GetGenericTypeDefinition()))
    {
        // Pobierz właściwość Body lub Detail z wyjątku
        var bodyProp = errorType.GetProperty("Body") ?? errorType.GetProperty("Detail");
        if (bodyProp != null)
        {
            bodyObject = bodyProp.GetValue(error);
        }

        // Pobierz status (jeśli istnieje)
        var statusProp = errorType.GetProperty("StatusCode") ?? errorType.GetProperty("Code");
        if (statusProp != null)
        {
            object statusValue = statusProp.GetValue(error);
            if (statusValue is HttpStatusCode code)
            {
                statusCode = code;
            }
            else if (statusValue is int intCode)
            {
                statusCode = (HttpStatusCode)intCode;
            }
        }
    }

    if (bodyObject == null)
    {
        // Domyślny ProblemDetails
        bodyObject = new ProblemDetails
        {
            Title = "Internal Server Error",
            Detail = error.Message,
            Status = (int)statusCode
        };
    }

    var json = System.Text.Json.JsonSerializer.Serialize(bodyObject);

    var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

    fault = Message.CreateMessage(version, null, new RawBodyWriter(stream));
    fault.Properties.Add(WebBodyFormatMessageProperty.Name, new WebBodyFormatMessageProperty(WebContentFormat.Raw));
    var httpResponse = new HttpResponseMessageProperty
    {
        StatusCode = statusCode
    };
    httpResponse.Headers[HttpResponseHeader.ContentType] = "application/json";
    fault.Properties.Add(HttpResponseMessageProperty.Name, httpResponse);
}

class RawBodyWriter : BodyWriter
{
    private readonly Stream _stream;

    public RawBodyWriter(Stream stream) : base(true)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
    {
        _stream.Position = 0;
        _stream.CopyTo(writer.BaseStream);
    }
}

public class ProblemDetails
{
    public int Status { get; set; }
    public string Title { get; set; }
    public string Detail { get; set; }
}
```


### Uwagi:

- W tablicy `SupportedGenericExceptionTypes` możesz dodać wszystkie typy generyczne, które chcesz obsłużyć.
- Metoda wykrywa, czy wyjątek jest generyczny i czy jego "definicja" jest jedną z dozwolonych.
- Używa refleksji, by wyciągnąć `Body` lub `Detail` jako payload do serializacji.
- Próbujemy też wyciągnąć kod statusu HTTP z właściwości `StatusCode` lub `Code`.
- Jeśli nie ma generycznego wyjątku, tworzymy prosty `ProblemDetails`.

To rozwiązanie jest elastyczne i łatwo rozszerzalne na różne typy wyjątków generycznych.

Jeśli chcesz mogę pomóc też z poniższą integracją lub testowym przykładem wywołania.

