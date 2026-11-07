namespace Marketplace.Orders.Models;

/// <summary>Статусы Saga: от создания до завершения или отмены.</summary>
public enum OrderStatus
{
    Created,              // заказ создан
    ReservingStock,       // резервируем на складе
    StockReserved,        // склад зарезервировал
    ProcessingPayment,    // проводим оплату
    Completed,            // оплачено и зарезервировано
    Failed,               // что-то пошло не так
    Cancelled             // компенсация выполнена
}