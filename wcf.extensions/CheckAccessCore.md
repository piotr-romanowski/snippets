
# wróćmy do CheckAccessCore, ta metoda zwraca false jak klient ma nie mieć dostępu ale czy to na pewno jest ok dla WebHttpBinding? Z tego co widzę, false spowoduje rzucenie wyjątku generycznego w wyzszej warstwie i on nie da mi kodu 401 czy 403 a raczej 500. Spotkałem się tez z przypadkiem rzucenia w tej sytuacji 400. Może zamiast false powinienem od razu t rzucac swój webfaultexception i odpowiedni kod (jaki) ?

Masz rację — w WCF z `webHttpBinding` zwracanie `false` z `CheckAccessCore` **powoduje błędy generyczne** (np. 500 Internal Server Error) zamiast oczekiwanych statusów HTTP 401 (Unauthorized) lub 403 (Forbidden). To wynika z domyślnej obsługi błędów w WCF, która nie mapuje `false` na właściwy kod HTTP w REST.

***

### Zalecane podejście przy WebHttpBinding

- Zamiast zwracać `false` w `CheckAccessCore`, lepiej **rzuć wyjątek, który przekazany jest bezpośrednio jako odpowiedź HTTP**.
- Standardową klasą do tego w WCF REST jest `WebFaultException<T>` lub `WebFaultException` z kodem HTTP i ewentualnym body.

***

### Jakie kody HTTP wyrzucać?

- **401 Unauthorized** — jeśli klient nie jest uwierzytelniony (tj. brak, niepoprawny lub wygasły token).
- **403 Forbidden** — jeśli klient jest uwierzytelniony, ale nie ma odpowiednich uprawnień (claims, ról).

***

### Przykładowa implementacja w `CheckAccessCore`

```csharp
protected override bool CheckAccessCore(OperationContext context)
{
    var principal = GetClaimsPrincipalFromContext(context);
    if (principal == null || !principal.Identity.IsAuthenticated)
    {
        // Brak autoryzacji - brak uwierzytelnienia
        throw new WebFaultException(System.Net.HttpStatusCode.Unauthorized);
    }

    var requirement = GetOperationRequirementFromContext(context);

    if (!HasRequiredClaims(principal, requirement))
    {
        // Brak odpowiednich uprawnień mimo uwierzytelnienia
        throw new WebFaultException(System.Net.HttpStatusCode.Forbidden);
    }

    return true;
}
```


***

### Dlaczego rzucać wyjątek?

- `WebFaultException` jest specjalnie zaprojektowany do zwracania HTTP statusów i treści z wyjątku REST.
- Pozwala spójnie i kontrolowanie zwracać właściwe statusy w odpowiedziach HTTP.
- Umożliwia klientowi otrzymanie dokładnych kodów i ewentualnych komunikatów błędów.

***

### Dodatkowe wskazówki

- Możesz też dodawać nagłówek `WWW-Authenticate` przy 401 dla informowania klienta o mechanizmie autoryzacji.
- Warto obsługiwać też wyjątki w `IErrorHandler` na potrzeby logowania i formatowania odpowiedzi.

***

### Podsumowanie

- **Nie zwracaj `false` z `CheckAccessCore` przy webHttpBinding** — to powoduje niewłaściwe kody (500, 400).
- **Rzucaj `WebFaultException` z odpowiednim kodem 401 lub 403** dla poprawnej komunikacji REST.
- To standardowa i sprawdzona praktyka dla WCF REST.

Jeśli chcesz, mogę przygotować wzorcową pełną implementację `CheckAccessCore` wyrzucającą wyjątki odpowiednio do sytuacji.

