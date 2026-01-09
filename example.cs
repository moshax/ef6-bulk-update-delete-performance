
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;

// Z.EntityFramework.Extensions namespace
using Z.EntityFramework.Extensions;

namespace Ef6BulkOpsDemo
{
    // Simple entity used for demonstration.
    public class Order
    {
        public int Id { get; set; }
        public string Status { get; set; }          // e.g. "New", "Archived"
        public DateTime CreatedOn { get; set; }
        public DateTime? ArchivedOn { get; set; }
    }

    // EF6 DbContext
    public class AppDbContext : DbContext
    {
        // NOTE: Replace with a real connection string name or full connection string.
        // Example: base("name=AppDb")
        public AppDbContext() : base("name=AppDb") { }

        public DbSet<Order> Orders { get; set; }
    }

    public static class Program
    {
        public static void Main()
        {
            Console.WriteLine("EF6 bulk UPDATE/DELETE demo started.");

            using (var ctx = new AppDbContext())
            {
                // Choose a date threshold for demo filters
                var threshold = DateTime.UtcNow.AddMonths(-6);

                // 1) NAIVE EF6 PATTERN (COSTLY):
                //    - Materialize many rows
                //    - Track them
                //    - SaveChanges emits many statements
                NaiveEf6Update(ctx, threshold);
                NaiveEf6Delete(ctx, threshold);

                // 2) SET-BASED SQL (FAST, BYPASSES EF TRACKING):
                SqlSetBasedUpdate(ctx, threshold);
                SqlSetBasedDelete(ctx, threshold);

                // 3) Z.EntityFramework.Extensions (FAST BULK APIs):
                //    - BulkUpdate
                //    - BulkDelete
                EfExtensionsBulkUpdate(ctx, threshold);
                EfExtensionsBulkDelete(ctx, threshold);
            }

            Console.WriteLine("Done.");
        }

        private static void NaiveEf6Update(AppDbContext ctx, DateTime threshold)
        {
            Console.WriteLine("NaiveEf6Update: Loading entities + tracking + per-entity updates...");

            // For large sets, this causes:
            // - Large memory usage (materialization)
            // - Change tracker overhead
            // - Many UPDATE statements on SaveChanges()
            var orders = ctx.Orders
                .Where(o => o.CreatedOn < threshold && o.Status == "New")
                .ToList();

            foreach (var o in orders)
            {
                o.Status = "Archived";
                o.ArchivedOn = DateTime.UtcNow;
            }

            ctx.SaveChanges();
            Console.WriteLine($"NaiveEf6Update: Updated {orders.Count} rows.");
        }

        private static void NaiveEf6Delete(AppDbContext ctx, DateTime threshold)
        {
            Console.WriteLine("NaiveEf6Delete: Loading entities + tracking + per-entity deletes...");

            var toDelete = ctx.Orders
                .Where(o => o.CreatedOn < threshold && o.Status == "Archived")
                .ToList();

            // RemoveRange reduces some overhead vs Remove in a loop,
            // but EF6 will still typically issue many DELETEs.
            ctx.Orders.RemoveRange(toDelete);
            ctx.SaveChanges();

            Console.WriteLine($"NaiveEf6Delete: Deleted {toDelete.Count} rows.");
        }

        private static void SqlSetBasedUpdate(AppDbContext ctx, DateTime threshold)
        {
            Console.WriteLine("SqlSetBasedUpdate: Single set-based UPDATE (fast, bypasses tracking)...");

            // Important:
            // - This bypasses EF tracking. Any already-loaded entities may now be stale.
            // - You should handle caching / stale entities appropriately.
            var sql = @"
UPDATE dbo.Orders
SET Status = @pStatus,
    ArchivedOn = @pArchivedOn
WHERE CreatedOn < @pThreshold
  AND Status = @pOldStatus;
";
            var pStatus = new SqlParameter("@pStatus", "Archived");
            var pArchivedOn = new SqlParameter("@pArchivedOn", DateTime.UtcNow);
            var pThreshold = new SqlParameter("@pThreshold", threshold);
            var pOldStatus = new SqlParameter("@pOldStatus", "New");

            var affected = ctx.Database.ExecuteSqlCommand(sql, pStatus, pArchivedOn, pThreshold, pOldStatus);
            Console.WriteLine($"SqlSetBasedUpdate: Updated {affected} rows.");
        }

        private static void SqlSetBasedDelete(AppDbContext ctx, DateTime threshold)
        {
            Console.WriteLine("SqlSetBasedDelete: Single set-based DELETE (fast, bypasses tracking)...");

            var sql = @"
DELETE FROM dbo.Orders
WHERE CreatedOn < @pThreshold
  AND Status = @pStatus;
";
            var pThreshold = new SqlParameter("@pThreshold", threshold);
            var pStatus = new SqlParameter("@pStatus", "Archived");

            var affected = ctx.Database.ExecuteSqlCommand(sql, pThreshold, pStatus);
            Console.WriteLine($"SqlSetBasedDelete: Deleted {affected} rows.");
        }

        private static void EfExtensionsBulkUpdate(AppDbContext ctx, DateTime threshold)
        {
            Console.WriteLine("EfExtensionsBulkUpdate: BulkUpdate via Z.EntityFramework.Extensions...");

            // Pattern:
            // - Fetch keys + update data (can be shaped to avoid huge graphs)
            // - Then call BulkUpdate for high-performance persistence
            //
            // In real systems, keep the projection tight (only necessary columns)
            // to reduce IO and memory.
            List<Order> ordersToUpdate = ctx.Orders
                .Where(o => o.CreatedOn < threshold && o.Status == "New")
                .Select(o => new Order
                {
                    Id = o.Id,
                    Status = "Archived",
                    ArchivedOn = DateTime.UtcNow,
                    CreatedOn = o.CreatedOn
                })
                .ToList();

            // BulkUpdate updates by key (Id) efficiently.
            // You can pass options if needed (batch size, columns, temp table usage, etc.)
            ctx.BulkUpdate(ordersToUpdate);

            Console.WriteLine($"EfExtensionsBulkUpdate: Updated {ordersToUpdate.Count} rows.");
        }

        private static void EfExtensionsBulkDelete(AppDbContext ctx, DateTime threshold)
        {
            Console.WriteLine("EfExtensionsBulkDelete: BulkDelete via Z.EntityFramework.Extensions...");

            // For BulkDelete you can also shape a list of entities with only keys.
            var keysToDelete = ctx.Orders
                .Where(o => o.CreatedOn < threshold && o.Status == "Archived")
                .Select(o => new Order { Id = o.Id })
                .ToList();

            ctx.BulkDelete(keysToDelete);

            Console.WriteLine($"EfExtensionsBulkDelete: Deleted {keysToDelete.Count} rows.");
        }
    }
}
