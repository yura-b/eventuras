using Eventuras.Services.Auth;
using Eventuras.Services.Events;
using Eventuras.Services.Invoicing;
using Eventuras.Services.ExternalSync;
using Eventuras.Services.Organizations;
using Eventuras.Services.Registrations;
using Eventuras.Services.Users;
using Microsoft.Extensions.DependencyInjection;

namespace Eventuras.Services
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCoreServices(this IServiceCollection services)
        {
            services.AddScoped<IPaymentMethodService, PaymentMethodService>();
            services.AddScoped<StripeInvoiceProvider>();
            services.AddScoped<IRegistrationService, RegistrationService>();
            services.AddScoped<IProductsService, ProductsService>();
            services.AddScoped<IOrderService, OrderService>();
            services.AddScoped<ICertificatesService, CertificatesService>();
            services.AddScoped<IMessageLogService, MessageLogService>();
            services.AddTransient<IOrderVmConversionService, OrderVmConversionService>();
            services.AddRegistrationServices();
            services.AddOrganizationServices();
            services.AddUserServices();
            services.AddAuthServices();
            services.AddEventServices();
            services.AddExternalSyncServices();
            return services;
        }
    }
}
