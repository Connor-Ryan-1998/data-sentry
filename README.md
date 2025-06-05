## Testing locally
dotnet restore
dotnet run --property WarningLevel=0


## For ADF testing, ensure cli is installed and logged in
## To distribute for win-x64

dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true

### will be stored in. requires access to dlls
bin\Release\net9.0\win-x64\publish\data-sentry.exe


### Local development
#### Secrets
dotnet user-secrets init
dotnet user-secrets set "OpsGenie:ApiKey" "KeyHere"