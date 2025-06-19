# Étape de build: compiler l'application Blazor WebAssembly
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Installation des dépendances nécessaires pour WebAssembly
RUN apt-get update && \
    apt-get install -y python3 python3-pip && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Copier les fichiers .csproj et restaurer les dépendances
COPY ["ExtraitBancaire.Client/ExtraitBancaire.Client.csproj", "ExtraitBancaire.Client/"]
COPY ["ExtraitBancaire.Common/ExtraitBancaire.Common.csproj", "ExtraitBancaire.Common/"]
RUN dotnet restore "ExtraitBancaire.Client/ExtraitBancaire.Client.csproj"
RUN dotnet workload install wasm-tools

# Copier tout le reste du code source
COPY . .

# Publier l'application
WORKDIR "/src/ExtraitBancaire.Client"
RUN dotnet build "ExtraitBancaire.Client.csproj" -c Release -o /app/build
RUN dotnet publish "ExtraitBancaire.Client.csproj" -c Release -o /app/publish

# Étape de publication: servir l'application statique avec Nginx
FROM nginx:alpine AS final
WORKDIR /usr/share/nginx/html

# Supprimer les fichiers de configuration Nginx par défaut
RUN rm -rf *

# Copier la sortie de l'application Blazor publiée
COPY --from=build /app/publish/wwwroot .

# Copier la configuration Nginx
COPY --from=build /src/ExtraitBancaire.Client/nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80