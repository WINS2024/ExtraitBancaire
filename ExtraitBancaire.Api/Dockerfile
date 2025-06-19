FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["ExtraitBancaire.Api/ExtraitBancaire.Api.csproj", "ExtraitBancaire.Api/"]
COPY ["ExtraitBancaire.Common/ExtraitBancaire.Common.csproj", "ExtraitBancaire.Common/"]
RUN dotnet restore "ExtraitBancaire.Api/ExtraitBancaire.Api.csproj"
COPY . .
WORKDIR "/src/ExtraitBancaire.Api"
RUN dotnet build "ExtraitBancaire.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ExtraitBancaire.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ExtraitBancaire.Api.dll"] 