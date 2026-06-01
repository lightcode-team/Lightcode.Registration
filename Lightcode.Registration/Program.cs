using Lightcode.Registration.AspNetCore.Hosting;

RegistrationHostEnvironment.LoadDotEnvIfPresent();

var builder = WebApplication.CreateBuilder(args);

builder.AddRegistrationApiHost();

var app = builder.Build();

app.UseRegistrationApiPipeline();

app.Run();
