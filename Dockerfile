# Built from the repository root, where Render looks for it: the web app and the
# database project it references.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

COPY global.json ./
COPY UnoTP/UnoTP.csproj UnoTP/
COPY UnoTP.Data/UnoTP.Data.csproj UnoTP.Data/
RUN dotnet restore UnoTP/UnoTP.csproj

COPY UnoTP/ UnoTP/
COPY UnoTP.Data/ UnoTP.Data/
RUN dotnet publish UnoTP/UnoTP.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV PORT=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "UnoTP.dll"]
