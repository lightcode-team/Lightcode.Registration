# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Lightcode.Registration.Domain/Lightcode.Registration.Domain.csproj Lightcode.Registration.Domain/
COPY Lightcode.Registration.Application/Lightcode.Registration.Application.csproj Lightcode.Registration.Application/
COPY Lightcode.Registration.Infrastructure/Lightcode.Registration.Infrastructure.csproj Lightcode.Registration.Infrastructure/
COPY Lightcode.Registration/Lightcode.Registration.csproj Lightcode.Registration/
RUN dotnet restore "Lightcode.Registration/Lightcode.Registration.csproj"

COPY Lightcode.Registration.Domain/ Lightcode.Registration.Domain/
COPY Lightcode.Registration.Application/ Lightcode.Registration.Application/
COPY Lightcode.Registration.Infrastructure/ Lightcode.Registration.Infrastructure/
COPY Lightcode.Registration/ Lightcode.Registration/
WORKDIR /src/Lightcode.Registration
RUN dotnet publish "Lightcode.Registration.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Run
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Lightcode.Registration.dll"]
