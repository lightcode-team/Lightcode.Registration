using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.AspNetCore.Hosting;

RegistrationHostEnvironment.LoadDotEnvIfPresent();

var builder = WebApplication.CreateBuilder(args);

builder.AddRegistrationApiHost(o =>
{
    o.RegisterRabbitMqConnection = true;
    o.EnableMvcViews = true;
});

var app = builder.Build();

app.UseRegistrationApiPipeline();

app.Run();
