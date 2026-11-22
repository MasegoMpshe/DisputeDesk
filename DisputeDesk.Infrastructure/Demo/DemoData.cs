using Microsoft.EntityFrameworkCore;

namespace DisputeDesk.Infrastructure;

public static class DemoData
{
    // Stable IDs keep repeat runs from overwriting or duplicating demo records.
    private const string InsertDemoData = """
        INSERT INTO customers (id, identity_user_id, display_name)
        VALUES
            ('10000000-0000-4000-8000-000000000001', 'demo-naledi', 'Naledi Mokoena'),
            ('10000000-0000-4000-8000-000000000002', 'demo-thabo', 'Thabo Dlamini')
        ON CONFLICT (id) DO NOTHING;

        INSERT INTO transactions (id, customer_id, description, amount, currency, occurred_at, merchant, city)
        VALUES
            ('20000000-0000-4000-8000-000000000001', '10000000-0000-4000-8000-000000000001', 'Grocery purchase', 385.49, 'ZAR', '2026-11-20T08:15:00Z', 'Corner Basket', 'Johannesburg'),
            ('20000000-0000-4000-8000-000000000002', '10000000-0000-4000-8000-000000000001', 'Online retail purchase', 749.00, 'ZAR', '2026-11-18T16:30:00Z', 'Market Lane Online', NULL),
            ('20000000-0000-4000-8000-000000000003', '10000000-0000-4000-8000-000000000002', 'Fuel purchase', 620.00, 'ZAR', '2026-11-19T11:20:00Z', 'Highway Fuel', 'Pretoria'),
            ('20000000-0000-4000-8000-000000000004', '10000000-0000-4000-8000-000000000002', 'Mobile airtime purchase', 120.00, 'ZAR', '2026-11-17T14:45:00Z', 'Connect Mobile', NULL)
        ON CONFLICT (id) DO NOTHING;
        """;

    public static void UseDemoData(this DbContextOptionsBuilder options)
    {
        options
            .UseSeeding((context, _) =>
                context.Database.ExecuteSqlRaw(InsertDemoData))
            .UseAsyncSeeding(async (context, _, cancellationToken) =>
                await context.Database.ExecuteSqlRawAsync(InsertDemoData, cancellationToken));
    }
}
