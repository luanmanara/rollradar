@echo off
rem Starts the RollRadar API and web app in two visible windows, then opens the site.
rem Close the two windows to stop everything.
cd /d "%~dp0"

echo Iniciando a API e o site do RollRadar...
start "RollRadar API" powershell -NoExit -Command "dotnet run --project backend/RollRadar.Api --launch-profile http"
start "RollRadar Web" powershell -NoExit -Command "npm --prefix frontend start"

echo Aguardando o site subir para abrir o navegador...
timeout /t 20 /nobreak >nul
start "" http://localhost:4200
