# DynamicQ

Library for building **Entity Framework Core** `IQueryable` projections from a declarative table tree (includes, nested shapes) and optional **flattening to `System.Data.DataTable`** for reporting or export scenarios.

## Requirements

- .NET 8
- EF Core 8 (pulled in as a package dependency)

## Installation

```bash
dotnet add package DynamicQ
```

## Quick start

Register services and configure registered tables:

```csharp
using DynamicQ.Extensions;

builder.Services.AddDynamicQ(options =>
{
    // Register entity types and navigation metadata your queries rely on.
    options.RegisteredTables.Add(...);
});
```

Inject `DynamicQ.DynamicQ` to build queries with `CreateProjectedQuery`, or register and use `DataTableBuilderService` when you need `FlattenToDataTable`.

## Repository

Source and issues: [github.com/Marin260/DynamicQ](https://github.com/Marin260/DynamicQ)

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE).
