### Przykładowa weryfikacja na `ClaimsPrincipal` na podstawie rozszerzonego formatu `RequiredClaims`

```csharp
protected override bool CheckAccessCore(OperationContext context)
{
    var props = context.IncomingMessageProperties;
    if (!props.TryGetValue("OperationRequirement", out var opReqObj))
        return false;

    var req = opReqObj as OperationRequirement;
    if (req == null)
        return false;

    if (req.AllowAnonymous)
        return true;

    // Pobierasz token JWT z nagłówka i go walidujesz (Twój kod)
    string jwtToken = ExtractJwtToken(context);
    var principal = ValidateJwtToken(jwtToken); // zwraca ClaimsPrincipal

    if (principal == null || !principal.Identity.IsAuthenticated)
        return false;

    foreach (var requirement in req.RequiredClaims)
    {
        var parts = requirement.Split(':', 2);
        var type = parts[0].ToLowerInvariant();
        var value = parts.Length > 1 ? parts[1] : null;

        switch (type)
        {
            case "role":
                if (!principal.IsInRole(value))
                    return false;
                break;
            case "scope":
                var scopes = principal.FindAll("scope").Select(c => c.Value);
                if (!scopes.Contains(value))
                    return false;
                break;
            case "claim":
                // Format: claim:claimType:claimValue (np. claim:email_verified:true)
                var claimParts = value?.Split(':', 2);
                if (claimParts == null || claimParts.Length != 2)
                    return false;
                var claimType = claimParts[0];
                var claimValue = claimParts[1];
                if (!principal.Claims.Any(c => c.Type == claimType && c.Value == claimValue))
                    return false;
                break;
            default:
                // Domyślna weryfikacja claim o nazwie type i wartości value
                if (!principal.Claims.Any(c => c.Type == type && c.Value == value))
                    return false;
                break;
        }
    }

    return true;
}
```


***

### Uwagi

- `ExtractJwtToken` i `ValidateJwtToken` to Twoje metody walidacji, które wykorzystują `JwtSecurityTokenHandler`.
- Weryfikacja robi się na bardzo czytelnym, standardowym obiekcie `.NET` `ClaimsPrincipal`.
- Dzięki temu masz elastyczność i możesz rozbudowywać logikę claimów i ról.

***

Jeśli potrzebujesz, pomogę przygotować kompletną implementację całego procesu wraz z atrybutami i integracją z `ServiceAuthorizationManager`.Dokładnie — skoro masz `ClaimsPrincipal` zwracany z `JwtSecurityTokenHandler.ValidateToken()`, to weryfikację nadanych wymagań claimów powinieneś wykonywać właśnie na tym obiekcie. Przykładowo pętla w `CheckAccessCore` mogłaby wyglądać tak:

```csharp
foreach (var requirement in req.RequiredClaims)
{
    var parts = requirement.Split(':', 2);
    var type = parts[0].ToLowerInvariant();
    var value = parts.Length > 1 ? parts[1] : null;

    switch(type)
    {
        case "role":
            if (!principal.IsInRole(value))
                return false;
            break;
        case "scope":
            var scopes = principal.FindAll("scope").Select(c => c.Value);
            if (!scopes.Contains(value))
                return false;
            break;
        case "claim":
            // format: claim:claimType:claimValue
            var claimParts = value?.Split(':', 2);
            if (claimParts == null || claimParts.Length != 2)
                return false;
            var claimType = claimParts[0];
            var claimValue = claimParts[1];
            if (!principal.Claims.Any(c => c.Type == claimType && c.Value == claimValue))
                return false;
            break;
        default:
            if (!principal.Claims.Any(c => c.Type == type && c.Value == value))
                return false;
            break;
    }
}
```
