FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS base
WORKDIR /app

RUN apt-get update && \
    apt-get install -y \
        ghostscript \
        tesseract-ocr \
        tesseract-ocr-deu \
        tesseract-ocr-eng \
        libleptonica-dev \
        libtesseract-dev \
        libmagickwand-dev \
    && rm -rf /var/lib/apt/lists/*

ENV TESSDATA_PREFIX=/app

RUN ln -s /usr/lib/x86_64-linux-gnu/libdl.so.2 /usr/lib/x86_64-linux-gnu/libdl.so
WORKDIR /app/x64
RUN ln -s /usr/lib/x86_64-linux-gnu/liblept.so.5 /app/x64/libleptonica-1.82.0.so
RUN ln -s /usr/lib/x86_64-linux-gnu/libtesseract.so.5 /app/x64/libtesseract50.so
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Paperless.OcrWorker/Paperless.OcrWorker.csproj", "Paperless.OcrWorker/"]
RUN dotnet restore "Paperless.OcrWorker/Paperless.OcrWorker.csproj"

COPY . .

WORKDIR "/src/Paperless.OcrWorker"
RUN dotnet publish "Paperless.OcrWorker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

COPY --from=build /app/publish .
COPY --from=build /src/Paperless.OcrWorker/tessdata ./tessdata

ENV TESSDATA_PREFIX=/app

ENTRYPOINT ["dotnet", "Paperless.OcrWorker.dll"]
