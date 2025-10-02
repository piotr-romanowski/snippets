using System;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;
using System.Text;
using System.Text.Json;

public class SystemTextJsonFormatter : IDispatchMessageFormatter
{
    private readonly Type _requestType;
    private readonly Type _responseType;
    private readonly JsonSerializerOptions _jsonOptions;

    public SystemTextJsonFormatter(Type requestType, Type responseType)
    {
        _requestType = requestType ?? typeof(void);
        _responseType = responseType ?? typeof(void);
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        };
    }

    public void DeserializeRequest(Message message, object[] parameters)
    {
        if (_requestType == typeof(void))
        {
            // Brak parametrów do deserializacji
            return;
        }

        if (parameters == null)
            throw new ArgumentNullException(nameof(parameters));

        // Odczyt surowego JSON z MessageData, jeśli jest JsonBufferedMessageData
        var jsonBufferData = message.MessageData as JsonBufferedMessageData;
        if (jsonBufferData == null)
            throw new InvalidOperationException("Unexpected message data type, expected JsonBufferedMessageData.");

        // Odczyt bajtów JSON z bufferu
        var buffer = jsonBufferData.Buffer;
        var json = Encoding.UTF8.GetString(buffer, jsonBufferData.Offset, jsonBufferData.Size);

        // Deserializacja JSON do obiektu określonego typu
        parameters[0] = JsonSerializer.Deserialize(json, _requestType, _jsonOptions);
    }

    public Message SerializeReply(MessageVersion messageVersion, object[] parameters, object result)
    {
        var ms = new System.IO.MemoryStream();

        // Serializuj obiekt wynikowy do JSON w stream
        Utf8JsonWriter writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = false });
        JsonSerializer.Serialize(writer, result, _responseType, _jsonOptions);
        writer.Flush();
        ms.Position = 0;

        // Utwórz wiadomość WCF z JSON body writerem
        var message = Message.CreateMessage(messageVersion, null, new JsonBodyWriter(ms));

        // Dodaj nagłówek Content-Type: application/json do odpowiedzi HTTP
        var httpResponse = new HttpResponseMessageProperty();
        httpResponse.Headers["Content-Type"] = "application/json";
        message.Properties.Add(HttpResponseMessageProperty.Name, httpResponse);

        return message;
    }

    private class JsonBodyWriter : BodyWriter
    {
        private readonly System.IO.Stream _stream;

        public JsonBodyWriter(System.IO.Stream stream) : base(true)
        {
            _stream = stream;
        }

        protected override void OnWriteBodyContents(System.Xml.XmlDictionaryWriter writer)
        {
            using (var reader = new System.IO.StreamReader(_stream, Encoding.UTF8, false, 1024, leaveOpen: true))
            {
                var json = reader.ReadToEnd();
                writer.WriteRaw(json);
            }
        }
    }
}
