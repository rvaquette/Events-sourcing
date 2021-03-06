﻿using System.Reflection;
using System.Web.Http;
using System.Web.Mvc;
using NServiceBus;
using Autofac;
using Autofac.Integration.WebApi;

namespace eCommerce.Rest
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            WebApiConfig.Register(GlobalConfiguration.Configuration);
            var x = Assembly.GetExecutingAssembly();
            ConfigureEndpoint();
        }

        /*
    Although NServiceBus uses its own DI container internally, or any other DI to set up injection into the web API controls, 
    because that's not supported by NServiceBus container. The ContainerBuilder class for Autofac is called ContainerBuilder. 
    We registered the controllers with the container => thus the endpoint is registered. We build the container to tell Web API
    to use it by setting the DependencyResolver on its Configuration object. But from now on, We can inject the endpoint
    instance into the controller to do operations with messages.

    install-package Microsoft.AspNet.WebApi.OData -ProjectName 
    update-package Microsoft.AspNet.WebApi -ProjectName eCommerce.Rest
    */
        private void ConfigureEndpoint()
        {
            var builder = new ContainerBuilder();

            builder.RegisterApiControllers(controllerAssemblies: Assembly.GetExecutingAssembly());
            var container = builder.Build();

            GlobalConfiguration.Configuration.DependencyResolver = new AutofacWebApiDependencyResolver(container);

            var endpointConfiguration = new EndpointConfiguration(endpointName: "eCommerce.Rest");
            endpointConfiguration.UsePersistence<InMemoryPersistence>();
            endpointConfiguration.UseTransport<MsmqTransport>();
            endpointConfiguration.PurgeOnStartup(value: true);
            endpointConfiguration.EnableInstallers();
            endpointConfiguration.UseContainer<AutofacBuilder>(
                customizations: customizations =>
                {
                    customizations.ExistingLifetimeScope(container);
                });
            endpointConfiguration.UsePersistence<InMemoryPersistence>();
            endpointConfiguration.EnableInstallers();

            var endpoint = Endpoint.Start(endpointConfiguration).GetAwaiter().GetResult();

            var updater = new ContainerBuilder();
            updater.RegisterInstance(endpoint);
            updater.RegisterApiControllers(controllerAssemblies: Assembly.GetExecutingAssembly());
            var updated = updater.Build();

            //We can inject the endpoint instance into the controller to do operations with messages.
            GlobalConfiguration.Configuration.DependencyResolver = new AutofacWebApiDependencyResolver(updated);
        }
    }
}