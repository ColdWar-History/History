# Frontend MVP

Frontend реализован как отдельное SPA на `React + TypeScript + Vite`.

## Что есть

- единый API client для gateway;
- хранение `accessToken` / `refreshToken` в `localStorage`;
- автоматический `refresh` при `401` и повтор запроса;
- публичные экраны на published-контенте;
- криптолаборатория, training, daily, inspector shift;
- профиль, лидерборд и editor/admin CRUD для контента.

## Запуск

```bash
cd frontend
npm install
npm run dev
```

По умолчанию dev-сервер работает на `http://localhost:5173`.

## API

- Gateway: `http://localhost:7000`
- Переменная окружения: `VITE_API_BASE_URL`
- Для локальной разработки Vite уже проксирует `/api`, `/openapi` и `/health` на gateway

Если хочешь ходить напрямую без прокси, можно оставить:

```bash
VITE_API_BASE_URL=http://localhost:7000
```

## Тестовый editor/admin

- `admin`
- `Admin123!`
