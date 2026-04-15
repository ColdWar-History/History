# Backend MVP

`backend` содержит рабочий MVP-бэкенд для проекта **Cold War: History** на C# / .NET 9.

## Что реализовано

- API Gateway как единая точка входа.
- Auth-сервис: регистрация, логин, refresh, logout, roles, token introspection.
- Crypto-сервис: `caesar`, `atbash`, `vigenere`, `rail-fence`, `columnar`.
- Content-сервис: каталог шифров, исторические события, подборки, статусы публикации, редакторский CRUD.
- Game-сервис: генерация тренировок, daily challenge, базовая смена `Inspector`, подсчёт очков.
- Progress-сервис: история криптоопераций, достижения, метрики профиля, лидерборд.
- Централизованный аудит в `backend/runtime/audit/audit-log.jsonl`.
- OpenAPI JSON для каждого сервиса и gateway по пути `/openapi/v1.json`.
- Интеграционный и контрактный harness-проекты в `backend/tests`.

## Архитектура

Каждый сервис разложен по hexagonal-слоям:

- `Domain`
- `Application`
- `Infrastructure`
- `Api`

Общие зависимости вынесены в `src/Shared`:

- `BuildingBlocks.Domain`
- `BuildingBlocks.Application`
- `BuildingBlocks.Infrastructure`
- `BuildingBlocks.Contracts`
- `BuildingBlocks.Api`

Solution организован через `solution folders`, чтобы слои и bounded contexts были отделены друг от друга.

## Текущие MVP-допущения

- Хранилище пока in-memory, потому что раздел `11.3` про БД вынесен в отдельный поток работ.
- Межсервисные события для MVP реализованы HTTP-нотификациями между сервисами.
- Токены пока opaque, а не JWT, чтобы упростить запуск локального MVP без внешней инфраструктуры.
- Централизованный аудит пишется в локальный jsonl-файл.

## Порты

- Gateway: `7000`
- Auth: `7001`
- Crypto: `7002`
- Content: `7003`
- Game: `7004`
- Progress: `7005`

## Сборка

```powershell
dotnet build ColdWarHistory.Backend.sln
```

## Проверка

Интеграционный сценарий через gateway:

```powershell
dotnet run --project tests/ColdWarHistory.Gateway.IntegrationHarness/ColdWarHistory.Gateway.IntegrationHarness.csproj
```

Проверка OpenAPI-контрактов:

```powershell
dotnet run --project tests/ColdWarHistory.Contracts.Harness/ColdWarHistory.Contracts.Harness.csproj
```

## Тестовый админ

- `userName`: `admin`
- `password`: `Admin123!`

## Что удобно сделать следующим шагом

- заменить in-memory repositories на persistence-слой с отдельными БД/схемами;
- вынести HTTP event choreography в broker/outbox;
- добавить настоящие unit/integration tests на `xUnit`/`Testcontainers`, если в проекте появится нормальный пакетный доступ;
- закрыть gateway rate-limiting, retries и observability.
