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

    private readonly int? _bodyParameterIndex;
    private readonly Type _bodyParameterType;
    private readonly int[] _uriParameterIndexes;

    public SystemTextJsonDispatchFormatter(IDispatchMessageFormatter defaultFormatter, OperationDescription operationDescription)
    {
        _defaultFormatter = defaultFormatter ?? throw new ArgumentNullException(nameof(defaultFormatter));
        _operationDescription = operationDescription ?? throw new ArgumentNullException(nameof(operationDescription));

        var bodyParts = _operationDescription.Messages[0].Body.Parts;

        if (bodyParts.Count == 1)
        {
            var bodyPart = bodyParts[0];
            _bodyParameterIndex = bodyPart.Index;
            _bodyParameterType = bodyPart.Type;

            _uriParameterIndexes = Enumerable.Range(0, _operationDescription.Messages[0].Body.Parts.Count)
                .Where(i => i != _bodyParameterIndex.Value).ToArray();
        }
        else
        {
            _bodyParameterIndex = null;
            _bodyParameterType = null;

            _uriParameterIndexes = Enumerable.Range(0, _operationDescription.Messages[0].Body.Parts.Count).ToArray();
        }
    }

    public void DeserializeRequest(Message message, object[] parameters)
    {
        if (_bodyParameterIndex.HasValue)
        {
            // Deserializacja parametrów URI (wszystkie poza body)
            if (_uriParameterIndexes.Length > 0)
            {
                object[] uriParams = new object[parameters.Length];
                uriParams[_bodyParameterIndex.Value] = GetDefaultValue(_bodyParameterType);
                _defaultFormatter.DeserializeRequest(message, uriParams);

                foreach (var i in _uriParameterIndexes)
                {
                    parameters[i] = uriParams[i];
                }
            }

            // Deserializacja JSON ciała do parametru body
            string json = ReadMessageBodyAsString(message);
            parameters[_bodyParameterIndex.Value] = 
                string.IsNullOrWhiteSpace(json) ? GetDefaultValue(_bodyParameterType) : JsonSerializer.Deserialize(json, _bodyParameterType);
        }
        else
        {
            // Wszystkie parametry z UriTemplate — domyślna deserializacja
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

    private string ReadMessageBodyAsString(Message message)
    {
        MessageBuffer buffer = message.CreateBufferedCopy(int.MaxValue);
        Message copy = buffer.CreateMessage();
        var reader = copy.GetReaderAtBodyContents();

        string body = reader.ReadOuterXml();

        return body;
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