
Oto pełna implementacja klasy `SystemTextJsonDispatchFormatter`, która:

- Przechowuje domyślny formatter (np. `UriTemplateDispatchFormatter`) do obsługi parametrów z UriTemplate (adresu).
- Dynamicznie rozpoznaje, który parametr pochodzi z Body na podstawie `OperationDescription`.
- W metodzie `DeserializeRequest` najpierw wywołuje domyślną deserializację parametrów UriTemplate,
- Następnie samodzielnie deserializuje parametr Body przy pomocy System.Text.Json,
- W metodzie `SerializeReply` serializuje wynik odpowiedzi do JSON przy pomocy System.Text.Json.

```csharp
using System;
using System.IO;
using System.Linq;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.Text;
using System.Text.Json;
using System.Xml;

public class SystemTextJsonDispatchFormatter : IDispatchMessageFormatter
{
    private readonly IDispatchMessageFormatter _defaultFormatter;
    private readonly OperationDescription _operationDescription;
    private int? _bodyParameterIndex;
    private Type _bodyParameterType;

    public SystemTextJsonDispatchFormatter(IDispatchMessageFormatter defaultFormatter, OperationDescription operationDescription)
    {
        _defaultFormatter = defaultFormatter ?? throw new ArgumentNullException(nameof(defaultFormatter));
        _operationDescription = operationDescription ?? throw new ArgumentNullException(nameof(operationDescription));

        DiscoverBodyParameter();
    }

    /// <summary>
    /// Znajduje parametr reprezentujący ciało wiadomości w opisie operacji.
    /// </summary>
    private void DiscoverBodyParameter()
    {
        // Wiadomość req to index 0
        var bodyParts = _operationDescription.Messages[0].Body.Parts;

        if (bodyParts.Count == 0)
        {
            _bodyParameterIndex = null;
            _bodyParameterType = null;
            return;
        }

        // Znajdujemy parametr, który oznaczony jest jako body (IsBody==true)
        var bodyPart = bodyParts.FirstOrDefault(p => p.IsBody);

        if (bodyPart == null)
        {
            _bodyParameterIndex = null;
            _bodyParameterType = null;
            return;
        }

        // W OperationDescription parametry metody są w kolejności MessageBodyDescription.Index
        // Szukamy parametru o takim samym indeksie
        _bodyParameterIndex = bodyPart.Index;
        _bodyParameterType = bodyPart.Type;
    }

    public void DeserializeRequest(Message message, object[] parameters)
    {
        if (parameters == null)
            throw new ArgumentNullException(nameof(parameters));

        if (_bodyParameterIndex.HasValue)
        {
            // Tworzymy tablicę dla parametrów URI (wszystkie poza body)
            if (parameters.Length > 1)
            {
                object[] uriParams = new object[parameters.Length];
                // Wyczyść wartość parametru body, żeby go pominąć w wywołaniu defaultFormatter
                // Domyślny formatter nie powinien deserializować tego parametru
                uriParams[_bodyParameterIndex.Value] = GetDefaultValue(_bodyParameterType);

                // Domyślna deserializacja parametrów URI
                _defaultFormatter.DeserializeRequest(message, uriParams);

                // Kopiujemy deserializowane parametry URI do parametrów metody (pomijamy body)
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (i != _bodyParameterIndex.Value)
                        parameters[i] = uriParams[i];
                }
            }
            else
            {
                // Tylko 1 parametr (body), zignoruj domyślny formatter
            }

            // Teraz pobierz JSON z ciała i zdeserializuj do parameteru body
            var json = ReadMessageBodyAsString(message);

            if (string.IsNullOrWhiteSpace(json))
            {
                parameters[_bodyParameterIndex.Value] = GetDefaultValue(_bodyParameterType);
            }
            else
            {
                parameters[_bodyParameterIndex.Value] = JsonSerializer.Deserialize(json, _bodyParameterType);
            }
        }
        else
        {
            // Brak parametru body, wszystko domyślnie
            _defaultFormatter.DeserializeRequest(message, parameters);
        }
    }

    public Message SerializeReply(MessageVersion messageVersion, object[] parameters, object result)
    {
        string json = JsonSerializer.Serialize(result);

        var message = Message.CreateMessage(messageVersion, null, new RawBodyWriter(json));

        message.Properties.Add(WebBodyFormatMessageProperty.Name, new WebBodyFormatMessageProperty(WebContentFormat.Raw));
        var httpResponse = new HttpResponseMessageProperty
        {
            StatusCode = System.Net.HttpStatusCode.OK
        };
        httpResponse.Headers[HttpResponseHeader.ContentType] = "application/json";
        message.Properties.Add(HttpResponseMessageProperty.Name, httpResponse);

        return message;
    }

    /// <summary>
    /// Odczytuje całe body wiadomości jako string JSON.
    /// </summary>
    private string ReadMessageBodyAsString(Message message)
    {
        // Buforujemy Message, bo w WCF jest jednokrotne czytanie
        MessageBuffer buffer = message.CreateBufferedCopy(int.MaxValue);
        Message copy = buffer.CreateMessage();

        var reader = copy.GetReaderAtBodyContents();

        // Odczytujemy JSON jako string
        reader.ReadStartElement(); // start elementu body
        string json = reader.ReadOuterXml();
        return json;
    }

    /// <summary>
    /// Zwraca domyślną wartość typu (null dla referencji, default dla wartości)
    /// </summary>
    private static object GetDefaultValue(Type t)
    {
        if (!t.IsValueType) return null; // referencja
        return Activator.CreateInstance(t); // value type
    }

    class RawBodyWriter : BodyWriter
    {
        private readonly string _json;

        public RawBodyWriter(string json) : base(true)
        {
            _json = json ?? throw new ArgumentNullException(nameof(json));
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(_json);
            writer.WriteStartElement("Binary");
            writer.WriteBase64(bytes, 0, bytes.Length);
            writer.WriteEndElement();
        }
    }
}
```


***

### Jak używać:

- W konstruktorze podajesz oryginalny formatter np.:

```csharp
var defaultFormatter = new UriTemplateDispatchFormatter(operationDescription);
var formatter = new SystemTextJsonDispatchFormatter(defaultFormatter, operationDescription);
```

- Rejestrujesz swój formatter w custom behavior na ServiceHost.

***

### Co robi ta klasa:

- Znajduje _dynamicznie_ który parametr z metody jest body.
- Dla pozostałych (UriTemplate) używa domyślnego deserializatora.
- Body deserializuje ręcznie z JSON za pomocą System.Text.Json.
- Serializuje odpowiedź do JSON.
- Obsługuje parametry w dowolnej kolejności i dowolny układ w kontrakcie.

Jeśli chcesz, mogę pomóc też z przykładowym użyciem lub z rejestracją tego formattera.

