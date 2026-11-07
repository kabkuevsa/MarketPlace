namespace Marketplace.Warehouse.Models;

public enum ReservationStatus
{
    Created,    // резерв держит товар
    Confirmed,  // оплата прошла, товар списан
    Released    // компенсация: резерв снят
}