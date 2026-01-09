# EF6 Bulk Update/Delete Cost in .NET Framework 4.8 — and How to Optimize with Z.EntityFramework.Extensions

This repository explains why **bulk UPDATE / DELETE** operations are costly in **Entity Framework 6 (EF6)** on **.NET Framework 4.8**, and demonstrates practical optimization strategies, including **Z.EntityFramework.Extensions** bulk operations.

## Why EF6 Bulk UPDATE / DELETE Is Expensive

EF6 is optimized for **unit-of-work** patterns (small-to-medium change sets) using:
- **Change tracking**
- **Entity materialization**
- **Per-entity SQL commands** (one UPDATE/DELETE per row in most common patterns)

When you do bulk updates/deletes the “classic EF6 way” (load entities → modify/remove → `SaveChanges()`), you typically pay for:

### 1) Entity materialization and memory pressure
You pull thousands of rows into memory just to update/delete them. This increases:
- GC pressure
- RAM usage
- CPU for mapping and tracking

### 2) Change tracker overhead (O(N))
EF6 tracks every loaded entity and detects changes (often repeatedly). This overhead grows quickly as entity counts rise.

### 3) Too many database round-trips / commands
Even when EF6 uses a transaction, it still commonly emits many individual UPDATE/DELETE statements.
Result: longer execution time, more locks, and higher load.

## Baseline Optimizations (Without External Libraries)

You can reduce *some* EF6 overhead, but these are still not truly set-based bulk operations:

### A) Avoid tracking when you only read
Use `AsNoTracking()` for read-only queries. This helps reads but does not solve the “bulk write” issue.

### B) Reduce change detection overhead
Temporarily disable:
- `AutoDetectChangesEnabled`
- `ValidateOnSaveEnabled`

This can help, but EF6 will still send many commands.

### C) Prefer set-based SQL for pure bulk operations
For simple “update all rows matching condition” or “delete by condition”, the fastest *native* approach is executing SQL directly:
- `context.Database.ExecuteSqlCommand(...)`

Tradeoff: you lose EF change tracker awareness and some domain invariants, so use carefully.

## The Practical EF6 Bulk Solution: Z.EntityFramework.Extensions

**Entity Framework Extensions** (ZZZ Projects) extends EF6 with high-performance bulk operations:
- `BulkUpdate`
- `BulkDelete`
- `BulkInsert`
- `BulkMerge`
- `BulkSaveChanges`

These methods are designed to avoid the classic EF6 pattern of materializing and tracking large entity graphs for bulk writes. The library’s overview and package description highlight these bulk APIs explicitly. :contentReference[oaicite:0]{index=0}

### Installation (EF6 on .NET Framework 4.8)

**Package Manager Console**
```powershell
Install-Package Z.EntityFramework.Extensions
