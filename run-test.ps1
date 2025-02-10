# Start the web server
Start-Process -FilePath "dotnet" -ArgumentList "run --project .\SalouServer\SalouServer.csproj" -NoNewWindow

# Wait for the server to start
Start-Sleep -Seconds 10

# Run the tests
dotnet test .\SalouTest\SalouTest.csproj

# Optionally, stop the web server (if needed)
# Get the process ID of the web server
$webServerProcess = Get-Process | Where-Object { $_.MainWindowTitle -like "*SalouServer*" }
if ($webServerProcess) {
    $webServerProcess | Stop-Process
}
