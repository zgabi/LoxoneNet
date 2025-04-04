for /d /r . %%d in (Release) do @if exist "%%d" rd /s/q "%%d"
dotnet publish --configuration Release --runtime linux-musl-arm64 --self-contained