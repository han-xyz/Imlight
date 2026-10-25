FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:747cbade5bfd918a175ac32f8f1d931edec2c7d0d5397a3de72eaef1ac1f66d2 AS guard
WORKDIR /src
COPY deploy/generator-input/ deploy/generator-input/
RUN set -eu; \
    cd deploy/generator-input; \
    if ! ls ./*.json >/dev/null 2>&1 || ! ls ./*Messages*.xml >/dev/null 2>&1; then \
        echo "ERROR: deploy/generator-input/ needs a WizType dump (*.json) and the client message definitions (*Messages*.xml). See deploy/generator-input/README.md." >&2; \
        exit 1; \
    fi

FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:747cbade5bfd918a175ac32f8f1d931edec2c7d0d5397a3de72eaef1ac1f66d2 AS restore
WORKDIR /src
COPY --from=guard /src/deploy/generator-input/ deploy/generator-input/
COPY src/ src/
COPY tests/ tests/
COPY submodule/ submodule/
RUN cp deploy/generator-input/*.json submodule/Imcodec/src/Imcodec.ObjectProperty/GeneratorInput/ \
    && cp deploy/generator-input/*Messages*.xml submodule/Imcodec/src/Imcodec.MessageLayer/GeneratorInput/ \
    && dotnet restore src/Imlight.sln

FROM restore AS build
RUN dotnet build src/Imlight.sln -c Release --no-restore \
    && dotnet publish src/Imlight.Director/Imlight.Director.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/runtime:10.0@sha256:0cf09aaccf6a9f68900aed4c73fb28fe25bb59ec85de4dea08ba02240a8635f3 AS runtime
WORKDIR /app
COPY --from=build /app .
RUN mkdir -p /data/db /data/spiraldb /data/logs /data/cache \
    && chown -R "$APP_UID" /data /app
USER $APP_UID
ENV IMLIGHT__Database__EmbeddedDatabaseDataDirectory=/data/db \
    IMLIGHT__Database__SpiralDBLocalPath=/data/spiraldb \
    IMLIGHT__Logging__LogPath=/data/logs/log.txt \
    IMLIGHT__Logging__LogLevel=INFO \
    IMLIGHT__Patch_Server__LocalWadCachePath=/data/cache \
    IMLIGHT__Patch_Server__PatchServerInternalUrl=http://aurorium:12369 \
    IMLIGHT__Game_Server__GameServerCount=1 \
    IMLIGHT__Game_Server__RealmNames=Centaur \
    IMLIGHT__Global_Settings__GameRevision=r806919.Wizard_1_610 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
VOLUME ["/data/db", "/data/spiraldb", "/data/logs", "/data/cache"]
EXPOSE 12000 12333 12500
ENTRYPOINT ["dotnet", "Imlight.Director.dll"]
