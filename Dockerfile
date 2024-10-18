# Step 1: Use an official .NET runtime image as the base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base


# Copy the certificates to the container
COPY cert.crt C:\Users\Can\Desktop\dotnetBackEnd\docker
COPY cert.key C:\Users\Can\Desktop\dotnetBackEnd\docker

WORKDIR /app
EXPOSE 80 443

# Step 2: Use the SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ../UrlShort/UrlShort.csproj /src/UrlShort/
RUN dotnet restore "/src/UrlShort/UrlShort.csproj"
COPY . .
WORKDIR "/src/UrlShort"
RUN dotnet build "UrlShort.csproj" -c Release -o /app/build

# Step 3: Publish the app for production
RUN dotnet publish "UrlShort.csproj" -c Release -o /app/publish

# Step 4: Create the final stage/image with the runtime
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .


ENTRYPOINT ["dotnet", "UrlShort.dll"]
