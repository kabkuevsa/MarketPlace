\# Архитектура системы Marketplace



\## Общее описание



Marketplace — учебный маркетплейс на микросервисах. Каждый сервис владеет

своей базой данных и не лезет в чужие: общение идёт через HTTP (синхронно)

и RabbitMQ (асинхронно, событиями).



Оформление заказа построено по паттерну Saga с оркестратором: сервис Orders

ведёт заказ по шагам «резерв на складе → оплата» и сам откатывает всё назад,

если шаг упал.



\## Стек



| Компонент | Технология |

|-----------|------------|

| Платформа | .NET 10 LTS |

| Веб-API | ASP.NET Core Minimal API |

| ORM | EF Core 10 |

| Базы данных | PostgreSQL 16 |

| Очередь | RabbitMQ 3 (management) |

| Клиент | Blazor Server |

| Запуск | Docker Compose |

| Логи | Serilog |

| API-документация | Swashbuckle (Swagger UI) |

| Тесты | xUnit |



\## Сервисы и кто за что отвечает



| Сервис | Владеет | С кем общается |

|--------|---------|----------------|

| Identity | пользователи, роли, JWT | выдаёт токены всем |

| Catalog | товары, категории, корзина | проверяет токен Identity |

| Warehouse | остатки и резервы | принимает команды от Orders |

| Payments | платежи (эмуляция) | принимает команды от Orders |

| Orders | заказы, Saga-оркестратор | Catalog, Warehouse, Payments |

| Web | Blazor-интерфейс | Identity, Catalog, Orders |



\## Как сервисы связаны



```mermaid

flowchart LR

&#x20;   Web -->|HTTP| Identity

&#x20;   Web -->|HTTP| Catalog

&#x20;   Web -->|HTTP| Orders

&#x20;   Orders -->|HTTP| Catalog

&#x20;   Orders -->|HTTP| Warehouse

&#x20;   Orders -->|HTTP| Payments

&#x20;   Warehouse -.->|события| RabbitMQ

&#x20;   Payments -.->|события| RabbitMQ

&#x20;   RabbitMQ -.-> Orders

