using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Estacionamento.Infrastructure.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParkingSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ResponseJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ParkingSpots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParkingSpots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tariffs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DailyRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tariffs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ParkingSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublicId = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ParkingSpotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Plate = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PlateNormalized = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    HolderName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    HolderDocumentProtected = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    EntryAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapturedDailyRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    DeliveredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeliveredByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParkingSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParkingSessions_ParkingSpots_ParkingSpotId",
                        column: x => x.ParkingSpotId,
                        principalTable: "ParkingSpots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Adjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParkingSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Adjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Adjustments_ParkingSessions_ParkingSessionId",
                        column: x => x.ParkingSessionId,
                        principalTable: "ParkingSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExceptionAuthorizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParkingSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExceptionAuthorizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExceptionAuthorizations_ParkingSessions_ParkingSessionId",
                        column: x => x.ParkingSessionId,
                        principalTable: "ParkingSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Inspections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParkingSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inspections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inspections_ParkingSessions_ParkingSessionId",
                        column: x => x.ParkingSessionId,
                        principalTable: "ParkingSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Occupancies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParkingSpotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParkingSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Occupancies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Occupancies_ParkingSessions_ParkingSessionId",
                        column: x => x.ParkingSessionId,
                        principalTable: "ParkingSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Occupancies_ParkingSpots_ParkingSpotId",
                        column: x => x.ParkingSpotId,
                        principalTable: "ParkingSpots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParkingSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_ParkingSessions_ParkingSessionId",
                        column: x => x.ParkingSessionId,
                        principalTable: "ParkingSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WithdrawalRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParkingSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentVerification = table.Column<int>(type: "int", nullable: false),
                    VerificationNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    State = table.Column<int>(type: "int", nullable: false),
                    SearchStartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SearchStartedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeliveredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WithdrawalRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WithdrawalRequests_ParkingSessions_ParkingSessionId",
                        column: x => x.ParkingSessionId,
                        principalTable: "ParkingSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Charges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParkingSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WithdrawalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ElapsedSeconds = table.Column<long>(type: "bigint", nullable: false),
                    DailyCount = table.Column<int>(type: "int", nullable: false),
                    DailyRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Charges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Charges_ParkingSessions_ParkingSessionId",
                        column: x => x.ParkingSessionId,
                        principalTable: "ParkingSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Charges_WithdrawalRequests_WithdrawalRequestId",
                        column: x => x.WithdrawalRequestId,
                        principalTable: "WithdrawalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Adjustments_ParkingSessionId",
                table: "Adjustments",
                column: "ParkingSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_ParkingSessionId_OccurredAtUtc",
                table: "AuditEntries",
                columns: new[] { "ParkingSessionId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Charges_ParkingSessionId",
                table: "Charges",
                column: "ParkingSessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Charges_WithdrawalRequestId",
                table: "Charges",
                column: "WithdrawalRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExceptionAuthorizations_ParkingSessionId_Type",
                table: "ExceptionAuthorizations",
                columns: new[] { "ParkingSessionId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_ActorUserId_Operation_Key",
                table: "IdempotencyRecords",
                columns: new[] { "ActorUserId", "Operation", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inspections_ParkingSessionId_Type",
                table: "Inspections",
                columns: new[] { "ParkingSessionId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Occupancies_ParkingSessionId",
                table: "Occupancies",
                column: "ParkingSessionId",
                unique: true,
                filter: "[EndedAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Occupancies_ParkingSpotId",
                table: "Occupancies",
                column: "ParkingSpotId",
                unique: true,
                filter: "[EndedAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ParkingSessions_ParkingSpotId",
                table: "ParkingSessions",
                column: "ParkingSpotId");

            migrationBuilder.CreateIndex(
                name: "IX_ParkingSessions_PlateNormalized_EntryAtUtc",
                table: "ParkingSessions",
                columns: new[] { "PlateNormalized", "EntryAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ParkingSessions_PublicId",
                table: "ParkingSessions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParkingSessions_State_EntryAtUtc",
                table: "ParkingSessions",
                columns: new[] { "State", "EntryAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ParkingSpots_Code",
                table: "ParkingSpots",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ParkingSessionId",
                table: "Payments",
                column: "ParkingSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Tariffs_EffectiveFromUtc",
                table: "Tariffs",
                column: "EffectiveFromUtc");

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRequests_ParkingSessionId",
                table: "WithdrawalRequests",
                column: "ParkingSessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRequests_State_RequestedAtUtc",
                table: "WithdrawalRequests",
                columns: new[] { "State", "RequestedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Adjustments");

            migrationBuilder.DropTable(
                name: "AuditEntries");

            migrationBuilder.DropTable(
                name: "Charges");

            migrationBuilder.DropTable(
                name: "ExceptionAuthorizations");

            migrationBuilder.DropTable(
                name: "IdempotencyRecords");

            migrationBuilder.DropTable(
                name: "Inspections");

            migrationBuilder.DropTable(
                name: "Occupancies");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "Tariffs");

            migrationBuilder.DropTable(
                name: "WithdrawalRequests");

            migrationBuilder.DropTable(
                name: "ParkingSessions");

            migrationBuilder.DropTable(
                name: "ParkingSpots");
        }
    }
}
