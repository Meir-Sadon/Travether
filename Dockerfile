# One image for Render and docker compose: the API serves the built frontend from wwwroot.

# ---------- Frontend build ----------
FROM node:22-alpine AS web
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

# ---------- API build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src/backend
COPY backend/Directory.Build.props backend/Travether.slnx ./
COPY backend/src/Travether.Api/Travether.Api.csproj src/Travether.Api/
RUN dotnet restore src/Travether.Api/Travether.Api.csproj
COPY .editorconfig /src/
COPY backend/src/ src/
RUN dotnet publish src/Travether.Api/Travether.Api.csproj -c Release -o /app --no-restore

# ---------- Runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=api /app ./
COPY --from=web /src/frontend/dist ./wwwroot
ENV PORT=8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Travether.Api.dll"]
