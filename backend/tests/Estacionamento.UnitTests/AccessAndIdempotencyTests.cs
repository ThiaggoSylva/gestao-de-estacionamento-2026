using System.Collections.Concurrent;
using Estacionamento.Application.Common;
using Estacionamento.Application.Operations;
using Estacionamento.Domain.Common;
using Estacionamento.Domain.Identity;

namespace Estacionamento.UnitTests;

[TestClass]
public sealed class AccessAndIdempotencyTests
{
    [TestMethod]
    public void Access_guard_requires_the_expected_roles_and_authenticated_session()
    {
        var administrator = new FakeCurrentUser(Guid.NewGuid(), EmployeeRoles.Administrator);
        var cashier = new FakeCurrentUser(Guid.NewGuid(), EmployeeRoles.Cashier);
        var valet = new FakeCurrentUser(Guid.NewGuid(), EmployeeRoles.Valet);
        var anonymous = new FakeCurrentUser(Guid.Empty);

        AccessGuard.RequireAdministrator(administrator);
        AssertForbidden(() => AccessGuard.RequireAdministrator(cashier));
        AssertForbidden(() => AccessGuard.RequireAdministrator(valet));

        AccessGuard.RequireCashierOrAdministrator(administrator);
        AccessGuard.RequireCashierOrAdministrator(cashier);
        AssertForbidden(() => AccessGuard.RequireCashierOrAdministrator(valet));

        AccessGuard.RequireAuthenticatedRole(cashier);
        AssertForbidden(() => AccessGuard.RequireAuthenticatedRole(anonymous));
    }

    [TestMethod]
    public async Task Idempotent_executor_replays_only_a_matching_request_hash()
    {
        var store = new InMemoryIdempotencyStore();
        var executor = NewExecutor(store, new FakeCurrentUser(Guid.NewGuid(), EmployeeRoles.Cashier));
        var calls = 0;

        var first = await executor.ExecuteAsync(
            "check-in",
            "key-1",
            new { Plate = "ABC1D23" },
            201,
            _ => Task.FromResult(++calls),
            CancellationToken.None);

        var replay = await executor.ExecuteAsync(
            "check-in",
            "key-1",
            new { Plate = "ABC1D23" },
            201,
            _ => Task.FromResult(++calls),
            CancellationToken.None);

        Assert.AreEqual(1, first);
        Assert.AreEqual(1, replay);
        Assert.AreEqual(1, calls);
        Assert.AreEqual(1, store.Count);

        var conflict = await Assert.ThrowsExactlyAsync<DomainException>(() => executor.ExecuteAsync(
            "check-in",
            "key-1",
            new { Plate = "XYZ9Z99" },
            201,
            _ => Task.FromResult(++calls),
            CancellationToken.None));

        Assert.AreEqual(DomainErrorKind.Conflict, conflict.Kind);
        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    public async Task Idempotent_executor_scopes_keys_by_actor_and_operation()
    {
        var store = new InMemoryIdempotencyStore();
        var firstActor = NewExecutor(store, new FakeCurrentUser(Guid.NewGuid(), EmployeeRoles.Cashier));
        var secondActor = NewExecutor(store, new FakeCurrentUser(Guid.NewGuid(), EmployeeRoles.Cashier));

        var first = await firstActor.ExecuteAsync(
            "payment", "same-key", new { Amount = 10 }, 201, _ => Task.FromResult("first"), CancellationToken.None);
        var second = await secondActor.ExecuteAsync(
            "payment", "same-key", new { Amount = 10 }, 201, _ => Task.FromResult("second"), CancellationToken.None);
        var otherOperation = await firstActor.ExecuteAsync(
            "delivery", "same-key", new { Amount = 10 }, 200, _ => Task.FromResult("delivery"), CancellationToken.None);

        Assert.AreEqual("first", first);
        Assert.AreEqual("second", second);
        Assert.AreEqual("delivery", otherOperation);
        Assert.AreEqual(3, store.Count);
    }

    [TestMethod]
    public async Task Idempotent_executor_validates_key_and_authenticated_actor()
    {
        var store = new InMemoryIdempotencyStore();
        var executor = NewExecutor(store, new FakeCurrentUser(Guid.NewGuid(), EmployeeRoles.Cashier));

        var missing = await Assert.ThrowsExactlyAsync<DomainException>(() => executor.ExecuteAsync(
            "check-in", "", new { }, 201, _ => Task.FromResult(1), CancellationToken.None));
        Assert.AreEqual(DomainErrorKind.Validation, missing.Kind);

        var tooLong = await Assert.ThrowsExactlyAsync<DomainException>(() => executor.ExecuteAsync(
            "check-in", new string('k', 101), new { }, 201, _ => Task.FromResult(1), CancellationToken.None));
        Assert.AreEqual(DomainErrorKind.Validation, tooLong.Kind);

        var anonymous = NewExecutor(store, new FakeCurrentUser(Guid.Empty));
        var forbidden = await Assert.ThrowsExactlyAsync<DomainException>(() => anonymous.ExecuteAsync(
            "check-in", "key", new { }, 201, _ => Task.FromResult(1), CancellationToken.None));
        Assert.AreEqual(DomainErrorKind.Forbidden, forbidden.Kind);
    }

    [TestMethod]
    public async Task Idempotent_executor_recovers_a_concurrent_duplicate_from_the_store()
    {
        var store = new InMemoryIdempotencyStore();
        var user = new FakeCurrentUser(Guid.NewGuid(), EmployeeRoles.Cashier);
        var executor = NewExecutor(store, user);
        var calls = 0;

        var first = await executor.ExecuteAsync(
            "delivery",
            "concurrent-key",
            new { Inspection = "ok" },
            200,
            _ => Task.FromResult("stored"),
            CancellationToken.None);

        store.HideNextFind();
        var second = await executor.ExecuteAsync(
            "delivery",
            "concurrent-key",
            new { Inspection = "ok" },
            200,
            _ =>
            {
                Interlocked.Increment(ref calls);
                return Task.FromException<string>(DomainException.Conflict("A operação concorrente já concluiu."));
            },
            CancellationToken.None);

        Assert.AreEqual("stored", first);
        Assert.AreEqual("stored", second);
        Assert.AreEqual(0, calls);
    }

    private static IdempotentOperationExecutor NewExecutor(
        IIdempotencyStore store,
        ICurrentUser user)
        => new(new ImmediateUnitOfWork(), store, user, new FixedClock());

    private static void AssertForbidden(Action action)
        => Assert.AreEqual(
            DomainErrorKind.Forbidden,
            Assert.ThrowsExactly<DomainException>(action).Kind);

    private sealed class FakeCurrentUser(Guid userId, params string[] roles) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
        public string UserName => "Teste";
        public IReadOnlyCollection<string> Roles { get; } = roles;
        public bool IsInRole(string role) => Roles.Contains(role);
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow { get; } = new(2026, 11, 7, 10, 0, 0, DateTimeKind.Utc);
    }

    private sealed class ImmediateUnitOfWork : IUnitOfWork
    {
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
            => operation(cancellationToken);
    }

    private sealed class InMemoryIdempotencyStore : IIdempotencyStore
    {
        private readonly ConcurrentDictionary<(Guid Actor, string Operation, string Key), IdempotencyRecordResult> records = new();
        private int hideNextFind;

        public int Count => records.Count;

        public void HideNextFind() => Interlocked.Exchange(ref hideNextFind, 1);

        public Task<IdempotencyRecordResult?> FindAsync(
            Guid actorUserId,
            string operation,
            string key,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref hideNextFind, 0) == 1)
            {
                return Task.FromResult<IdempotencyRecordResult?>(null);
            }

            return Task.FromResult(
                records.TryGetValue((actorUserId, operation, key), out var record) ? record : null);
        }

        public Task AddAsync(
            Guid actorUserId,
            string operation,
            string key,
            string requestHash,
            string responseJson,
            int statusCode,
            DateTime createdAtUtc,
            CancellationToken cancellationToken)
        {
            records.TryAdd(
                (actorUserId, operation, key),
                new IdempotencyRecordResult(requestHash, responseJson, statusCode));
            return Task.CompletedTask;
        }
    }
}
