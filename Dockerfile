FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /app

# Copy solution and project files
COPY *.sln ./
COPY portal/*.csproj ./portal/

RUN dotnet restore portal/AspnetCoreMvcFull.csproj

# Copy remaining source code and publish
COPY . .
WORKDIR /app/portal
RUN dotnet publish -c Release -o /app/out

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app
COPY --from=build /app/out .

EXPOSE 8080
ENTRYPOINT ["dotnet", "AspnetCoreMvcFull.dll"]
