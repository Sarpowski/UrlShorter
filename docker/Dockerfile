# Step 1: Use an official .NET runtime image as the base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80 443

# Step 2: Use the SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["UrlShort/UrlShort.csproj", "UrlShort/"]
RUN dotnet restore "UrlShort/UrlShort.csproj"
COPY . .
WORKDIR "/src/UrlShort"
RUN dotnet build "UrlShort.csproj" -c Release -o /app/build

# Step 3: Publish the app for production
RUN dotnet publish "UrlShort.csproj" -c Release -o /app/publish

# Step 4: Create the final stage/image with the runtime
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .


# Copy the certificates to the container
#COPY ../UrlShort/cert.crt /UrlShort/cert.crt
#COPY ../UrlShort/cert.key /UrlShort/cert.key
COPY UrlShort/cert.crt /app/cert.crt
COPY UrlShort/cert.key /app/cert.key

ENTRYPOINT ["dotnet", "UrlShort.dll"]
