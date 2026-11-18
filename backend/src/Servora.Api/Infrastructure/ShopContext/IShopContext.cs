using Servora.Api.Features.Shops;

namespace Servora.Api.Infrastructure.ShopContext;

public interface IShopContext
{
    Task<int> GetCurrentShopIdAsync(CancellationToken cancellationToken = default);
    Task<Shop> GetCurrentWorkspaceAsync(CancellationToken cancellationToken = default);
}
