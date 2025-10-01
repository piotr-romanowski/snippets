
Własny formatter dla WCF, który używa `System.Text.Json` i działa w .NET Framework 4.7.2, a ponadto obsługuje całą wiadomość, a nie tylko typy `DateTime`. Biblioteka `System.Text.Json` jest dostępna w .NET Framework 4.7.2 poprzez pakiet NuGet.[^1][^2]

### Przykład własnego formattera z System.Text.Json

Poniżej znajduje się kompletny przykład niestandardowego formattera dla WCF, który serializuje i deserializuje całą wiadomość JSON za pomocą `System.Text.Json`, z obsługą `DateTime` w formacie ISO 8601 i zachowaniem kompatybilności typów.

```csharp
using System;
using System.IO;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;
using System.Text;
using System.Text.Json;

public class SystemTextJsonFormatter : IDispatchMessageFormatter
{
    private readonly Type _requestType;
    private readonly Type _responseType;
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, // opcjonalne: np. dla kompatybilności z frontendem
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public SystemTextJsonFormatter(Type requestType, Type responseType)
    {
        _requestType = requestType;
        _responseType = responseType;
    }

    public void DeserializeRequest(Message message, object[] parameters)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));
        if (parameters == null) throw new ArgumentNullException(nameof(parameters));

        var body = message.GetBody<string>();
        if (string.IsNullOrEmpty(body))
        {
            parameters[^0] = Activator.CreateInstance(_requestType);
            return;
        }

        var obj = JsonSerializer.Deserialize(body, _requestType, JsonOptions);
        parameters[^0] = obj ?? Activator.CreateInstance(_requestType);
    }

    public Message SerializeReply(MessageVersion messageVersion, object[] parameters, object result)
    {
        var json = JsonSerializer.Serialize(result, _responseType, JsonOptions);
        var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var message = Message.CreateMessage(messageVersion, null, new JsonBodyWriter(ms));
        message.Properties.Add(WebBodyFormatMessageProperty.Name, new WebBodyFormatMessageProperty(WebContentFormat.Json));
        message.Properties.ContentType = new System.Net.Mime.ContentType("application/json");
        return message;
    }

    private class JsonBodyWriter : BodyWriter
    {
        private readonly MemoryStream _stream;

        public JsonBodyWriter(MemoryStream stream) : base(true) => _stream = stream;

        protected override void OnWriteBodyContents(System.Xml.XmlDictionaryWriter writer)
        {
            _stream.Position = 0;
            using var sr = new StreamReader(_stream, Encoding.UTF8, false, 1024, true);
            var json = sr.ReadToEnd();
            writer.WriteRaw(json);
        }
    }
}
```


### Jak zarejestrować formatter w WCF

Można to zrobić przez atrybut operacji:

```csharp
public class JsonOperationBehavior : Attribute, IOperationBehavior
{
    public void ApplyDispatchBehavior(OperationDescription operationDescription, DispatchOperation dispatchOperation)
    {
        var requestType = operationDescription.Messages[^0].Body.Parts[^0]?.Type;
        var responseType = operationDescription.Messages[^1].Body.ReturnValue?.Type;

        dispatchOperation.Formatter = new SystemTextJsonFormatter(requestType, responseType);
    }

    public void AddBindingParameters(OperationDescription operationDescription, BindingParameterCollection bindingParameters) { }
    public void ApplyClientBehavior(OperationDescription operationDescription, ClientOperation clientOperation) { }
    public void Validate(OperationDescription operationDescription) { }
}
```

Użycie w kontrakcie:

```csharp
[ServiceContract]
public interface IMyService
{
    [OperationContract]
    [JsonOperationBehavior]
    MyResponse GetData(MyRequest request);
}
```


### Wymagania

- Dodaj pakiet NuGet: `System.Text.Json` (wspiera .NET Framework 4.7.2).[^3][^2]
- Upewnij się, że `DataContractJsonSerializer` nie jest używany — ten formatter zastępuje go całkowicie.
- Formatter działa na całej wiadomości, więc obsługuje wszystkie typy danych, nie tylko `DateTime`.


### Zalety

- Format `DateTime` w ISO 8601 (np. `"2025-10-01T20:26:00Z"`), kompatybilny z .NET 8.
- Obsługuje całą strukturę obiektów, w tym zagnieżdżone typy, listy, nullables.
- Lepsza wydajność i mniejsze alokacje niż `Newtonsoft.Json`.[^2]

Takie podejście zapewnia pełną kontrolę nad serializacją JSON w WCF i gwarantuje kompatybilność z nowoczesnymi klientami .NET 8.[^4][^5][^2]
<span style="display:none">[^10][^11][^12][^13][^14][^15][^16][^17][^18][^19][^20][^21][^22][^23][^24][^25][^26][^27][^28][^29][^30][^31][^32][^6][^7][^8][^9]</span>

<div align="center">⁂</div>

[^1]: https://learn.microsoft.com/en-us/answers/questions/65502/which-net-framework-needed-to-use-system-text-json

[^2]: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/overview

[^3]: https://www.nuget.org/packages/System.Text.Json/4.7.2

[^4]: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/converters-how-to

[^5]: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/migrate-from-newtonsoft

[^6]: https://stackoverflow.com/questions/60318422/net-4-7-2-system-text-json-missing-dependency

[^7]: https://github.com/dotnet/docs/issues/35113

[^8]: https://learn.microsoft.com/en-us/dotnet/framework/wcf/feature-details/serializing-in-json-with-message-level-programming

[^9]: https://stackoverflow.com/questions/59595684/how-do-i-fix-the-error-could-not-load-file-or-assembly-system-text-json

[^10]: https://love2dev.com/blog/creating-a-wcf-service-for-json/

[^11]: https://www.rhyous.com/2019/04/12/how-to-replace-wcf-serialization-with-json-net-without-wrapping-and-with-uritemplate-support/

[^12]: https://github.com/dotnet/runtime/issues/107271

[^13]: https://thecodebuzz.com/serialization-and-deserialization-using-system-text-json/

[^14]: https://www.youtube.com/watch?v=4pqHLyhnuUY

[^15]: https://makolyte.com/system-text-json-how-to-customize-serialization-with-jsonconverter/

[^16]: https://www.reddit.com/r/dotnet/comments/14xgfjl/should_you_use_newtonsoftjson_or_systemtextjson/

[^17]: https://www.nuget.org/packages/system.text.json/

[^18]: https://stackoverflow.com/questions/59503393/is-using-the-system-text-json-no-longer-possible-for-serializing-deserialiing

[^19]: https://stackoverflow.com/questions/9679671/implement-json-net-in-wcf-message-formatter

[^20]: https://docs.servicestack.net/system-text-json

[^21]: https://weblog.west-wind.com/posts/2016/apr/02/custom-message-formatting-in-wcf-to-add-all-namespaces-to-the-soap-envelope

[^22]: https://stackoverflow.com/questions/12559337/determining-parameter-types-of-wcf-method-when-using-idispatchmessageformatter-d

[^23]: https://learn.microsoft.com/pl-pl/visualstudio/data-tools/walkthrough-creating-a-simple-wcf-service-in-windows-forms?view=vs-2022

[^24]: https://learn.microsoft.com/pl-pl/dotnet/api/system.servicemodel.dispatcher.idispatchmessageformatter?view=netframework-4.8.1

[^25]: https://blog.ostebaronen.dk/2025/01/jsonserializercontext.html

[^26]: https://www.nuget.org/packages/System.Text.json

[^27]: https://www.youtube.com/watch?v=isdfIIYGH-E

[^28]: https://stackoverflow.com/questions/tagged/.net-framework-version

[^29]: https://thecodebuzz.com/system-text-json-create-a-stringconverter-json-serialization/

[^30]: https://khalidabuhakmeh.com/aspnet-core-6-mvc-upgrade-systemtextjson-serialization-issues

[^31]: https://makolyte.com/system-text-json-using-anonymous-types-to-customize-serialization/

[^32]: https://www.couchbase.com/forums/t/use-system-text-json-as-defaultserializer/28518




### Behavior serwisu podmieniający formatter na wszystkich operacjach

```csharp
using System;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;

public class SystemTextJsonServiceBehavior : IServiceBehavior
{
    public void AddBindingParameters(ServiceDescription serviceDescription, System.ServiceModel.ServiceHostBase serviceHostBase, System.Collections.ObjectModel.Collection<ServiceEndpoint> endpoints, System.ServiceModel.Channels.BindingParameterCollection bindingParameters)
    {
        // Nie potrzebujemy dodawać parametrów
    }

    public void ApplyDispatchBehavior(ServiceDescription serviceDescription, System.ServiceModel.ServiceHostBase serviceHostBase)
    {
        foreach (var endpoint in serviceDescription.Endpoints)
        {
            foreach (var operation in endpoint.Contract.Operations)
            {
                var dispatchOperation = serviceHostBase.ChannelDispatchers
                    .OfType<System.ServiceModel.Dispatcher.ChannelDispatcher>()
                    .SelectMany(cd => cd.Endpoints)
                    .SelectMany(ed => ed.DispatchRuntime.Operations)
                    .FirstOrDefault(op => op.Name == operation.Name);

                if (dispatchOperation != null)
                {
                    var dataType = operation.Messages[^1].Body.Parts[^0].Type;
                    dispatchOperation.Formatter = new SystemTextJsonFormatter(dataType);
                }
            }
        }
    }

    public void Validate(ServiceDescription serviceDescription, System.ServiceModel.ServiceHostBase serviceHostBase)
    {
        // Można dodać walidacje jeśli potrzeba
    }
}
```


### Jak dodać behavior do ServiceHost?

W kodzie konfigurującym serwis (np. w Main lub inicjalizacji serwisu):

```csharp
var host = new ServiceHost(typeof(YourService));
host.Description.Behaviors.Add(new SystemTextJsonServiceBehavior());
host.Open();
```


### Co to robi?

Behavior `SystemTextJsonServiceBehavior` przechodzi po wszystkich endpointach i operacjach serwisu i podmienia domyślny formatter na `SystemTextJsonFormatter`, który używa System.Text.Json do serializacji i deserializacji całych wiadomości JSON, zapewniając kompatybilność z .NET 8 i nowoczesnym formatem dat (ISO 8601).

To rozwiązanie działa globalnie dla całego serwisu, więc nie trzeba dekorować poszczególnych metod.

Chętnie dopomogę z implementacją lub dostosowaniem jeśli będą pytania.[^1]

<div align="center">⁂</div>

[^1]: https://learn.microsoft.com/en-us/dotnet/framework/wcf/feature-details/stand-alone-json-serialization

