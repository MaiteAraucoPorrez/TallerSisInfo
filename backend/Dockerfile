# Imagen del backend para publicarlo en internet (Render u otro servicio con Docker).
# Las credenciales NO van aquí: se cargan como variables de entorno en el panel del servicio (ver README, «Despliegue»).

# 1) Compilar
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS compilar
WORKDIR /src
COPY Breaku.csproj ./
RUN dotnet restore Breaku.csproj
COPY . .
RUN dotnet publish Breaku.csproj -c Release -o /app --no-restore

# 2) Ejecutar (imagen liviana, solo el runtime)
FROM mcr.microsoft.com/dotnet/aspnet:10.0
# libfontconfig1: la usa SkiaSharp al convertir el PDF del horario en imagen.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=compilar /app .
# Render espera el servicio en el puerto 10000 (su valor de PORT por defecto).
ENV ASPNETCORE_HTTP_PORTS=10000
EXPOSE 10000
USER $APP_UID
ENTRYPOINT ["dotnet", "Breaku.dll"]
