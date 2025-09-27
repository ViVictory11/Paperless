FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ./Paperless.Contracts/*.csproj ./Paperless.Contracts/
COPY ./Paperless.DAL.Service/*.csproj ./Paperless.DAL.Service/
RUN dotnet restore ./Paperless.DAL.Service/Paperless.DAL.Service.csproj

COPY . .
RUN dotnet publish ./Paperless.DAL.Service/Paperless.DAL.Service.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Paperless.DAL.Service.dll"]
