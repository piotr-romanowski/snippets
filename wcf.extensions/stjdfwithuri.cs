using System;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.Text;
using System.Text.Json;
using System.Xml;

public class SystemTextJsonDispatchFormatter : IDispatchMessageFormatter
{
    private readonly OperationDescription _operationDescription;

    public SystemTextJsonDispatchFormatter(OperationDescription operationDescription)
    {
        _operationDescription = operationDescription ?? throw new ArgumentNullException(nameof(operationDescription));
    }

    public void DeserializeRequest(Message message, object[] parameters)
    {
        if (parameters == null)
            throw new ArgumentNullException(nameof(parameters));

        // Pobierz dopasowanie URI z właściwości message
        if (!message.Properties.TryGetValue(UriTemplateMatchResultsProperty.Name, out var matchObj) ||
            !(matchObj is UriTemplateMatch uriMatch))
        {
            throw new InvalidOperationException("Brak UriTemplateMatch w Message.Properties");
        }

        var boundVariables = uriMatch.BoundVariables; // Nazwa -> string

        // Pobierz parametry opisane w kontrakcie z Request MessageParts
        var paramParts = _operationDescription.Messages[0].Body.Parts;

        // Jeśli nie ma żadnych parametrów, zakończ (brak argumentów w metodzie)
        if (paramParts.Count == 0)
            return;

        // W WCF REST: tylko maksymalnie jeden parametr z body (JSON)
        // Znajdź czy istnieje parametr, który nie występuje w boundVariables (czyli z body)
        int? bodyParamIndex = null;
        for (int i = 0; i < paramParts.Count; i++)
        {
            if (!boundVariables.ContainsKey(paramParts[i].Name))
            {
                bodyParamIndex = paramParts[i].Index;
                break;
            }
        }

        // Deserializuj parametry z URI do tablicy results
        foreach (var kvp in boundVariables)
        {
            // Znajdź index parametru o tej nazwie
            var param = paramParts.FirstOrDefault(p => p.Name == kvp.Key);
            if (param != null)
            {
                int idx = param.Index;
                parameters[idx] = ConvertParameter(kvp.Value, param.Type);
            }
        }

        // Deserializuj JSON z body gdy parametr body istnieje
        if (bodyParamIndex.HasValue)
        {
            var bodyPart = paramParts.First(p => p.Index == bodyParamIndex.Value);
            string json = ReadMessageBodyAsString(message);

            if (string.IsNullOrWhiteSpace(json))
            {
                parameters[bodyParamIndex.Value] = GetDefaultValue(bodyPart.Type);
            }
            else
            {
                parameters[bodyParamIndex.Value] = JsonSerializer.Deserialize(json, bodyPart.Type);
            }
        }
    }

    public Message SerializeReply(MessageVersion messageVersion, object[] parameters, object result)
    {
        string json = JsonSerializer.Serialize(result);

        var message = Message.CreateMessage(messageVersion, null, new RawBodyWriter(json));
        message.Properties.Add(WebBodyFormatMessageProperty.Name, new WebBodyFormatMessageProperty(WebContentFormat.Raw));
        var httpResponse = new HttpResponseMessageProperty
        {
            StatusCode = HttpStatusCode.OK
        };
        httpResponse.Headers[HttpResponseHeader.ContentType] = "application/json";
        message.Properties.Add(HttpResponseMessageProperty.Name, httpResponse);

        return message;
    }

    private string ReadMessageBodyAsString(Message message)
    {
        MessageBuffer buffer = message.CreateBufferedCopy(int.MaxValue);
        Message copy = buffer.CreateMessage();

        using (var reader = copy.GetReaderAtBodyContents())
        {
            return reader.ReadOuterXml();
        }
    }

    private static object ConvertParameter(string value, Type targetType)
    {
        if (targetType == typeof(string))
            return value;

        if (string.IsNullOrEmpty(value))
            return GetDefaultValue(targetType);

        if (targetType.IsEnum)
            return Enum.Parse(targetType, value, ignoreCase: true);

        try
        {
            return Convert.ChangeType(value, targetType);
        }
        catch
        {
            // konwersja się nie powiodła, zwróć domyślną wartość
            return GetDefaultValue(targetType);
        }
    }

    private static object GetDefaultValue(Type t)
    {
        if (!t.IsValueType) return null;
        return Activator.CreateInstance(t);
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