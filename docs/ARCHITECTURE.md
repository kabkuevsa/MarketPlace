# Архитектура системы Marketplace

Тема: Маркетплейс с заказами на событиях (тема 3)
Студент: Кабкуев Сааду
Период практики: 21.10.2026 — 16.12.2026

## Что это за система

Marketplace — учебный маркетплейс на микросервисах. Каждый сервис владеет
своей базой данных и не лезет в чужие: общение идёт через HTTP (синхронно)
и через RabbitMQ (асинхронно, событиями).

Оформление заказа построено по паттерну Saga с оркестратором: сервис Orders
ведёт заказ по шагам «резерв на складе → оплата» и сам откатывает всё назад,
если какой-то шаг упал.

## Стек

| Компонент | Технология |
|-----------|------------|
| Платформа | .NET 10 LTS |
| Веб-API | ASP.NET Core Minimal API |
| ORM | EF Core 10 |
| Базы данных | PostgreSQL 16 |
| Очередь | RabbitMQ 3 (с management-консолью) |
| Клиент | Blazor Server |
| Запуск | Docker Compose |
| Логи | Serilog |
| Документация API | Swashbuckle (Swagger UI) |
| Тесты | xUnit |

## Сервисы и кто за что отвечает

| Сервис | Владеет | С кем общается |
|--------|---------|----------------|
| Identity | пользователи, роли, JWT | выдаёт токены всем |
| Catalog | товары, категории, корзина | проверяет токен Identity |
| Warehouse | остатки и резервы | принимает команды от Orders |
| Payments | платежи (эмуляция) | принимает команды от Orders |
| Orders | заказы, Saga-оркестратор | Catalog, Warehouse, Payments |
| Web | Blazor-интерфейс | Identity, Catalog, Orders |

## Как сервисы связаны

```mermaid
flowchart LR
    Web -->|HTTP| Identity
    Web -->|HTTP| Catalog
    Web -->|HTTP| Orders
    Orders -->|HTTP| Catalog
    Orders -->|HTTP| Warehouse
    Orders -->|HTTP| Payments
    Warehouse -.->|события| RabbitMQ
    Payments -.->|события| RabbitMQ
    RabbitMQ -.-> Orders
