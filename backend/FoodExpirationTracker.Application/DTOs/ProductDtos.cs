using FoodExpirationTracker.Domain.Enums;

namespace FoodExpirationTracker.Application.DTOs;

public record AddProductRequest(string Name, string CategoryName, DateOnly ExpiryDate, int Quantity);
public record ConsumeBatchRequest(Guid BatchId, int QuantityUsed);
public record BatchDto(Guid BatchId, DateOnly ExpiryDate, int Quantity, BatchStatus Status);
public record ProductDto(Guid ProductId, string Name, string CategoryName, int TotalQuantity, List<BatchDto> Batches);
public record DashboardDto(int TotalProducts, int ExpiringSoonCount, int UsedThisMonth, int WasteThisMonth);
public record PagedResult<T>(int Page, int PageSize, int TotalCount, IReadOnlyList<T> Items);
