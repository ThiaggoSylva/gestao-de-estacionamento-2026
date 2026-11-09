using Estacionamento.Domain.Common;
using Estacionamento.Domain.Operations;

namespace Estacionamento.UnitTests;

[TestClass]
public sealed class StateTransitionTests
{
    private static readonly DateTime Now = new(2026, 11, 7, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Actor = Guid.NewGuid();

    [TestMethod]
    public void Session_moves_through_active_awaiting_searching_and_delivered()
    {
        var session = NewSession();

        Assert.AreEqual(ParkingSessionState.Active, session.State);

        session.MarkAwaitingWithdrawal();
        Assert.AreEqual(ParkingSessionState.AwaitingWithdrawal, session.State);

        session.MarkSearching();
        Assert.AreEqual(ParkingSessionState.Searching, session.State);

        session.MarkDelivered(Now.AddHours(4), Actor);
        Assert.AreEqual(ParkingSessionState.Delivered, session.State);
        Assert.AreEqual(Now.AddHours(4), session.DeliveredAtUtc);
        Assert.AreEqual(Actor, session.DeliveredByUserId);
    }

    [TestMethod]
    public void Session_rejects_out_of_order_or_duplicate_transitions()
    {
        var session = NewSession();

        AssertConflict(() => session.MarkSearching());
        AssertConflict(() => session.MarkDelivered(Now, Actor));

        session.MarkAwaitingWithdrawal();
        AssertConflict(() => session.MarkAwaitingWithdrawal());

        session.MarkDelivered(Now, Actor);
        AssertConflict(() => session.MarkDelivered(Now, Actor));
        AssertConflict(() => session.MarkAwaitingWithdrawal());
        AssertConflict(() => session.MarkSearching());
    }

    [TestMethod]
    public void Session_accepts_one_inspection_of_each_type_and_blocks_duplicates()
    {
        var session = NewSession();
        session.AddInspection(InspectionType.Entry, "Sem avarias", Actor, Now);

        var duplicate = Assert.ThrowsExactly<DomainException>(() =>
            session.AddInspection(InspectionType.Entry, "Outra vistoria", Actor, Now));
        Assert.AreEqual(DomainErrorKind.Conflict, duplicate.Kind);

        session.MarkAwaitingWithdrawal();
        session.MarkDelivered(Now, Actor);
        var afterDelivery = Assert.ThrowsExactly<DomainException>(() =>
            session.AddInspection(InspectionType.Exit, "Tentativa tardia", Actor, Now));
        Assert.AreEqual(DomainErrorKind.Conflict, afterDelivery.Kind);
    }

    [TestMethod]
    public void Withdrawal_moves_from_awaiting_to_searching_and_delivered()
    {
        var request = WithdrawalRequest.Create(
            Guid.NewGuid(),
            Now,
            Actor,
            DocumentVerification.Approved,
            null);

        Assert.AreEqual(WithdrawalState.Awaiting, request.State);

        request.StartSearch(Actor, Now.AddMinutes(5));
        Assert.AreEqual(WithdrawalState.Searching, request.State);
        Assert.AreEqual(Now.AddMinutes(5), request.SearchStartedAtUtc);
        Assert.AreEqual(Actor, request.SearchStartedByUserId);

        AssertConflict(() => request.StartSearch(Actor, Now.AddMinutes(10)));

        request.MarkDelivered(Now.AddMinutes(20));
        Assert.AreEqual(WithdrawalState.Delivered, request.State);
        Assert.AreEqual(Now.AddMinutes(20), request.DeliveredAtUtc);

        AssertConflict(() => request.MarkDelivered(Now.AddMinutes(30)));
    }

    [TestMethod]
    public void Domain_entities_validate_required_values_and_supported_ranges()
    {
        AssertValidation(() => ParkingSession.Create(
            "", Guid.NewGuid(), "ABC1D23", "Sedan", "Maria", "protected", Now, Actor, 35m));
        AssertValidation(() => ParkingSession.Create(
            "PUB-1", Guid.NewGuid(), "ABC1D23", "", "Maria", "protected", Now, Actor, 35m));
        AssertValidation(() => ParkingSession.Create(
            "PUB-1", Guid.NewGuid(), "ABC1D23", "Sedan", "Maria", "", Now, Actor, 35m));
        AssertValidation(() => ParkingSession.Create(
            "PUB-1", Guid.NewGuid(), "ABC1D23", "Sedan", "Maria", "protected", Now, Actor, 0m));
        AssertValidation(() => PlateNormalizer.Normalize("AB"));
        AssertValidation(() => PlateNormalizer.Normalize("   "));

        AssertValidation(() => Tariff.Create(0, Now, Now, Actor));
        AssertValidation(() => Tariff.Create(10.123m, Now, Now, Actor));
        AssertValidation(() => Tariff.Create(10, Now, Now, Guid.Empty));

        AssertValidation(() => ParkingSpot.Create(""));
        AssertValidation(() => ParkingSpot.Create(new string('A', 21)));

        AssertValidation(() => WithdrawalRequest.Create(
            Guid.Empty, Now, Actor, DocumentVerification.Approved, null));
        AssertValidation(() => WithdrawalRequest.Create(
            Guid.NewGuid(), Now, Actor, DocumentVerification.Approved, new string('x', 501)));

        AssertValidation(() => Payment.Create(Guid.NewGuid(), 0, PaymentMethod.Cash, Now, Actor));
        AssertValidation(() => Payment.Create(Guid.NewGuid(), 10.123m, PaymentMethod.Cash, Now, Actor));
        AssertValidation(() => Payment.Create(Guid.NewGuid(), 10, (PaymentMethod)99, Now, Actor));

        AssertValidation(() => Adjustment.Create(Guid.NewGuid(), 0, "Motivo válido", Now, Actor));
        AssertValidation(() => Adjustment.Create(Guid.NewGuid(), 10, "abc", Now, Actor));
        AssertValidation(() => Adjustment.Create(Guid.NewGuid(), 10.123m, "Motivo válido", Now, Actor));

        AssertValidation(() => ExceptionAuthorization.Create(
            Guid.NewGuid(), (ExceptionAuthorizationType)99, "Justificativa válida", Now, Actor));
        AssertValidation(() => ExceptionAuthorization.Create(
            Guid.NewGuid(), ExceptionAuthorizationType.DocumentDivergence, "abc", Now, Actor));
    }

    [TestMethod]
    public void Parking_spot_normalizes_code_and_can_be_deactivated()
    {
        var spot = ParkingSpot.Create("  a-12 ");

        Assert.AreEqual("A-12", spot.Code);
        Assert.IsTrue(spot.IsActive);
        spot.Deactivate();
        Assert.IsFalse(spot.IsActive);
    }

    private static ParkingSession NewSession()
        => ParkingSession.Create(
            "PUB-1",
            Guid.NewGuid(),
            "abc-1d23",
            "Sedan",
            "Maria",
            "protected-document",
            Now,
            Actor,
            35.00m);

    private static void AssertConflict(Action action)
        => Assert.AreEqual(
            DomainErrorKind.Conflict,
            Assert.ThrowsExactly<DomainException>(action).Kind);

    private static void AssertValidation(Action action)
        => Assert.AreEqual(
            DomainErrorKind.Validation,
            Assert.ThrowsExactly<DomainException>(action).Kind);
}
