
Poniżej przedstawiam kompletną, poprawioną wersję implementacji, korzystającą z `IMessageInspector` do wyznaczania, którą operację wywołano i odczytu wymagań z wcześniej zbudowanej mapy, przechowywanej w `ServiceBehavior`. W `ServiceAuthorizationManager` odczytujemy dane z `IncomingMessageProperties`, eliminując konieczność korzystania z `IExtension<OperationDescription>`, co nie jest właściwe.

***

### 1. Definicja atrybutów i modelu wymagań

```csharp
using System;
using System.Linq;
using System.Reflection;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class AllowAnonymousAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class RequiredClaimsAttribute : Attribute
{
    public string[] Claims { get; }
    public RequiredClaimsAttribute(params string[] claims)
    {
        Claims = claims;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class RequiredAudiencesAttribute : Attribute
{
    public string[] Audiences { get; }
    public RequiredAudiencesAttribute(params string[] audiences)
    {
        Audiences = audiences;
    }
}

public class OperationRequirement
{
    public bool AllowAnonymous { get; set; }
    public string[] RequiredClaims { get; set; } = Array.Empty<string>();
    public string[] RequiredAudiences { get; set; } = Array.Empty<string>();
}
```


***

### 2. Budowanie mapy wymagań podczas konfiguracji serwisu

```csharp
using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Description;

public class AuthorizationBehavior : IServiceBehavior
{
    public static Dictionary<string, OperationRequirement> OperationRequirements { get; private set; } = new();

    public void ApplyDispatchBehavior(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
    {
        var contractType = serviceDescription.ServiceType;
        var defaultRequirement = GetRequirements(contractType);

        foreach (var endpoint in serviceDescription.Endpoints)
        {
            var contract = endpoint.Contract.ContractType;
            foreach (var opDesc in endpoint.Contract.Operations)
            {
                var methodInfo = contract.GetMethod(opDesc.Name);
                var methodReq = GetRequirements(methodInfo) ?? defaultRequirement;

                // Zapisywanie do mapy
                OperationRequirements[opDesc.Name] = methodReq;
            }
        }
    }

    private static OperationRequirement GetRequirements(MemberInfo member)
    {
        if (member == null) return null;

        var allowAnon = member.GetCustomAttribute<AllowAnonymousAttribute>() != null;
        var claimsAttr = member.GetCustomAttribute<RequiredClaimsAttribute>();
        var audAttr = member.GetCustomAttribute<RequiredAudiencesAttribute>();

        if (allowAnon || claimsAttr != null || audAttr != null)
        {
            return new OperationRequirement
            {
                AllowAnonymous = allowAnon,
                RequiredClaims = claimsAttr?.Claims ?? Array.Empty<string>(),
                RequiredAudiences = audAttr?.Audiences ?? Array.Empty<string>()
            };
        }
        return null;
    }

    public void AddBindingParameters(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase,
        System.Collections.ObjectModel.Collection<ServiceEndpoint> endpoints, System.ServiceModel.Channels.BindingParameterCollection bindingParameters)
    { }

    public void Validate(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase) { }
}
```


***

### 3. Implementacja Inspektora wiadomości do ustalania operacji

```csharp
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;

public class OperationInspector : IDispatchMessageInspector
{
    public object AfterReceiveRequest(ref Message request, IClientChannel channel, InstanceContext instanceContext)
    {
        // Tu można wyłuskać nazwę operacji z URI lub nagłówków HTTP
        // Zakładamy, że nazwę operacji można wywnioskować z URI (np. ostatni segment)

        var httpProps = ObjectPropertyBag.GetProperty<HttpRequestMessageProperty>(request.Properties);
        string path = null;
        if (request.Properties.ContainsKey("HttpRequestMessageProperty"))
            path = request.Properties["HttpRequestMessageProperty"] as HttpRequestMessageProperty?.Method;

        // Jest różnie, ale dla prostoty:
        string operationName = null;
        if (request.Properties.TryGetValue("UriTemplateMatchResults", out var matchResultsObj))
        {
            var matchResults = matchResultsObj as System.Web.Http.Routing.HttpRouteData;
            operationName = matchResults?.Values["operation"]?.ToString(); // jeśli masz to zdefiniowane w routing
        }
        else
        {
            // alternatywnie, odczyt z URI
            var uri = request.Headers.To?.AbsolutePath;
            if (uri != null)
            {
                var segments = new Uri(uri).Segments;
                operationName = segments.LastOrDefault()?.Trim('/');
            }
        }

        // W tym przykładzie zakładamy, że masz nazwę metody jako ostatni segment URI
        if (!string.IsNullOrEmpty(operationName) && AuthorizationBehavior.OperationRequirements.ContainsKey(operationName))
        {
            // zapisanie do IncomingMessageProperties
            request.Properties["OperationName"] = operationName;
        }

        return null;
    }

    public void BeforeSendReply(ref Message reply, object correlationState)
    {
        // Nic nie potrzebujemy
    }
}
```


### Uwaga:

- W praktyce odczytałem `OperationName` z URI, co jest typowe dla REST API.
- Dla konkretnych konfiguracji warto dopracować odczyt metody.

***

### 4. `ServiceAuthorizationManager` wykonujący weryfikację

```csharp
using System.Collections.Generic;
using System.IdentityModel.Claims;
using System.Linq;
using System.ServiceModel;

public class CustomServiceAuthorizationManager : ServiceAuthorizationManager
{
    protected override bool CheckAccessCore(OperationContext context)
    {
        var props = context.IncomingMessageProperties;
        if (!props.TryGetValue("OperationName", out var opNameObj))
            return false;

        var operationName = opNameObj as string;
        if (operationName == null || !AuthorizationBehavior.OperationRequirements.TryGetValue(operationName, out var req))
            return false;

        if (req.AllowAnonymous)
            return true;

        var sc = context.ServiceSecurityContext;
        if (sc == null || !sc.PrimaryIdentity.IsAuthenticated)
            return false;

        var claimSets = sc.AuthorizationContext.ClaimSets ?? new List<ClaimSet>();

        // Sprawdzanie Claimów
        foreach (var claim in req.RequiredClaims)
        {
            if (!claimSets.Any(cs => cs.FindClaims(claim, Rights.PossessProperty).Any()))
                return false;
        }

        // Sprawdzanie audience
        foreach (var aud in req.RequiredAudiences)
        {
            if (!claimSets.Any(cs => cs.FindClaims("aud", Rights.PossessProperty).Any(c => c.Resource.ToString() == aud)))
                return false;
        }
        return true;
    }
}
```


***

### 5. Ustawienia w hostingu

```csharp
var host = new ServiceHost(typeof(MyService));
host.Description.Behaviors.Add(new AuthorizationBehavior());
// Dodajesz inspektor
foreach (var dispatcher in host.ChannelDispatchers.OfType<ChannelDispatcher>())
{
    foreach (var endpoint in dispatcher.Endpoints)
    {
        endpoint.DispatchRuntime.MessageInspectors.Add(new OperationInspector());
    }
}
host.Open();
```


***

### Podsumowanie

- Automatyczne ustalanie nazwy operacji na podstawie URI w `OperationInspector`.
- Mapowanie wymagań na starcie i przechowywanie w statycznym słowniku w `AuthorizationBehavior`.
- Autoryzacja oparta na odczycie tych wymagań w `CheckAccessCore`.

To rozwiązanie jest skalowalne i czyste, a użycie `IExtension<OperationDescription>` ogranicza się do konfiguracji, a runtime operacji odczytujemy z `IncomingMessageProperties`.

