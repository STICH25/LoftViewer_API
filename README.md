# LoftViewer API

REST API for [Rey Family Loft](https://www.reyfamilyloft.com): pigeon records, photos, authentication
and local weather. ASP.NET Core 10 + MongoDB Atlas, deployed to Railway.

Front end: [LoftViewer_FrontEnd](https://github.com/STICH25/LoftViewer_FrontEnd).

## Run locally

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
cd src/LoftViewer
dotnet user-secrets set "MongoDBSettings:Username" "<atlas user>"
dotnet user-secrets set "MongoDBSettings:Password" "<atlas password>"
dotnet user-secrets set "JwtSettings:Secret" "<random string, 32+ characters>"
dotnet user-secrets set "WeatherSettings:ApiKey" "<openweathermap key>"   # optional
cd ../..
dotnet run --project src/LoftViewer --launch-profile http
```

Swagger UI: http://localhost:5053/swagger. Health: http://localhost:5053/health.

## Test

```bash
dotnet test
```

## Deploy (Railway)

Railway builds the root `Dockerfile`. Set these service variables:

| Variable | Notes |
|---|---|
| `MongoDBSettings__Username`, `MongoDBSettings__Password` | or `MongoDBSettings__ConnectionString` |
| `JwtSettings__Secret` | 32+ random characters. Changing it signs everyone out. |
| `WeatherSettings__ApiKey` | optional |
| `Swagger__Enabled` | `true` to expose `/swagger` in production |

The container listens on `PORT` when Railway sets it, otherwise 8080.
