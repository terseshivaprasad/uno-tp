# Built from src/ (see render.yaml): the app and the two backend libraries it references.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY UnoTP/UnoTP.csproj UnoTP/
COPY UnoTP.Backend/UnoTP.Backend.csproj UnoTP.Backend/
COPY UnoTP.Backend.Mock/UnoTP.Backend.Mock.csproj UnoTP.Backend.Mock/
RUN dotnet restore UnoTP/UnoTP.csproj

COPY . .
RUN dotnet publish UnoTP/UnoTP.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV PORT=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "UnoTP.dll"]
