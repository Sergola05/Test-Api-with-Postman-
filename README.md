# ApiTestingDemo (Structured)

ASP.NET Core 8.0 Minimal API с JWT и Swagger, модели вынесены в отдельные файлы (без ошибки CS8803).

## Запуск
1. Открой **ApiTestingDemo.csproj** в Visual Studio 2022+ (или `dotnet run` в папке проекта).
2. Swagger UI: `https://localhost:5000/swagger` (порт может отличаться).
3. Логин по умолчанию:
   - Email: `admin@example.com`
   - Password: `admin123`

## Эндпоинты
- **Auth**: `/auth/login`, `/auth/refresh`, `/auth/logout`
- **Users**: `GET /users`, `GET /users/{id}`, `POST /users`, `PUT /users/{id}`, `DELETE /users/{id}`
- **Products**: `GET /products`, `POST /products`, `PUT /products/{id}`
- **Orders**: `POST /orders`, `GET /orders?userId=`, `GET /users/{id}/orders`

## Postman
Импортируй файлы из папки `postman/`:
- `My API Testing Collection.postman_collection.json`
- `My API Env.postman_environment.json`
- `test-data.json`
