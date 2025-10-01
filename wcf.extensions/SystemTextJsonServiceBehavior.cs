using System;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;

public class SystemTextJsonServiceBehavior : IServiceBehavior
{
    public void AddBindingParameters(ServiceDescription serviceDescription, System.ServiceModel.ServiceHostBase serviceHostBase, System.Collections.ObjectModel.Collection<ServiceEndpoint> endpoints, System.ServiceModel.Channels.BindingParameterCollection bindingParameters)
    {
        // Nie potrzebujemy dodawać parametrów
    }

    public void ApplyDispatchBehavior(ServiceDescription serviceDescription, System.ServiceModel.ServiceHostBase serviceHostBase)
    {
        foreach (var endpoint in serviceDescription.Endpoints)
        {
            foreach (var operation in endpoint.Contract.Operations)
            {
                var dispatchOperation = serviceHostBase.ChannelDispatchers
                    .OfType<System.ServiceModel.Dispatcher.ChannelDispatcher>()
                    .SelectMany(cd => cd.Endpoints)
                    .SelectMany(ed => ed.DispatchRuntime.Operations)
                    .FirstOrDefault(op => op.Name == operation.Name);

                if (dispatchOperation != null)
                {
                    var dataType = operation.Messages[1].Body.Parts[0].Type;
                    dispatchOperation.Formatter = new SystemTextJsonFormatter(dataType);
                }
            }
        }
    }

    public void Validate(ServiceDescription serviceDescription, System.ServiceModel.ServiceHostBase serviceHostBase)
    {
        // Można dodać walidacje jeśli potrzeba
    }
}
