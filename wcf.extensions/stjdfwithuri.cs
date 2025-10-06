using System;
using System.Collections.Specialized;
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

        if (!message.Properties.TryGetValue("UriTemplateMatchResults", out var matchObj) ||
            !(matchObj is UriTemplateMatch uriMatch))
        {
            throw new InvalidOperationException("Brak UriTemplateMatch w Message.Properties");
        }

        NameValueCollection boundVariables = uriMatch.BoundVariables;

        var paramParts = _operationDescription.Messages[0].Body.Parts;

        if (paramParts.Count == 0)
            return;

        // Znajdź parametr body - taki który nie jest w boundVariables
        int? bodyParamIndex = null;
        for (int i = 0; i < paramParts.Count; i++)
        {
            if (!boundVariables.AllKeys.Contains(paramParts[i].Name))
            {
                bodyParamIndex = paramParts[i].Index;
                break;
            }
        }

        // Deserializuj parametry URI
        foreach (string key in boundVariables.AllKeys)
        {
            var param = paramParts.FirstOrDefault(p => p.Name == key);
            if (param != null)
            {
                int idx = param.Index;
                parameters[idx] = ConvertParameter(boundVariables[key], param.Type);
            }
        }

        // Deserializuj body JSON
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