using Lightcode.Registration.AspNetCore.Hosting;

RegistrationHostEnvironment.LoadDotEnvIfPresent();

var builder = WebApplication.CreateBuilder(args);

builder.AddRegistrationApiHost(o => o.RegisterRabbitMqConnection = true);

var app = builder.Build();

app.UseRegistrationApiPipeline();

app.Run();
