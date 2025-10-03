using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;
using System.Text;
using System.Text.Json;
using System.Xml;

public class SystemTextJsonDispatchFormatter : IDispatchMessageFormatter
{
    private readonly OperationDescription _operation;
    private readonly bool _isRequest;
    private readonly Dictionary<string, int> _parameterNames;
    private readonly JsonSerializerOptions _jsonOptions;

    public SystemTextJsonDispatchFormatter(OperationDescription operation, bool isRequest)
    {
        _operation = operation;
        _isRequest = isRequest;

        if (isRequest && operation.Messages.Count > 0 && operation.Messages[0].Body.Parts.Count > 1)
        {
            _parameterNames = new Dictionary<string, int>();
            for (int i = 0; i < operation.Messages[0].Body.Parts.Count; i++)
            {
                _parameterNames.Add(operation.Messages[0].Body.Parts[i].Name, i);
            }
        }

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
            // Możesz tu dodać inne opcje według potrzeb
        };
    }

    public void DeserializeRequest(Message message, object[] parameters)
    {
        if (!message.Properties.TryGetValue(WebBodyFormatMessageProperty.Name, out var bodyFormatProperty) ||
            ((WebBodyFormatMessageProperty)bodyFormatProperty).Format != WebContentFormat.Raw)
        {
            throw new InvalidOperationException("Incoming messages must have a body format of Raw. Is a ContentTypeMapper set on the WebHttpBinding?");
        }

        using (var bodyReader = message.GetReaderAtBodyContents())
        {
            bodyReader.MoveToContent();
            if (bodyReader.NodeType != XmlNodeType.Element || bodyReader.LocalName != "Binary")
                throw new InvalidOperationException("Expected <Binary> element not found.");

            string base64Content = bodyReader.ReadElementContentAsString();
            byte[] rawBody = Convert.FromBase64String(base64Content);

            using (var ms = new MemoryStream(rawBody))
            using (var sr = new StreamReader(ms))
            {
                if (_parameterNames == null)
                {
                    parameters[0] = JsonSerializer.Deserialize(sr.ReadToEnd(), _operation.Messages[0].Body.Parts[0].Type, _jsonOptions);
                }
                else
                {
                    var jsonText = sr.ReadToEnd();
                    using (JsonDocument jsonDoc = JsonDocument.Parse(jsonText))
                    {
                        foreach (var prop in jsonDoc.RootElement.EnumerateObject())
                        {
                            if (_parameterNames.TryGetValue(prop.Name, out int index))
                            {
                                var paramType = _operation.Messages[0].Body.Parts[index].Type;
                                parameters[index] = JsonSerializer.Deserialize(prop.Value.GetRawText(), paramType, _jsonOptions);
                            }
                        }
                    }
                }
            }
        }
    }

    public Message SerializeReply(MessageVersion messageVersion, object[] parameters, object result)
    {
        byte[] body;
        using (var ms = new MemoryStream())
        {
            using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = false }))
            {
                JsonSerializer.Serialize(writer, result, _operation.Messages[1].Body.ReturnValue.Type, _jsonOptions);
            }
            body = ms.ToArray();
        }

        var replyMessage = Message.CreateMessage(messageVersion, _operation.Messages[1].Action, new RawBodyWriter(body));
        replyMessage.Properties.Add(WebBodyFormatMessageProperty.Name, new WebBodyFormatMessageProperty(WebContentFormat.Raw));

        var httpResponse = new HttpResponseMessageProperty();
        httpResponse.Headers["Content-Type"] = "application/json";
        replyMessage.Properties.Add(HttpResponseMessageProperty.Name, httpResponse);

        return replyMessage;
    }

    private class RawBodyWriter : BodyWriter
    {
        private readonly byte[] _content;

        public RawBodyWriter(byte[] content) : base(true)
        {
            _content = content;
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            writer.WriteStartElement("Binary");
            writer.WriteBase64(_content, 0, _content.Length);
            writer.WriteEndElement();
        }
    }
}
