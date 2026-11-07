namespace Marketplace.Shared;

/// <summary>Фиксированные id демо-товаров: одинаковы в Catalog и Warehouse.</summary>
public static class DemoData
{
    public static readonly Guid SmartphoneId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid LaptopId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid LampId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid TeaSetId = Guid.Parse("44444444-4444-4444-4444-444444444444");
}