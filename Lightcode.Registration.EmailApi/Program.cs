using Lightcode.Registration.AspNetCore.Hosting;

RegistrationHostEnvironment.LoadDotEnvIfPresent();

var builder = WebApplication.CreateBuilder(args);

builder.AddRegistrationApiHost(o =>
{
    o.RegisterRabbitMqConnection = true;
    o.RabbitMqConnectionClientName = "lightcode-registration-email-api";
});

var app = builder.Build();

app.UseRegistrationApiPipeline();

app.Run();
