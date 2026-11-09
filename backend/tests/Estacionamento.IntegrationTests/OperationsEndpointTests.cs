using System.Net;
using System.Net.Http.Json;
using Estacionamento.Application.Operations;
using Estacionamento.Domain.Identity;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Estacionamento.IntegrationTests;

[TestClass]
[DoNotParallelize]
public sealed class OperationsEndpointTests
{
    [TestMethod]
    public async Task Check_in_is_idempotent_occupies_one_spot_and_masks_financial_and_document_data_for_valet()
    {
        using var factory = new TestApplicationFactory(idleTimeoutSeconds: 3600);
        await SessionEndpointTests.PrepareDatabaseAndAccounts(factory);
        var admin = await SignInAsync(factory, "admin@local.example.invalid", "TEST_ADMIN_PASSWORD");
        var cashier = await SignInAsync(factory, "caixa@local.example.invalid", "TEST_CASHIER_PASSWORD");
        var valet = await SignInAsync(factory, "manobrista@local.example.invalid", "TEST_VALET_PASSWORD");
        var (spot, _) = await CreateSpotAndTariffAsync(admin);
        var plate = UniquePlate();
        var document = "12345678901";
        var key = $"checkin-{Guid.NewGuid():N}";
        var body = new
        {
            spotId = spot.Id,
            plate,
            model = "Sedan",
            holderName = "Maria Teste",
            holderDocument = document,
            entryInspection = "Sem avarias aparentes",
        };

        var created = await SendAsync<AttendanceCreatedDto>(cashier, "/api/v1/attendances", body, key, HttpStatusCode.Created);
        var replay = await SendAsync<AttendanceCreatedDto>(cashier, "/api/v1/attendances", body, key, HttpStatusCode.Created);
        Assert.AreEqual(created.PublicId, replay.PublicId);
        Assert.AreEqual(created.Id, replay.Id);
        Assert.AreEqual("Active", created.State);

        var conflictBody = new
        {
            spotId = spot.Id,
            plate = UniquePlate(),
            model = "Sedan",
            holderName = "Maria Teste",
            holderDocument = document,
            entryInspection = "Sem avarias aparentes",
        };
        var conflict = await SendAsync<object>(cashier, "/api/v1/attendances", conflictBody, key, HttpStatusCode.Conflict);
        Assert.IsNotNull(conflict);

        var secondSpot = await CreateSpotAsync(admin);
        var keyWithoutHeader = await cashier.PostAsJsonAsync("/api/v1/attendances", new
        {
            spotId = secondSpot.Id,
            plate = UniquePlate(),
            model = "Sedan",
            holderName = "Maria Teste",
            holderDocument = document,
            entryInspection = "Sem avarias",
        });
        Assert.AreEqual(HttpStatusCode.BadRequest, keyWithoutHeader.StatusCode,
            await keyWithoutHeader.Content.ReadAsStringAsync());

        var valetSearch = await valet.GetFromJsonAsync<PagedResult<AttendanceSummaryDto>>(
            $"/api/v1/attendances?publicId={created.PublicId}");
        Assert.IsNotNull(valetSearch);
        Assert.AreEqual(1, valetSearch.TotalCount);
        var valetSummary = valetSearch.Items.Single();
        Assert.AreEqual($"***{document[^2..]}", valetSummary.Document);
        Assert.IsNull(valetSummary.ChargedAmount);
        Assert.IsNull(valetSummary.PaidAmount);
        Assert.IsNull(valetSummary.OutstandingAmount);

        var valetDetail = await valet.GetFromJsonAsync<AttendanceDetailDto>($"/api/v1/attendances/{created.Id}");
        Assert.IsNotNull(valetDetail);
        Assert.AreEqual($"***{document[^2..]}", valetDetail.Document);
        Assert.IsNull(valetDetail.Financial);
        Assert.AreEqual("Sem avarias aparentes", valetDetail.EntryInspection);
        Assert.IsFalse(valetDetail.CanRequestWithdrawal);

        var cashierDetail = await cashier.GetFromJsonAsync<AttendanceDetailDto>($"/api/v1/attendances/{created.Id}");
        Assert.IsNotNull(cashierDetail);
        Assert.AreEqual(document, cashierDetail.Document);
        Assert.IsNotNull(cashierDetail.Financial);
        Assert.IsTrue(cashierDetail.CanRequestWithdrawal);
        Assert.AreEqual(0m, cashierDetail.Financial.ChargedAmount);

        var spots = await valet.GetFromJsonAsync<List<SpotDto>>("/api/v1/spots");
        Assert.IsNotNull(spots);
        Assert.IsTrue(spots.Single(item => item.Id == spot.Id).IsOccupied);

        await AssertForbiddenAsync(valet, HttpMethod.Post, "/api/v1/spots", new { code = UniquePlate() });
    }

    [TestMethod]
    public async Task Withdrawal_payment_and_delivery_are_idempotent_and_release_the_spot()
    {
        using var factory = new TestApplicationFactory(idleTimeoutSeconds: 3600);
        await SessionEndpointTests.PrepareDatabaseAndAccounts(factory);
        var admin = await SignInAsync(factory, "admin@local.example.invalid", "TEST_ADMIN_PASSWORD");
        var cashier = await SignInAsync(factory, "caixa@local.example.invalid", "TEST_CASHIER_PASSWORD");
        var valet = await SignInAsync(factory, "manobrista@local.example.invalid", "TEST_VALET_PASSWORD");
        var (spot, _) = await CreateSpotAndTariffAsync(admin);
        var attendance = await CheckInAsync(cashier, spot.Id);

        var withdrawalKey = $"withdrawal-{Guid.NewGuid():N}";
        var withdrawalBody = new { documentVerified = true, verificationNotes = (string?)null };
        var withdrawal = await SendAsync<WithdrawalResultDto>(
            cashier,
            $"/api/v1/attendances/{attendance.Id}/withdrawals",
            withdrawalBody,
            withdrawalKey,
            HttpStatusCode.Created);
        var withdrawalReplay = await SendAsync<WithdrawalResultDto>(
            cashier,
            $"/api/v1/attendances/{attendance.Id}/withdrawals",
            withdrawalBody,
            withdrawalKey,
            HttpStatusCode.Created);
        Assert.AreEqual(withdrawal.RequestId, withdrawalReplay.RequestId);
        Assert.AreEqual(1, withdrawal.DailyCount);

        var started = await cashier.PostAsync($"/api/v1/withdrawals/{withdrawal.RequestId}/search", content: null);
        Assert.AreEqual(HttpStatusCode.OK, started.StatusCode, await started.Content.ReadAsStringAsync());

        var queue = await valet.GetFromJsonAsync<List<QueueItemDto>>("/api/v1/withdrawals/queue");
        Assert.IsNotNull(queue);
        var queueItem = queue.Single(item => item.RequestId == withdrawal.RequestId);
        Assert.AreEqual("Searching", queueItem.State);
        Assert.IsNull(queueItem.TotalAmount);
        Assert.IsNull(queueItem.OutstandingAmount);

        var paymentKey = $"payment-{Guid.NewGuid():N}";
        var paymentBody = new { amount = withdrawal.OutstandingAmount, method = "Pix" };
        var payment = await SendAsync<PaymentResultDto>(
            cashier,
            $"/api/v1/attendances/{attendance.Id}/payments",
            paymentBody,
            paymentKey,
            HttpStatusCode.Created);
        var paymentReplay = await SendAsync<PaymentResultDto>(
            cashier,
            $"/api/v1/attendances/{attendance.Id}/payments",
            paymentBody,
            paymentKey,
            HttpStatusCode.Created);
        Assert.AreEqual(payment.PaymentId, paymentReplay.PaymentId);
        Assert.AreEqual(0m, paymentReplay.OutstandingAmount);

        var overpay = await SendAsync<object>(
            cashier,
            $"/api/v1/attendances/{attendance.Id}/payments",
            paymentBody,
            $"payment-{Guid.NewGuid():N}",
            HttpStatusCode.UnprocessableEntity);
        Assert.IsNotNull(overpay);

        var deliveryKey = $"delivery-{Guid.NewGuid():N}";
        var deliveryBody = new { exitInspection = "Devolvido sem novas avarias" };
        var delivery = await SendAsync<DeliveryResultDto>(
            cashier,
            $"/api/v1/attendances/{attendance.Id}/delivery",
            deliveryBody,
            deliveryKey,
            HttpStatusCode.OK);
        var deliveryReplay = await SendAsync<DeliveryResultDto>(
            cashier,
            $"/api/v1/attendances/{attendance.Id}/delivery",
            deliveryBody,
            deliveryKey,
            HttpStatusCode.OK);
        Assert.AreEqual(delivery.DeliveredAtUtc, deliveryReplay.DeliveredAtUtc);
        Assert.AreEqual("Delivered", delivery.State);

        var duplicateDelivery = await SendAsync<object>(
            cashier,
            $"/api/v1/attendances/{attendance.Id}/delivery",
            deliveryBody,
            $"delivery-{Guid.NewGuid():N}",
            HttpStatusCode.Conflict);
        Assert.IsNotNull(duplicateDelivery);

        var spots = await valet.GetFromJsonAsync<List<SpotDto>>("/api/v1/spots");
        Assert.IsNotNull(spots);
        Assert.IsFalse(spots.Single(item => item.Id == spot.Id).IsOccupied);

        var detail = await cashier.GetFromJsonAsync<AttendanceDetailDto>($"/api/v1/attendances/{attendance.Id}");
        Assert.IsNotNull(detail);
        Assert.AreEqual("Delivered", detail.State);
        Assert.AreEqual(0m, detail.Financial?.OutstandingAmount);
    }

    [TestMethod]
    public async Task Divergent_document_requires_audited_authorization_and_admin_adjustment()
    {
        using var factory = new TestApplicationFactory(idleTimeoutSeconds: 3600);
        await SessionEndpointTests.PrepareDatabaseAndAccounts(factory);
        var admin = await SignInAsync(factory, "admin@local.example.invalid", "TEST_ADMIN_PASSWORD");
        var cashier = await SignInAsync(factory, "caixa@local.example.invalid", "TEST_CASHIER_PASSWORD");
        var valet = await SignInAsync(factory, "manobrista@local.example.invalid", "TEST_VALET_PASSWORD");
        var (spot, _) = await CreateSpotAndTariffAsync(admin);
        var attendance = await CheckInAsync(cashier, spot.Id);

        var withdrawal = await SendAsync<WithdrawalResultDto>(
            cashier,
            $"/api/v1/attendances/{attendance.Id}/withdrawals",
            new { documentVerified = false, verificationNotes = "Documento vencido" },
            $"withdrawal-{Guid.NewGuid():N}",
            HttpStatusCode.Created);

        var blockedDelivery = await SendAsync<object>(
            cashier,
            $"/api/v1/attendances/{attendance.Id}/delivery",
            new { exitInspection = "Tentativa sem autorização" },
            $"delivery-{Guid.NewGuid():N}",
            HttpStatusCode.UnprocessableEntity);
        Assert.IsNotNull(blockedDelivery);

        await AssertForbiddenAsync(cashier, HttpMethod.Post, $"/api/v1/attendances/{attendance.Id}/authorizations",
            new { type = "DocumentDivergence", reason = "Conferência manual aprovada" });
        await AssertForbiddenAsync(cashier, HttpMethod.Post, $"/api/v1/attendances/{attendance.Id}/adjustments",
            new { amount = -10m, reason = "Ajuste indevido" });

        var authorizationBody = new { type = "DocumentDivergence", reason = "Conferência manual aprovada" };
        var authorization = await SendAsync<AuthorizationResultDto>(
            admin,
            $"/api/v1/attendances/{attendance.Id}/authorizations",
            authorizationBody,
            key: null,
            HttpStatusCode.OK);
        Assert.AreEqual("DocumentDivergence", authorization.Type);

        var duplicateAuthorization = await SendAsync<object>(
            admin,
            $"/api/v1/attendances/{attendance.Id}/authorizations",
            authorizationBody,
            key: null,
            HttpStatusCode.Conflict);
        Assert.IsNotNull(duplicateAuthorization);

        var adjustment = await SendAsync<AdjustmentResultDto>(
            admin,
            $"/api/v1/attendances/{attendance.Id}/adjustments",
            new { amount = -10m, reason = "Desconto administrativo auditado" },
            key: null,
            HttpStatusCode.OK);
        Assert.AreEqual(-10m, adjustment.Amount);
        Assert.AreEqual(withdrawal.TotalAmount - 10m, adjustment.ChargedAmount);

        var newOutstanding = adjustment.OutstandingAmount;
        var payment = await SendAsync<PaymentResultDto>(
            cashier,
            $"/api/v1/attendances/{attendance.Id}/payments",
            new { amount = newOutstanding, method = "Card" },
            $"payment-{Guid.NewGuid():N}",
            HttpStatusCode.Created);
        Assert.AreEqual(0m, payment.OutstandingAmount);

        var delivery = await SendAsync<DeliveryResultDto>(
            cashier,
            $"/api/v1/attendances/{attendance.Id}/delivery",
            new { exitInspection = "Entregue após autorização" },
            $"delivery-{Guid.NewGuid():N}",
            HttpStatusCode.OK);
        Assert.AreEqual("Delivered", delivery.State);

        var cashierDetail = await cashier.GetFromJsonAsync<AttendanceDetailDto>($"/api/v1/attendances/{attendance.Id}");
        Assert.IsNotNull(cashierDetail);
        Assert.AreEqual("Delivered", cashierDetail.State);

        var audit = await admin.GetFromJsonAsync<List<AuditEntryDto>>($"/api/v1/attendances/{attendance.Id}/audit");
        Assert.IsNotNull(audit);
        CollectionAssert.Contains(audit.Select(entry => entry.Action).ToList(), "ExceptionAuthorized");
        CollectionAssert.Contains(audit.Select(entry => entry.Action).ToList(), "AdjustmentRegistered");
        CollectionAssert.Contains(audit.Select(entry => entry.Action).ToList(), "DocumentRead");

        var valetAudit = await valet.GetAsync($"/api/v1/attendances/{attendance.Id}/audit");
        Assert.AreEqual(HttpStatusCode.Forbidden, valetAudit.StatusCode);
    }

    [TestMethod]
    public async Task Concurrent_check_ins_on_the_same_spot_allow_only_one_occupancy()
    {
        using var factory = new TestApplicationFactory(idleTimeoutSeconds: 3600);
        await SessionEndpointTests.PrepareDatabaseAndAccounts(factory);
        var admin = await SignInAsync(factory, "admin@local.example.invalid", "TEST_ADMIN_PASSWORD");
        var firstCashier = await SignInAsync(factory, "caixa@local.example.invalid", "TEST_CASHIER_PASSWORD");
        var secondCashier = await SignInAsync(factory, "caixa@local.example.invalid", "TEST_CASHIER_PASSWORD");
        var (spot, _) = await CreateSpotAndTariffAsync(admin);

        var first = CheckInRequest(secondCashier, spot.Id, $"concurrent-{Guid.NewGuid():N}");
        var second = CheckInRequest(firstCashier, spot.Id, $"concurrent-{Guid.NewGuid():N}");
        var responses = await Task.WhenAll(first, second);
        var statuses = responses.Select(response => response.StatusCode).OrderBy(status => status).ToArray();

        CollectionAssert.AreEqual(
            new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }.OrderBy(status => status).ToArray(),
            statuses);
        foreach (var response in responses)
        {
            response.Dispose();
        }

        var spots = await firstCashier.GetFromJsonAsync<List<SpotDto>>("/api/v1/spots");
        Assert.IsNotNull(spots);
        Assert.IsTrue(spots.Single(item => item.Id == spot.Id).IsOccupied);
    }

    [TestMethod]
    public async Task Tariff_change_after_check_in_does_not_recalculate_the_withdrawal()
    {
        using var factory = new TestApplicationFactory(idleTimeoutSeconds: 3600);
        await SessionEndpointTests.PrepareDatabaseAndAccounts(factory);
        var admin = await SignInAsync(factory, "admin@local.example.invalid", "TEST_ADMIN_PASSWORD");
        var cashier = await SignInAsync(factory, "caixa@local.example.invalid", "TEST_CASHIER_PASSWORD");
        var spot = await CreateSpotAsync(admin);
        var capturedTariff = await SendAsync<TariffDto>(
            admin,
            "/api/v1/tariffs",
            new { dailyRate = 55.25m },
            key: null,
            HttpStatusCode.Created);
        var attendance = await CheckInAsync(cashier, spot.Id);

        var changedTariff = await SendAsync<TariffDto>(
            admin,
            "/api/v1/tariffs",
            new { dailyRate = 99.90m },
            key: null,
            HttpStatusCode.Created);
        Assert.AreNotEqual(capturedTariff.DailyRate, changedTariff.DailyRate);

        var withdrawal = await SendAsync<WithdrawalResultDto>(
            cashier,
            $"/api/v1/attendances/{attendance.Id}/withdrawals",
            new { documentVerified = true, verificationNotes = (string?)null },
            $"withdrawal-{Guid.NewGuid():N}",
            HttpStatusCode.Created);

        Assert.AreEqual(1, withdrawal.DailyCount);
        Assert.AreEqual(capturedTariff.DailyRate, withdrawal.TotalAmount);
    }

    [TestMethod]
    public async Task Outstanding_balance_requires_its_own_authorization_before_delivery()
    {
        using var factory = new TestApplicationFactory(idleTimeoutSeconds: 3600);
        await SessionEndpointTests.PrepareDatabaseAndAccounts(factory);
        var admin = await SignInAsync(factory, "admin@local.example.invalid", "TEST_ADMIN_PASSWORD");
        var cashier = await SignInAsync(factory, "caixa@local.example.invalid", "TEST_CASHIER_PASSWORD");
        var valet = await SignInAsync(factory, "manobrista@local.example.invalid", "TEST_VALET_PASSWORD");
        var (spot, _) = await CreateSpotAndTariffAsync(admin);
        var attendance = await CheckInAsync(cashier, spot.Id);
        var withdrawal = await SendAsync<WithdrawalResultDto>(
            cashier,
            $"/api/v1/attendances/{attendance.Id}/withdrawals",
            new { documentVerified = true, verificationNotes = (string?)null },
            $"withdrawal-{Guid.NewGuid():N}",
            HttpStatusCode.Created);

        var blocked = await SendAsync<object>(
            cashier,
            $"/api/v1/attendances/{attendance.Id}/delivery",
            new { exitInspection = "Saída sem quitação" },
            $"delivery-{Guid.NewGuid():N}",
            HttpStatusCode.UnprocessableEntity);
        Assert.IsNotNull(blocked);

        await SendAsync<AuthorizationResultDto>(
            admin,
            $"/api/v1/attendances/{attendance.Id}/authorizations",
            new { type = "DocumentDivergence", reason = "Tipo não cobre saldo pendente" },
            key: null,
            HttpStatusCode.OK);

        var stillBlocked = await SendAsync<object>(
            cashier,
            $"/api/v1/attendances/{attendance.Id}/delivery",
            new { exitInspection = "Saída sem quitação" },
            $"delivery-{Guid.NewGuid():N}",
            HttpStatusCode.UnprocessableEntity);
        Assert.IsNotNull(stillBlocked);

        await SendAsync<AuthorizationResultDto>(
            admin,
            $"/api/v1/attendances/{attendance.Id}/authorizations",
            new { type = "OutstandingBalance", reason = "Saldo acordado para acerto posterior" },
            key: null,
            HttpStatusCode.OK);

        var delivery = await SendAsync<DeliveryResultDto>(
            cashier,
            $"/api/v1/attendances/{attendance.Id}/delivery",
            new { exitInspection = "Veículo entregue com saldo acordado" },
            $"delivery-{Guid.NewGuid():N}",
            HttpStatusCode.OK);

        Assert.AreEqual("Delivered", delivery.State);
        Assert.AreEqual(withdrawal.TotalAmount, delivery.OutstandingAmount);
        var detail = await cashier.GetFromJsonAsync<AttendanceDetailDto>($"/api/v1/attendances/{attendance.Id}");
        Assert.IsNotNull(detail);
        Assert.AreEqual(withdrawal.TotalAmount, detail.Financial?.OutstandingAmount);
        Assert.AreEqual(0, detail.Payments.Count);

        var spots = await valet.GetFromJsonAsync<List<SpotDto>>("/api/v1/spots");
        Assert.IsNotNull(spots);
        Assert.IsFalse(spots.Single(item => item.Id == spot.Id).IsOccupied);
    }

    [TestMethod]
    public async Task Delivered_attendance_accepts_a_new_audited_adjustment_without_editing_history()
    {
        using var factory = new TestApplicationFactory(idleTimeoutSeconds: 3600);
        await SessionEndpointTests.PrepareDatabaseAndAccounts(factory);
        var admin = await SignInAsync(factory, "admin@local.example.invalid", "TEST_ADMIN_PASSWORD");
        var cashier = await SignInAsync(factory, "caixa@local.example.invalid", "TEST_CASHIER_PASSWORD");
        var (spot, _) = await CreateSpotAndTariffAsync(admin);
        var attendance = await CheckInAsync(cashier, spot.Id);
        var withdrawal = await SendAsync<WithdrawalResultDto>(
            cashier,
            $"/api/v1/attendances/{attendance.Id}/withdrawals",
            new { documentVerified = true, verificationNotes = (string?)null },
            $"withdrawal-{Guid.NewGuid():N}",
            HttpStatusCode.Created);
        await SendAsync<PaymentResultDto>(
            cashier,
            $"/api/v1/attendances/{attendance.Id}/payments",
            new { amount = withdrawal.TotalAmount, method = "Pix" },
            $"payment-{Guid.NewGuid():N}",
            HttpStatusCode.Created);
        await SendAsync<DeliveryResultDto>(
            cashier,
            $"/api/v1/attendances/{attendance.Id}/delivery",
            new { exitInspection = "Entrega concluída" },
            $"delivery-{Guid.NewGuid():N}",
            HttpStatusCode.OK);

        await AssertForbiddenAsync(
            cashier,
            HttpMethod.Post,
            $"/api/v1/attendances/{attendance.Id}/adjustments",
            new { amount = -5m, reason = "Ajuste indevido pelo caixa" });

        var adjustment = await SendAsync<AdjustmentResultDto>(
            admin,
            $"/api/v1/attendances/{attendance.Id}/adjustments",
            new { amount = -5m, reason = "Correção pós-entrega auditada" },
            key: null,
            HttpStatusCode.OK);

        Assert.AreEqual(-5m, adjustment.Amount);
        Assert.AreEqual(withdrawal.TotalAmount - 5m, adjustment.ChargedAmount);
        Assert.AreEqual(withdrawal.TotalAmount, adjustment.PaidAmount);
        Assert.AreEqual(-5m, adjustment.OutstandingAmount);

        var detail = await cashier.GetFromJsonAsync<AttendanceDetailDto>($"/api/v1/attendances/{attendance.Id}");
        Assert.IsNotNull(detail);
        Assert.AreEqual("Delivered", detail.State);
        Assert.AreEqual(1, detail.Payments.Count);
        Assert.AreEqual(1, detail.Adjustments.Count);
        Assert.AreEqual(-5m, detail.Financial?.OutstandingAmount);

        var audit = await admin.GetFromJsonAsync<List<AuditEntryDto>>($"/api/v1/attendances/{attendance.Id}/audit");
        Assert.IsNotNull(audit);
        CollectionAssert.Contains(audit.Select(entry => entry.Action).ToList(), "DeliveryCompleted");
        CollectionAssert.Contains(audit.Select(entry => entry.Action).ToList(), "AdjustmentRegistered");
    }

    private static async Task<HttpClient> SignInAsync(
        TestApplicationFactory factory,
        string email,
        string passwordEnvironmentVariable)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });
        await SessionEndpointTests.BootstrapCsrf(client);
        var response = await client.PostAsJsonAsync("/api/v1/session/login", new
        {
            email,
            password = Environment.GetEnvironmentVariable(passwordEnvironmentVariable)
                ?? throw new InvalidOperationException($"Set {passwordEnvironmentVariable} for integration tests.")
        });
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());
        await SessionEndpointTests.BootstrapCsrf(client);
        return client;
    }

    private static async Task<(SpotDto Spot, decimal DailyRate)> CreateSpotAndTariffAsync(HttpClient admin)
    {
        const decimal dailyRate = 37.50m;
        var tariff = await SendAsync<TariffDto>(
            admin,
            "/api/v1/tariffs",
            new { dailyRate },
            key: null,
            HttpStatusCode.Created);
        Assert.AreEqual(dailyRate, tariff.DailyRate);
        var spot = await CreateSpotAsync(admin);
        return (spot, dailyRate);
    }

    private static async Task<SpotDto> CreateSpotAsync(HttpClient admin)
    {
        var code = $"P{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        return await SendAsync<SpotDto>(admin, "/api/v1/spots", new { code }, key: null, HttpStatusCode.Created);
    }

    private static async Task<AttendanceCreatedDto> CheckInAsync(HttpClient cashier, Guid spotId)
        => await SendAsync<AttendanceCreatedDto>(
            cashier,
            "/api/v1/attendances",
            new
            {
                spotId,
                plate = UniquePlate(),
                model = "Hatch",
                holderName = "João Teste",
                holderDocument = "98765432100",
                entryInspection = "Sem avarias",
            },
            $"checkin-{Guid.NewGuid():N}",
            HttpStatusCode.Created);

    private static Task<HttpResponseMessage> CheckInRequest(HttpClient client, Guid spotId, string key)
        => client.SendAsync(Request(
            HttpMethod.Post,
            "/api/v1/attendances",
            new
            {
                spotId,
                plate = UniquePlate(),
                model = "Hatch",
                holderName = "João Teste",
                holderDocument = "98765432100",
                entryInspection = "Sem avarias",
            },
            key));

    private static async Task<T> SendAsync<T>(
        HttpClient client,
        string path,
        object body,
        string? key,
        HttpStatusCode expected)
    {
        var response = await client.SendAsync(Request(HttpMethod.Post, path, body, key));
        var content = await response.Content.ReadAsStringAsync();
        Assert.AreEqual(expected, response.StatusCode, content);
        if (typeof(T) == typeof(object))
        {
            return (T)(object)content;
        }

        var result = await response.Content.ReadFromJsonAsync<T>();
        Assert.IsNotNull(result, content);
        return result;
    }

    private static HttpRequestMessage Request(HttpMethod method, string path, object? body, string? key)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (key is not null)
        {
            request.Headers.Add("Idempotency-Key", key);
        }

        return request;
    }

    private static async Task AssertForbiddenAsync(HttpClient client, HttpMethod method, string path, object? body)
    {
        var response = await client.SendAsync(Request(method, path, body, key: null));
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private static string UniquePlate()
        => $"T{Guid.NewGuid():N}"[..8].ToUpperInvariant();
}
