# Built from the repository root, where Render looks for it: the app and the two
# backend libraries it references, under src/.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /repo

COPY global.json ./
COPY src/UnoTP/UnoTP.csproj src/UnoTP/
COPY src/UnoTP.Backend/UnoTP.Backend.csproj src/UnoTP.Backend/
COPY src/UnoTP.Backend.Mock/UnoTP.Backend.Mock.csproj src/UnoTP.Backend.Mock/
RUN dotnet restore src/UnoTP/UnoTP.csproj

COPY src/ src/
RUN dotnet publish src/UnoTP/UnoTP.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV PORT=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "UnoTP.dll"]
