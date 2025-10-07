
Poniżej znajduje się kompletna, poprawiona implementacja z wykorzystaniem **custom OperationSelector** zamiast `IDispatchMessageInspector` do rozpoznawania operacji w WCF webHttpBinding. Umożliwia to lekkie, szybkie i semantyczne przypisanie nazwy operacji do `IncomingMessageProperties`, co potem jest wykorzystywane w `ServiceAuthorizationManager` do autoryzacji.

***

### 1. Atrybuty i model wymagań

```csharp
using System;
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

### 2. `AuthorizationBehavior` – budowanie mapy wymagań

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

                OperationRequirements[opDesc.Name] = methodReq;
            }

            // Podmieniamy OperationSelector na własny
            var dispatchRuntime = serviceHostBase.ChannelDispatchers
                .OfType<ChannelDispatcher>()
                .SelectMany(cd => cd.Endpoints)
                .FirstOrDefault(ep => ep.Contract.Name == endpoint.Contract.Name)?
                .DispatchRuntime;

            if (dispatchRuntime != null)
            {
                dispatchRuntime.OperationSelector = new CustomOperationSelector(OperationRequirements);
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

### 3. `CustomOperationSelector` - ustalanie operacji i wpis do IncomingMessageProperties

```csharp
using System.Collections.Generic;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;
using System.Linq;
using System;

public class CustomOperationSelector : IDispatchOperationSelector
{
    private readonly Dictionary<string, OperationRequirement> _requirements;

    public CustomOperationSelector(Dictionary<string, OperationRequirement> requirements)
    {
        _requirements = requirements;
    }

    public string SelectOperation(ref Message message)
    {
        // Przykład: wyciągnięcie nazwy operacji z URI — zakładamy REST z segmentem metody na końcu
        var uri = message.Properties.Via.AbsolutePath;
        var segments = uri.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

        // Zakładamy, że ostatni segment to nazwa operacji
        var operationName = segments.LastOrDefault();

        if (!string.IsNullOrEmpty(operationName) && _requirements.ContainsKey(operationName))
        {
            // Przekazujemy operationName w IncomingMessageProperties do AuthorizationManager
            message.Properties["OperationName"] = operationName;
            return operationName;
        }

        // Fallback - domyślnie żadna operacja, wyrzuci błąd lub obsłuży inaczej
        return null;
    }
}
```


***

### 4. `CustomServiceAuthorizationManager` – weryfikacja dostępu po odczycie operationName

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

        // Sprawdzanie wymaganych claimów
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

### 5. Konfiguracja serwisu i dodanie behawioru

```csharp
var host = new ServiceHost(typeof(MyService));
host.Description.Behaviors.Add(new AuthorizationBehavior());

// Otwórz host
host.Open();
```


***

### Podsumowanie

- `AuthorizationBehavior` buduje mapę wymagań i podmienia `OperationSelector` na własny.
- `CustomOperationSelector` rozpoznaje operację na podstawie URI i przekazuje jej nazwę do `IncomingMessageProperties`.
- `CustomServiceAuthorizationManager` korzysta z tego, by sprawdzić wymagania i wykonać autoryzację.

To podejście jest lżejsze i bardziej zgodne z pipeline WCF niż inspektor wiadomości, co daje najlepszą wydajność i czytelność.

Jeśli trzeba, mogę pomóc z dostosowaniem rozpoznawania operacji do specyficznego routingu URI.

