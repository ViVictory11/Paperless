FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Paperless.OcrWorker/Paperless.OcrWorker.csproj", "Paperless.OcrWorker/"]
RUN dotnet restore "Paperless.OcrWorker/Paperless.OcrWorker.csproj"

COPY . .
WORKDIR "/src/Paperless.OcrWorker"
RUN dotnet publish "Paperless.OcrWorker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Paperless.OcrWorker.dll"]
