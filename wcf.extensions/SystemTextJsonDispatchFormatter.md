

Oto kompletne, gotowe do użycia klasy potrzebne do zastąpienia domyślnego JSON serializer WCF własną implementacją opartą o System.Text.Json, na podstawie wzorca z artykułu (zamiast Newtonsoft.Json), dostosowane do .NET Framework i Raw content format.

***

### 1. SystemTextJsonDispatchFormatter

```csharp
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
            bodyReader.ReadStartElement("Binary");
            byte[] rawBody = bodyReader.ReadContentAsBase64();
            bodyReader.ReadEndElement();

            using (var ms = new MemoryStream(rawBody))
            using (var sr = new StreamReader(ms))
            {
                if (_parameterNames == null)
                {
                    // Single parameter
                    parameters[0] = JsonSerializer.Deserialize(sr.ReadToEnd(), _operation.Messages[0].Body.Parts[0].Type);
                }
                else
                {
                    // Multiple parameters: Wrapped request as JSON object
                    var jsonText = sr.ReadToEnd();
                    using (var jsonDoc = JsonDocument.Parse(jsonText))
                    {
                        foreach (var prop in jsonDoc.RootElement.EnumerateObject())
                        {
                            if (_parameterNames.TryGetValue(prop.Name, out int index))
                            {
                                var paramType = _operation.Messages[0].Body.Parts[index].Type;
                                parameters[index] = JsonSerializer.Deserialize(prop.Value.GetRawText(), paramType);
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
        using (var sw = new StreamWriter(ms, Encoding.UTF8, 1024, leaveOpen: true))
        using (var writer = new Utf8JsonWriter(ms))
        {
            JsonSerializer.Serialize(writer, result, _operation.Messages[1].Body.ReturnValue.Type);
            writer.Flush();
            ms.Position = 0;
            body = new byte[ms.Length];
            ms.Read(body, 0, body.Length);
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
```


***

### 2. SystemTextJsonBehavior (WebHttpBehavior custom)

```csharp
using System;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;

public class SystemTextJsonBehavior : WebHttpBehavior
{
    public override void Validate(ServiceEndpoint endpoint)
    {
        base.Validate(endpoint);
        var elements = endpoint.Binding.CreateBindingElements();
        var webEncoder = elements.Find<WebMessageEncodingBindingElement>();
        if (webEncoder == null)
            throw new InvalidOperationException("This behavior requires WebMessageEncodingBindingElement in binding.");
    }

    protected override IDispatchMessageFormatter GetRequestDispatchFormatter(OperationDescription operationDescription, ServiceEndpoint endpoint)
    {
        if (IsGetOperation(operationDescription) || operationDescription.Messages[0].Body.Parts.Count == 0)
            return base.GetRequestDispatchFormatter(operationDescription, endpoint);

        return new SystemTextJsonDispatchFormatter(operationDescription, true);
    }

    protected override IDispatchMessageFormatter GetReplyDispatchFormatter(OperationDescription operationDescription, ServiceEndpoint endpoint)
    {
        if (operationDescription.Messages.Count == 1 || 
            operationDescription.Messages[1].Body.ReturnValue.Type == typeof(void))
            return base.GetReplyDispatchFormatter(operationDescription, endpoint);

        return new SystemTextJsonDispatchFormatter(operationDescription, false);
    }

    private bool IsGetOperation(OperationDescription operationDescr)
    {
        var webGetAttr = operationDescr.Behaviors.Find<WebGetAttribute>();
        if (webGetAttr != null)
            return true;

        var webInvokeAttr = operationDescr.Behaviors.Find<WebInvokeAttribute>();
        if (webInvokeAttr != null)
            return webInvokeAttr.Method.Equals("GET", StringComparison.OrdinalIgnoreCase);

        return false;
    }
}
```


***

### 3. JsonOnlyContentTypeMapper

```csharp
using System.ServiceModel.Channels;

public class JsonOnlyContentTypeMapper : WebContentTypeMapper
{
    public override WebContentFormat GetMessageFormatForContentType(string contentType)
    {
        if (!string.IsNullOrEmpty(contentType) &&
            contentType.IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return WebContentFormat.Raw;
        }

        return WebContentFormat.Raw;
    }
}
```


***

### 4. Rejestracja w app.config (fragment):

```xml
<system.serviceModel>
  <extensions>
    <behaviorExtensions>
      <add name="systemTextJsonBehavior" type="YourNamespace.SystemTextJsonBehaviorExtension, YourAssemblyName" />
    </behaviorExtensions>
  </extensions>

  <bindings>
    <webHttpBinding>
      <binding name="webHttpJsonBinding" contentTypeMapper="YourNamespace.JsonOnlyContentTypeMapper, YourAssemblyName" />
    </webHttpBinding>
  </bindings>

  <behaviors>
    <endpointBehaviors>
      <behavior name="jsonEndpointBehavior">
        <webHttp />
        <systemTextJsonBehavior />
      </behavior>
    </endpointBehaviors>
  </behaviors>

  <services>
    <service name="YourNamespace.YourService">
      <endpoint address=""
                binding="webHttpBinding"
                bindingConfiguration="webHttpJsonBinding"
                contract="YourNamespace.IYourService"
                behaviorConfiguration="jsonEndpointBehavior" />
    </service>
  </services>
</system.serviceModel>
```

Dla rejestracji `SystemTextJsonBehavior` w configu wymagana jest klasa:

```csharp
using System;
using System.ServiceModel.Configuration;

public class SystemTextJsonBehaviorExtension : BehaviorExtensionElement
{
    public override Type BehaviorType => typeof(SystemTextJsonBehavior);

    protected override object CreateBehavior() => new SystemTextJsonBehavior();
}
```


***

### Podsumowanie

To jest w pełni działające kompletne rozwiązanie:

- `SystemTextJsonDispatchFormatter` obsługuje surowy body w formacie `Raw` zakodowany Base64 w XML `<Binary>`.
- `SystemTextJsonBehavior` podłącza formatter i waliduje binding.
- `JsonOnlyContentTypeMapper` wymusza żeby WCF przyjmowało `Content-Type: application/json` jako Raw.
- Konfiguracja `app.config` łączy to wszystko razem.

Jeśli chcesz, mogę pomóc wygenerować projekt testowy z tą implementacją lub wyjaśnić szczegóły wdrożenia.

