# Stage 1: Build React frontend
FROM node:22-alpine AS frontend
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ .
RUN npm run build

# Stage 2: Build .NET API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for restore
COPY src/Squirmify.sln .
COPY src/Squirmify.Core/Squirmify.Core.csproj Squirmify.Core/
COPY src/Squirmify.Data/Squirmify.Data.csproj Squirmify.Data/
COPY src/Squirmify.Services/Squirmify.Services.csproj Squirmify.Services/
COPY src/Squirmify.Api/Squirmify.Api.csproj Squirmify.Api/

RUN dotnet restore Squirmify.Api/Squirmify.Api.csproj

# Copy source and build
COPY src/ .
RUN dotnet publish Squirmify.Api/Squirmify.Api.csproj -c Release -o /app --no-restore

# Copy frontend build into wwwroot
COPY --from=frontend /web/dist /app/wwwroot

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

RUN mkdir -p /app/data

VOLUME /app/data

ENV ASPNETCORE_URLS=http://+:5105
ENV ASPNETCORE_ENVIRONMENT=Production
ENV SQUIRMIFY_DATA=/app/data

EXPOSE 5105

ENTRYPOINT ["dotnet", "Squirmify.Api.dll"]
