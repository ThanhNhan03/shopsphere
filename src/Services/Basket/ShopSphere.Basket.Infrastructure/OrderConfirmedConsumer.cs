using MassTransit;
using ShopSphere.Contracts;
using StackExchange.Redis;
namespace ShopSphere.Basket.Infrastructure;

public sealed class BasketConfirmedConsumer(IConnectionMultiplexer redis) : IConsumer<OrderConfirmedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<OrderConfirmedIntegrationEvent> context)
    {
        var message = context.Message;
        // ponytail: seven-day deduplication; use a permanent ledger before supporting historical replay.
        var args = message.Items.SelectMany(i => new RedisValue[] { i.ProductId.ToString(), i.Quantity }).ToArray();
        await redis.GetDatabase().ScriptEvaluateAsync("""
            if redis.call('EXISTS', KEYS[2]) == 1 then return 0 end
            for i=1,#ARGV,2 do
                local q = tonumber(redis.call('HGET', KEYS[1], ARGV[i]) or '0') - tonumber(ARGV[i+1])
                if q > 0 then redis.call('HSET', KEYS[1], ARGV[i], q)
                else redis.call('HDEL', KEYS[1], ARGV[i]) end
            end
            redis.call('SET', KEYS[2], '1', 'EX', 604800)
            return 1
            """, [$"basket:{message.CustomerId}", $"basket-completed:{message.OrderId}"], args);
    }
}
