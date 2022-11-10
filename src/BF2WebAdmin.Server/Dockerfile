#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

# For use in Visual Studio
FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["src/BF2WebAdmin.Server/BF2WebAdmin.Server.csproj", "src/BF2WebAdmin.Server/"]
COPY ["src/BF2WebAdmin.Data/BF2WebAdmin.Data.csproj", "src/BF2WebAdmin.Data/"]
COPY ["src/BF2WebAdmin.Common/BF2WebAdmin.Common.csproj", "src/BF2WebAdmin.Common/"]
COPY ["src/BF2WebAdmin.Shared/BF2WebAdmin.Shared.csproj", "src/BF2WebAdmin.Shared/"]
COPY ["src/BF2WebAdmin.Blazor/BF2WebAdmin.Blazor.csproj", "src/BF2WebAdmin.Blazor/"]
COPY ./nuget.config /src/nuget.config
RUN dotnet restore "src/BF2WebAdmin.Server/BF2WebAdmin.Server.csproj" --configfile ./nuget.config
#RUN dotnet restore "src/BF2WebAdmin.Server/BF2WebAdmin.Server.csproj"
COPY . .
WORKDIR "/src/src/BF2WebAdmin.Server"
RUN dotnet build "BF2WebAdmin.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BF2WebAdmin.Server.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BF2WebAdmin.Server.dll"]