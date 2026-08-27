# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY HotelUK.Reviews.Api/HotelUK.Reviews.Api.csproj HotelUK.Reviews.Api/
RUN dotnet restore HotelUK.Reviews.Api/HotelUK.Reviews.Api.csproj

COPY . .
RUN dotnet publish HotelUK.Reviews.Api/HotelUK.Reviews.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---------- run ----------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

# SkiaSharp needs libfontconfig1. Without it the app starts fine and then
# crashes the first time it tries to draw text.
#
# fonts-noto-core is what lets a review written in Russian, Sinhala, Tamil or
# Chinese come out as letters instead of empty boxes on the Instagram graphic.
# Drop it if you only ever expect Latin scripts and want a smaller image.
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      libfontconfig1 fontconfig fonts-dejavu-core fonts-noto-core \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

# Heroku runs the container as a random non-root user, so a folder created here
# by root would not be writable at runtime. Instagram posting needs to write a
# temporary PNG into it, so open it up.
RUN mkdir -p /app/wwwroot/generated && chmod -R a+rwX /app/wwwroot/generated

ENV ASPNETCORE_ENVIRONMENT=Production

# Heroku picks the port at runtime, so it has to be read from the shell.
CMD ASPNETCORE_URLS=http://*:${PORT:-8080} dotnet HotelUK.Reviews.Api.dll
