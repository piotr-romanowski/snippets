W WCF, atrybuty typu ServiceBehavior mogą implementować interfejs IServiceBehavior i są wykrywane przez framework automatycznie na klasie serwisu. Jednakże atrybuty to instancje tworzone przez refleksję, bez wsparcia dla wstrzykiwania zależności czy konfiguracji zewnętrznej wprost przez konstruktor.

Najczęstszy sposób, aby “wstrzyknąć” konfigurację do atrybutu takiego jak ServiceBehavior, to:

- przechowywać w atrybucie informację o typie providera konfiguracji lub konfiguracji w postaci np. stringów/kluczy,
- a następnie w metodzie ApplyDispatchBehavior (interfejs IServiceBehavior), która działa w runtime, tworzyć instancję providera (np. za pomocą Activator lub z IoC kontenera) i korzystać z konfiguracji,
- alternatywnie, jeśli korzystasz z IoC kontenera, możesz stworzyć własny ServiceHost (lub ServiceHostFactory), który będzie przygotowywał konfigurację i przekazywał ją do behavior w sposób programowy (np. dodając behavior do ServiceDescription.Behaviors w kodzie zamiast atrybutem).

Przykład prostego custom ServiceBehavior atrybutu, który pobiera konfigurację z provider klasy w runtime:

```csharp
[AttributeUsage(AttributeTargets.Class)]
public class MyServiceBehaviorAttribute : Attribute, IServiceBehavior
{
    private readonly Type _configProviderType;
    private object _configProviderInstance;

    public MyServiceBehaviorAttribute(Type configProviderType)
    {
        _configProviderType = configProviderType;
    }

    public void AddBindingParameters(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase, Collection<ServiceEndpoint> endpoints, BindingParameterCollection bindingParameters)
    {
        // brak implementacji w tym przykładzie
    }

    public void ApplyDispatchBehavior(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
    {
        // instancja providera konfiguracji tworzona dynamicznie
        if (_configProviderInstance == null && _configProviderType != null)
        {
            _configProviderInstance = Activator.CreateInstance(_configProviderType);
        }

        // teraz możesz korzystać z _configProviderInstance przy konfiguracji ServiceHostBase
        // przykładowo, jeśli to jest konkretny typ z metodą GetConfigValue
    }

    public void Validate(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
    {
        // brak implementacji w tym przykładzie
    }
}

public class MyConfigProvider
{
    public string GetConfigValue()
    {
        return "Przykładowa konfiguracja";
    }
}

[MyServiceBehavior(typeof(MyConfigProvider))]
public class MyService
{
    // implementacja serwisu
}
```

W tym przykładzie, w metodzie ApplyDispatchBehavior tworzona jest instancja providera i można tę konfigurację użyć do modyfikacji zachowania serwisu.

Alternatywnie, jeśli używasz IoC, możesz:

- stworzyć custom ServiceHost (lub ServiceHostFactory),
- w nim pobrać instancję konfiguracji z kontenera,
- dodać behavior programowo do ServiceDescription.Behaviors z tą konfiguracją.

To podejście jest bardziej elastyczne i często zalecane.

Podsumowanie:

- Nie można wstrzyknąć konfiguracji bezpośrednio do atrybutu przez konstruktor,
- można trzymać typ providera w atrybucie i w ApplyDispatchBehavior tworzyć instancję i pobierać konfigurację,
- dla zaawansowanej konfiguracji lepiej korzystać z custom ServiceHost/ServiceHostFactory i programowego dodawania behavior.

Czy warto przygotować konkretny przykład implementacji z custom ServiceHostFactory i IoC?
<span style="display:none">[^1][^10][^11][^12][^13][^14][^15][^16][^17][^18][^19][^2][^20][^3][^4][^5][^6][^7][^8][^9]</span>

<div align="center">⁂</div>

[^1]: https://stackoverflow.com/questions/13541213/dependency-injection-for-wcf-custom-behaviors

[^2]: https://learn.microsoft.com/en-us/dotnet/framework/wcf/extending/configuring-and-extending-the-runtime-with-behaviors

[^3]: https://learn.microsoft.com/en-us/dotnet/framework/wcf/samples/default-service-behavior

[^4]: https://www.c-sharpcorner.com/blogs/servicebehavior-attribute-and-its-parameters-in-wcf

[^5]: https://pieterderycke.wordpress.com/2011/05/09/using-an-ioc-container-to-create-wcf-service-instances/

[^6]: https://knuturke.wordpress.com/2020/09/30/wcf-custom-behaviors-service-behavior/

[^7]: https://trycatch.me/adding-custom-message-headers-to-a-wcf-service-using-inspectors-behaviors/

[^8]: http://orand.blogspot.com/2006/10/wcf-service-dependency-injection.html

[^9]: https://stackoverflow.com/questions/35844969/how-to-add-custom-service-behavior-to-wcf-configuration

[^10]: https://www.c-sharpcorner.com/UploadFile/rkartikcsharp/service-behavior-in-wcf/

[^11]: https://dzimchuk.net/dependency-injection-with-wcf/

[^12]: https://learn.microsoft.com/en-us/archive/msdn-magazine/2007/december/service-station-extending-wcf-with-custom-behaviors

[^13]: https://www.codemag.com/article/0809101/WCF-the-Manual-Way…-the-Right-Way

[^14]: https://blogs.cuttingedge.it/steven/posts/2014/dependency-injection-in-attributes-dont-do-it/

[^15]: https://www.codemag.com/article/0701041/Hosting-WCF-Services

[^16]: https://www.c-sharpcorner.com/UploadFile/db2972/wcf-service-configuration-using-web-config-day-11/

[^17]: https://autofac.org/apidoc/html/3BC9275C.htm

[^18]: https://www.kolls.net/blog/?p=43

[^19]: https://www.codeproject.com/articles/Focus-on-the-Extension-of-WCF-Behavior

[^20]: https://alexanderdevelopment.net/post/2013/08/01/custom-wcf-service-authentication-using-microsoft-dynamics-crm-credentials-2/

