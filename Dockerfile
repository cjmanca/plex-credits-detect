FROM mcr.microsoft.com/dotnet/sdk:6.0 as builder
COPY ./ /src
WORKDIR /src
RUN dotnet restore && \
    dotnet publish plex-credits-detect.sln -c Release -o build /p:CopyOutputSymbolsToPublishDirectory=false

FROM mcr.microsoft.com/dotnet/runtime:6.0 AS runtime
COPY --from=builder /src/build /app
WORKDIR /app
ENV DEBIAN_FRONTEND=noninteractive
ENV XDG_CONFIG_HOME=/config
RUN mkdir -p /config && \
    chmod 777 /config && \
    apt update && \
    apt install -y ffmpeg && \
    rm -rf /var/lib/apt/lists/*
ENTRYPOINT ["dotnet", "plex-credits-detect.dll"]
VOLUME [ "/config" ]
