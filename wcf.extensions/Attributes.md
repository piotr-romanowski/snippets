Poniżej znajduje się pełna przykładowa implementacja atrybutów i powiązanych typów, które odpowiadają tym z `System.ServiceModel.Web` i `CoreWCF.Web`, niezbędnych do oznaczania metod WCF REST (WebInvoke, WebGet, WebMessageFormat itd.). Są to uproszczone wersje modeli i atrybutów, które umożliwią definiowanie kontraktów z REST-owym webHttpBinding, kompletne by zastąpić oryginalne atrybuty bez zależności od CoreWCF.Web.

```csharp
using System;

namespace System.ServiceModel.Web
{
    // Typ formatu komunikatu (JSON lub XML)
    public enum WebMessageFormat
    {
        Xml,
        Json
    }

    // Typ http method dla WebInvoke / WebGet
    public enum WebMessageBodyStyle
    {
        Bare,
        Wrapped,
        WrappedRequest,
        WrappedResponse
    }

    /using System;

namespace System.ServiceModel.Web
{
    public enum WebMessageFormat
    {
        Xml,
        Json
    }

    public enum WebMessageBodyStyle
    {
        Bare,
        Wrapped,
        WrappedRequest,
        WrappedResponse
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class WebInvokeAttribute : Attribute
    {
        public WebInvokeAttribute()
        {
            Method = "POST";
            UriTemplate = null;
            RequestFormat = WebMessageFormat.Xml;
            ResponseFormat = WebMessageFormat.Xml;
            BodyStyle = WebMessageBodyStyle.Bare;
        }

        public string Method { get; set; }
        public string UriTemplate { get; set; }
        public WebMessageFormat RequestFormat { get; set; }
        public WebMessageFormat ResponseFormat { get; set; }
        public WebMessageBodyStyle BodyStyle { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class WebGetAttribute : Attribute
    {
        public WebGetAttribute()
        {
            Method = "GET";
            UriTemplate = null;
            RequestFormat = WebMessageFormat.Xml;
            ResponseFormat = WebMessageFormat.Xml;
            BodyStyle = WebMessageBodyStyle.Bare;
        }

        public string Method { get; set; }
        public string UriTemplate { get; set; }
        public WebMessageFormat RequestFormat { get; set; }
        public WebMessageFormat ResponseFormat { get; set; }
        public WebMessageBodyStyle BodyStyle { get; set; }
    }
}


    // Opcje formatowania URI w zapytaniu
    [Flags]
    public enum WebContentFormat
    {
        Default = 0,
        Raw = 1,
        Xml = 2,
        Json = 4,
        RawJson = 8
    }

    // Atrybut do oznaczenia operacji REST z obsługą HEAD, DELETE lub niestandardowych metod HTTP
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class WebHttpBehaviorAttribute : Attribute
    {
        // Możesz rozszerzyć o dowolne dodatkowe właściwości, jeśli potrzeba
    }

    // Opcjonalnie enumeracja lub klasa z opcjami dodatkowej konfiguracji może być tu dodana

    // Przykładowe klasy pomocnicze, na których bazują CoreWCF lub własna implementacja mogą być dopisane zgodnie z potrzebą
}
```


### Co jest w powyższym:

- `WebInvokeAttribute` - atrybut oznaczający operację REST z podaniem metody HTTP, formatu JSON/XML oraz URI template.
- `WebGetAttribute` dziedziczący z `WebInvokeAttribute` z metodą GET jako domyślną.
- `WebMessageFormat` dla określenia formatu JSON/XML.
- `WebMessageBodyStyle` dla stylu ciała (bare, wrapped itd.) używanego w WCF JSON/XML.
- Dodatkowe typy jak `WebContentFormat` czy `WebHttpBehaviorAttribute` można rozszerzyć w zależności od potrzeb.


### Jak tego używać?

- W projekcie referencjonującym Twoje kontrakty REST dodaj tę definicję zamiast `CoreWCF.Web`.
- Oznacz metody serwisu `[WebGet]` lub `[WebInvoke]` z własnej przestrzeni `System.ServiceModel.Web` (wcześniej utworzona).
- Zapewnij własną logikę (np. behavior) do obsługi tych atrybutów w dispatcherze serwisu.

W razie potrzeby mogę pomóc też w zaimplementowaniu powiązanych `IOperationBehavior` i innych klas do pełnej integracji z WCF.Oto uproszczone wersje atrybutów i powiązanych typów z namespace `System.ServiceModel.Web`, odpowiadające tym z CoreWCF.Web, które możesz wykorzystać zamiast oryginalnych, aby niezależnie definiować REST w WCF:

```csharp
using System;

namespace System.ServiceModel.Web
{
    public enum WebMessageFormat
    {
        Xml,
        Json
    }

    public enum WebMessageBodyStyle
    {
        Bare,
        Wrapped,
        WrappedRequest,
        WrappedResponse
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class WebInvokeAttribute : Attribute
    {
        public WebInvokeAttribute() : this("POST") { }

        public WebInvokeAttribute(string method)
        {
            Method = method;
            UriTemplate = null;
            RequestFormat = WebMessageFormat.Xml;
            ResponseFormat = WebMessageFormat.Xml;
            BodyStyle = WebMessageBodyStyle.Bare;
        }

        public string Method { get; set; }
        public string UriTemplate { get; set; }
        public WebMessageFormat RequestFormat { get; set; }
        public WebMessageFormat ResponseFormat { get; set; }
        public WebMessageBodyStyle BodyStyle { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class WebGetAttribute : WebInvokeAttribute
    {
        public WebGetAttribute() : base("GET") { }
    }

    [Flags]
    public enum WebContentFormat
    {
        Default = 0,
        Raw = 1,
        Xml = 2,
        Json = 4,
        RawJson = 8
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class WebHttpBehaviorAttribute : Attribute
    {
        // Możesz rozbudować o dodatkowe właściwości
    }
}
```

Te atrybuty i typy możesz umieścić w swoim assembly pod namespace `System.ServiceModel.Web` bez ryzyka kolizji typów. Zapewniają one pełną funkcjonalność oznaczania metod REST w WCF webHttpBinding, umożliwiając zdefiniowanie HTTP method, URI template, formatu (JSON/XML) oraz stylu ciała wiadomości.

Jeśli chcesz, pomogę też z implementacją zachowań (`IOperationBehavior`) do pełnej obsługi tych atrybutów w dispatcherze serwisu.

