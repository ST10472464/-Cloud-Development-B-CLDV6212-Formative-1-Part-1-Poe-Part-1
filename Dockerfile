FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
WORKDIR "/src/Cloud Development B CLDV 6212- POE Part 1"
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0
WORKDIR /home/site/wwwroot
COPY --from=build /app/publish .
