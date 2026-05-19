## Локальный запуск

### 1. Backend и база данных

Из корня репозитория:

```powershell
cd backend
docker compose up --build --remove-orphans
```

Backend gateway должен быть доступен по адресу:

```text
http://localhost:7000
```

Полезные локальные адреса:

```text
Gateway health: http://localhost:7000/health
Gateway OpenAPI: http://localhost:7000/openapi/v1.json
PostgreSQL: localhost:5433
```

### 2. Frontend

В отдельном терминале:

```powershell
cd frontend
npm install
npm run dev
```

Frontend запускается по адресу:

```text
http://localhost:5173
```

В режиме разработки Vite проксирует запросы `/api`, `/health` и `/openapi` на backend gateway `http://localhost:7000`.

## Проверки

Crypto harness для backend:

```powershell
cd backend
dotnet run --project tests/ColdWarHistory.Crypto.UnitHarness/ColdWarHistory.Crypto.UnitHarness.csproj
```

Service harness для backend:

```powershell
cd backend
dotnet run --project tests/ColdWarHistory.Services.UnitHarness/ColdWarHistory.Services.UnitHarness.csproj
```

Сборка frontend:

```powershell
cd frontend
npm run build
```

## Отчет по проекту

Формальный отчет находится в:

```text
reports/Cold_War_History_Project_Report.pdf
reports/Cold_War_History_Project_Report.tex
```

Скриншоты, использованные в отчете:

```text
reports/screenshots/
```
