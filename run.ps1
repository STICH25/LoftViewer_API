# Change to the project directory
Set-Location "C:\Users\jaylo\RiderProjects\LoftViewer_API"

# Build the project
dotnet build LoftViewer.csproj

# Start the backend process hidden (console window will not be shown)
$backend = Start-Process "dotnet" -ArgumentList "run --project LoftViewer.csproj" -WindowStyle Hidden -PassThru

# Wait a few seconds to allow the backend to start (adjust as needed)
Start-Sleep -Seconds 2

# Open the Swagger UI in the default browser
Start-Process "https://localhost:5001/swagger"
