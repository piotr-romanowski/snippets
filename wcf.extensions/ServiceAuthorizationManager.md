
Oto kompletna przykładowa implementacja z dodaniem własnego extension do operacji, by przechowywać nazwę metody i wymagania autoryzacyjne, oraz użyciem tego w ServiceAuthorizationManager.

***

### Definicje atrybutów i klasy na wymagania

```csharp
using System;
using System.Collections.Generic;
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

### Własny Operation Extension do trzymania metadanych operacji

```csharp
using System.ServiceModel.Description;

public class OperationMetadata : IExtension<OperationDescription>
{
    public string OperationName { get; }
    public OperationRequirement Requirement { get; }

    public OperationMetadata(string operationName, OperationRequirement requirement)
    {
        OperationName = operationName;
        Requirement = requirement;
    }

    public void Attach(OperationDescription owner) { }
    public void Detach(OperationDescription owner) { }
}
```


***

### ServiceBehavior do przypięcia metadanych do operacji

```csharp
using System.Collections.ObjectModel;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;

public class AuthorizationBehavior : IServiceBehavior
{
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

                var metadata = new OperationMetadata(opDesc.Name, methodReq);
                opDesc.Extensions.Add(metadata);
            }
        }

        foreach (ChannelDispatcher cd in serviceHostBase.ChannelDispatchers)
            foreach (var ed in cd.Endpoints)
                ed.DispatchRuntime.ServiceAuthorizationManager = new CustomServiceAuthorizationManager();
    }

    private OperationRequirement GetRequirements(MemberInfo member)
    {
        var allowAnon = member.GetCustomAttribute<AllowAnonymousAttribute>() != null;
        var claimsAttr = member.GetCustomAttribute<RequiredClaimsAttribute>();
        var audAttr = member.GetCustomAttribute<RequiredAudiencesAttribute>();

        if (allowAnon || claimsAttr != null || audAttr != null)
        {
            return new OperationRequirement()
            {
                AllowAnonymous = allowAnon,
                RequiredClaims = claimsAttr?.Claims ?? Array.Empty<string>(),
                RequiredAudiences = audAttr?.Audiences ?? Array.Empty<string>()
            };
        }
        return null;
    }

    public void AddBindingParameters(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase,
        Collection<ServiceEndpoint> endpoints, BindingParameterCollection bindingParameters) { }

    public void Validate(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase) { }
}
```


***

### ServiceAuthorizationManager z odczytem rozszerzenia operacji

```csharp
using System.Collections.Generic;
using System.IdentityModel.Claims;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;

public class CustomServiceAuthorizationManager : ServiceAuthorizationManager
{
    protected override bool CheckAccessCore(OperationContext operationContext)
    {
        if (!operationContext.IncomingMessageProperties.TryGetValue("HttpOperation", out var opDescObj)
            || !(opDescObj is OperationDescription opDesc))
            return false;

        var metadata = opDesc.Extensions.Find<OperationMetadata>();
        if (metadata == null)
            return false;

        var req = metadata.Requirement;
        if (req.AllowAnonymous)
            return true;

        var sc = operationContext.ServiceSecurityContext;
        if (sc == null || !sc.PrimaryIdentity.IsAuthenticated)
            return false;

        var claimSets = sc.AuthorizationContext.ClaimSets ?? new List<ClaimSet>();

        // Sprawdź wymagane claims
        foreach (var requiredClaim in req.RequiredClaims)
        {
            bool found = claimSets.Any(cs => cs.FindClaims(requiredClaim, Rights.PossessProperty).Any());
            if (!found)
                return false;
        }

        // Sprawdź wymagane audiences
        foreach (var requiredAud in req.RequiredAudiences)
        {
            bool foundAud = claimSets.Any(cs => cs.FindClaims("aud", Rights.PossessProperty)
                .Any(c => c.Resource.ToString().Equals(requiredAud)));
            if (!foundAud)
                return false;
        }

        return true;
    }
}
```


***

### Uwaga dotycząca ustawienia `HttpOperation` w IncomingMessageProperties

W przypadku webHttpBinding operacja, do której trafia request, może nie być standardowo dostępna pod kluczem `"HttpOperation"`. Możesz to zrobić ustanawiając własny `IDispatchMessageInspector` i ustawiając `IncomingMessageProperties["HttpOperation"]` podczas przyjmowania wiadomości. Przykładowo:

```csharp
public class OperationInspector : IDispatchMessageInspector
{
    public object AfterReceiveRequest(ref Message request, IClientChannel channel, InstanceContext instanceContext)
    {
        var endpointDispatcher = instanceContext.Host.ChannelDispatchers
            .OfType<ChannelDispatcher>()
            .SelectMany(cd => cd.Endpoints)
            .FirstOrDefault(ep => ep.DispatchRuntime.AddressFilter.Match(request));

        if (endpointDispatcher != null)
        {
            var operation = endpointDispatcher.DispatchRuntime.Operations
                .FirstOrDefault(op => request.Headers.Action?.EndsWith(op.Name) == true);

            if (operation != null)
            {
                request.Properties["HttpOperation"] = operation.Description;
            }
        }
        return null;
    }

    public void BeforeSendReply(ref Message reply, object correlationState) { }
}
```

Ten inspektor mocujesz do DispatchRuntime w `ApplyDispatchBehavior`.

***

### Podsumowanie dodania do serwisu

W ServiceHost konfigurujesz:

```csharp
var host = new ServiceHost(typeof(MyService));
host.Description.Behaviors.Add(new AuthorizationBehavior());
foreach (ChannelDispatcher cd in host.ChannelDispatchers)
    foreach (var ed in cd.Endpoints)
        ed.DispatchRuntime.MessageInspectors.Add(new OperationInspector());
host.Open();
```


***

Ten wzorzec pozwala efektywnie:

- Dopiąć metadane i wymagania do każdej operacji REST (webHttpBinding)
- Rozpoznać operację w AuthorizationManager przez message inspector ustawiający extension
- W `CheckAccessCore` mieć szybki dostęp do atrybutów i wymagań bez refleksji w runtime

Jeśli jest potrzeba, mogę pomóc też z dokładną implementacją inspektora lub innymi szczegółami.

