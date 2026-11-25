using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Dental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidateSchemaTo28Tables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step A: Drop obsolete foreign keys, indexes, and constraints referencing tables to be removed/reshaped.
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_DiscountPresets_DiscountPresetId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_VisitServices_ServicePrices_ServicePriceId",
                table: "VisitServices");

            migrationBuilder.DropIndex(
                name: "IX_VisitServices_ServicePriceId",
                table: "VisitServices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_DiscountPresetId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceRefunds_InvoiceId_Revision",
                table: "InvoiceRefunds");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceRefunds_PaymentTransactionId",
                table: "InvoiceRefunds");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InvoiceRefunds_Payment",
                table: "InvoiceRefunds");

            // Step B: Add consolidated columns, alter nullable FKs on Appointments, and create unified Inventory tables.
            migrationBuilder.AddColumn<string>(
                name: "PostTreatmentNotesJson",
                table: "Visits",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnlockHistoryJson",
                table: "Visits",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingPhoneChangeJson",
                table: "Users",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "WorkspaceDate",
                table: "Users",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkspaceDentistId",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkspaceRoomId",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WorkspaceUpdatedAt",
                table: "Users",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkspaceUpdatedByUserId",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkspaceVersion",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublicCalledAtUtc",
                table: "QueueEntries",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PublicCalledByUserId",
                table: "QueueEntries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReturnedFromImaging",
                table: "QueueEntries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ReturnedFromSkipped",
                table: "QueueEntries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "StatusHistoryJson",
                table: "QueueEntries",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DispenseHistoryJson",
                table: "Prescriptions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OnHand",
                table: "Medicines",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "StockUpdatedAt",
                table: "Medicines",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "StockVersion",
                table: "Medicines",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "OnHand",
                table: "MedicalSupplies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "StockUpdatedAt",
                table: "MedicalSupplies",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "StockVersion",
                table: "MedicalSupplies",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "ItemsJson",
                table: "MedicalHistoryRecords",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DecidedAt",
                table: "InvoiceRefunds",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionNotes",
                table: "InvoiceRefunds",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItemsJson",
                table: "InvoiceRefunds",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "ReviewerId",
                table: "InvoiceRefunds",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Snapshot",
                table: "InvoiceRefunds",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "InvoiceRefunds",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Approved");

            migrationBuilder.AddColumn<DateTime>(
                name: "PriceEffectiveFrom",
                table: "DentalServices",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriceHistoryJson",
                table: "DentalServices",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPrice",
                table: "DentalServices",
                type: "numeric(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ClinicalAddendaJson",
                table: "DentalImages",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "PatientId",
                table: "Appointments",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "CreatedByUserId",
                table: "Appointments",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<Guid>(
                name: "ClientRequestId",
                table: "Appointments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CreatedAccount",
                table: "Appointments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "GuestDateOfBirth",
                table: "Appointments",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestEmail",
                table: "Appointments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestFullName",
                table: "Appointments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestGender",
                table: "Appointments",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestPhone",
                table: "Appointments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntentDigest",
                table: "Appointments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsReturning",
                table: "Appointments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Appointments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewedByUserId",
                table: "Appointments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Appointments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Counter");

            migrationBuilder.AddColumn<int>(
                name: "SubmittedByUserId",
                table: "Appointments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Appointments",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "InventoryTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemDomain = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TransactionType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SupplierName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    VisitId = table.Column<int>(type: "integer", nullable: true),
                    PrescriptionId = table.Column<int>(type: "integer", nullable: true),
                    DispenseEventId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ConfirmedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_Prescriptions_PrescriptionId",
                        column: x => x.PrescriptionId,
                        principalTable: "Prescriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_Users_ConfirmedByUserId",
                        column: x => x.ConfirmedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_Visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "Visits",
                        principalColumn: "VisitId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryTransactionLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TransactionId = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    PrescriptionItemId = table.Column<int>(type: "integer", nullable: true),
                    ItemCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryTransactionLines", x => x.Id);
                    table.CheckConstraint("CK_InventoryTransactionLine_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_InventoryTransactionLines_InventoryTransactions_Transaction~",
                        column: x => x.TransactionId,
                        principalTable: "InventoryTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransactionLines_PrescriptionItems_PrescriptionIte~",
                        column: x => x.PrescriptionItemId,
                        principalTable: "PrescriptionItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Step C: Idempotent data-preserving backfill from the legacy tables BEFORE dropping them.
            migrationBuilder.Sql("""
                -- 1. StaffWorkspaces -> Users
                UPDATE "Users" u
                SET "WorkspaceRoomId" = sw."RoomId",
                    "WorkspaceDentistId" = sw."DentistId",
                    "WorkspaceDate" = sw."WorkDate",
                    "WorkspaceVersion" = GREATEST(1, sw."Version"),
                    "WorkspaceUpdatedByUserId" = sw."UpdatedByUserId",
                    "WorkspaceUpdatedAt" = COALESCE(sw."UpdatedAt", sw."CreatedAt")
                FROM "StaffWorkspaces" sw
                WHERE sw."UserId" = u."UserId";

                UPDATE "Users"
                SET "WorkspaceVersion" = 1
                WHERE "WorkspaceVersion" = 0;

                -- 2. PatientPhoneChangeRequests -> Users.PendingPhoneChangeJson (latest uncompleted, else latest per user)
                UPDATE "Users" u
                SET "PendingPhoneChangeJson" = latest.payload
                FROM (
                    SELECT DISTINCT ON ("UserId")
                        "UserId",
                        jsonb_build_object(
                            'Id', "Id",
                            'UserId', "UserId",
                            'AppointmentId', "AppointmentId",
                            'CreatedByUserId', "CreatedByUserId",
                            'CurrentPhone', "CurrentPhone",
                            'NewPhone', "NewPhone",
                            'TokenVersion', "TokenVersion",
                            'CreatedAtUtc', "CreatedAtUtc",
                            'CompletedAtUtc', "CompletedAtUtc"
                        ) AS payload
                    FROM "PatientPhoneChangeRequests"
                    ORDER BY "UserId", ("CompletedAtUtc" IS NULL) DESC, "CreatedAtUtc" DESC
                ) latest
                WHERE u."UserId" = latest."UserId";

                -- 3. PostTreatmentNotes -> Visits.PostTreatmentNotesJson
                UPDATE "Visits" v
                SET "PostTreatmentNotesJson" = notes.payload
                FROM (
                    SELECT
                        "VisitId",
                        jsonb_agg(
                            jsonb_build_object(
                                'Id', "Id",
                                'VisitId', "VisitId",
                                'CreatedByUserId', "CreatedByUserId",
                                'Note', "Note",
                                'CreatedAt', "CreatedAt",
                                'RequestId', "RequestId"
                            )
                            ORDER BY "CreatedAt", "Id"
                        ) AS payload
                    FROM "PostTreatmentNotes"
                    GROUP BY "VisitId"
                ) notes
                WHERE v."VisitId" = notes."VisitId";

                -- 4. VisitUnlockRecords -> Visits.UnlockHistoryJson
                UPDATE "Visits" v
                SET "UnlockHistoryJson" = unlocks.payload
                FROM (
                    SELECT
                        "VisitId",
                        jsonb_agg(
                            jsonb_build_object(
                                'Id', "Id",
                                'VisitId', "VisitId",
                                'ActorUserId', "ActorUserId",
                                'CancelledInvoiceId', "CancelledInvoiceId",
                                'Reason', "Reason",
                                'CreatedAt', "CreatedAt"
                            )
                            ORDER BY "CreatedAt", "Id"
                        ) AS payload
                    FROM "VisitUnlockRecords"
                    GROUP BY "VisitId"
                ) unlocks
                WHERE v."VisitId" = unlocks."VisitId";

                -- 5. DentalImageClinicalAddenda -> DentalImages.ClinicalAddendaJson
                UPDATE "DentalImages" di
                SET "ClinicalAddendaJson" = addenda.payload
                FROM (
                    SELECT
                        "DentalImageId",
                        jsonb_agg(
                            jsonb_build_object(
                                'Id', "Id",
                                'DentalImageId', "DentalImageId",
                                'CreatedByUserId', "CreatedByUserId",
                                'AuthorName', "AuthorName",
                                'AuthorRole', "AuthorRole",
                                'Note', "Note",
                                'CreatedAt', "CreatedAt",
                                'RequestId', "RequestId",
                                'ImageVersion', "ImageVersion"
                            )
                            ORDER BY "CreatedAt", "Id"
                        ) AS payload
                    FROM "DentalImageClinicalAddenda"
                    GROUP BY "DentalImageId"
                ) addenda
                WHERE di."Id" = addenda."DentalImageId";

                -- 6. QueueStatusHistories -> QueueEntries
                UPDATE "QueueEntries" q
                SET "StatusHistoryJson" = hist.payload,
                    "ReturnedFromImaging" = hist.ret_imaging,
                    "ReturnedFromSkipped" = hist.ret_skipped,
                    "PublicCalledAtUtc" = hist.pub_at,
                    "PublicCalledByUserId" = hist.pub_by
                FROM (
                    SELECT
                        h."QueueEntryId",
                        jsonb_agg(
                            jsonb_build_object(
                                'QueueStatusHistoryId', h."QueueStatusHistoryId",
                                'QueueEntryId', h."QueueEntryId",
                                'FromStatus', h."FromStatus",
                                'ToStatus', h."ToStatus",
                                'ChangedByUserId', h."ChangedByUserId",
                                'ChangedAt', h."ChangedAt",
                                'Reason', h."Reason"
                            )
                            ORDER BY h."ChangedAt", h."QueueStatusHistoryId"
                        ) AS payload,
                        COALESCE(BOOL_OR(h."FromStatus" = 5 AND h."ToStatus" = 1), FALSE) AS ret_imaging,
                        COALESCE(BOOL_OR(h."FromStatus" = 7 AND h."ToStatus" = 1), FALSE) AS ret_skipped,
                        MAX(CASE WHEN h."FromStatus" = 1 AND h."ToStatus" = 6 AND h."Reason" = 'Nha sĩ gọi bệnh nhân trên TV' THEN h."ChangedAt" END) AS pub_at,
                        (ARRAY_AGG(h."ChangedByUserId" ORDER BY h."ChangedAt" DESC, h."QueueStatusHistoryId" DESC)
                            FILTER (WHERE h."FromStatus" = 1 AND h."ToStatus" = 6 AND h."Reason" = 'Nha sĩ gọi bệnh nhân trên TV'))[1] AS pub_by
                    FROM "QueueStatusHistories" h
                    GROUP BY h."QueueEntryId"
                ) hist
                WHERE q."QueueEntryId" = hist."QueueEntryId";

                -- 7. ServicePrices -> DentalServices
                UPDATE "DentalServices" ds
                SET "PriceHistoryJson" = sp.payload
                FROM (
                    SELECT
                        "ServiceId",
                        jsonb_agg(
                            jsonb_build_object(
                                'ServicePriceId', "ServicePriceId",
                                'ServiceId', "ServiceId",
                                'Amount', "Amount",
                                'EffectiveFrom', "EffectiveFrom",
                                'CreatedByUserId', "CreatedByUserId",
                                'CreatedAt', "CreatedAt",
                                'UpdatedAt', "UpdatedAt"
                            )
                            ORDER BY "EffectiveFrom", "ServicePriceId"
                        ) AS payload
                    FROM "ServicePrices"
                    GROUP BY "ServiceId"
                ) sp
                WHERE ds."DentalServiceId" = sp."ServiceId";

                UPDATE "DentalServices" ds
                SET "UnitPrice" = curr."Amount",
                    "PriceEffectiveFrom" = curr."EffectiveFrom"
                FROM (
                    SELECT DISTINCT ON ("ServiceId")
                        "ServiceId", "Amount", "EffectiveFrom"
                    FROM "ServicePrices"
                    ORDER BY "ServiceId", ("EffectiveFrom" <= NOW()) DESC, "EffectiveFrom" DESC, "ServicePriceId" DESC
                ) curr
                WHERE ds."DentalServiceId" = curr."ServiceId";

                -- 8. MedicalHistoryItems -> MedicalHistoryRecords.ItemsJson
                UPDATE "MedicalHistoryRecords" r
                SET "ItemsJson" = mi.payload
                FROM (
                    SELECT
                        "RecordId",
                        jsonb_agg(
                            jsonb_build_object(
                                'ItemId', "ItemId",
                                'RecordId', "RecordId",
                                'Type', "Type",
                                'Name', "Name",
                                'IsCritical', "IsCritical",
                                'Detail', "Detail"
                            )
                            ORDER BY "ItemId"
                        ) AS payload
                    FROM "MedicalHistoryItems"
                    GROUP BY "RecordId"
                ) mi
                WHERE r."RecordId" = mi."RecordId";

                -- 9. PrescriptionDispenseEvents -> Prescriptions.DispenseHistoryJson
                UPDATE "Prescriptions" p
                SET "DispenseHistoryJson" = de.payload
                FROM (
                    SELECT
                        "PrescriptionId",
                        jsonb_agg(
                            jsonb_build_object(
                                'Id', "Id",
                                'PrescriptionId', "PrescriptionId",
                                'PrescriptionVersion', "PrescriptionVersion",
                                'Action', "Action",
                                'PerformedAt', "PerformedAt",
                                'PerformedBy', "PerformedBy",
                                'Reason', "Reason",
                                'ReceivedByProxy', "ReceivedByProxy",
                                'ReceiverName', "ReceiverName",
                                'ReceiverPhone', "ReceiverPhone",
                                'ExpiryOverridden', "ExpiryOverridden"
                            )
                            ORDER BY "PerformedAt", "Id"
                        ) AS payload
                    FROM "PrescriptionDispenseEvents"
                    GROUP BY "PrescriptionId"
                ) de
                WHERE p."Id" = de."PrescriptionId";

                -- 10. StockBalances -> MedicalSupplies & MedicineStockBalances -> Medicines
                UPDATE "MedicalSupplies" ms
                SET "OnHand" = sb."OnHand",
                    "StockVersion" = GREATEST(1, sb."Version"),
                    "StockUpdatedAt" = sb."UpdatedAt"
                FROM "StockBalances" sb
                WHERE sb."MedicalSupplyId" = ms."Id";

                UPDATE "MedicalSupplies"
                SET "StockVersion" = 1
                WHERE "StockVersion" = 0;

                UPDATE "Medicines" m
                SET "OnHand" = mb."OnHand",
                    "StockVersion" = GREATEST(1, mb."Version"),
                    "StockUpdatedAt" = mb."UpdatedAt"
                FROM "MedicineStockBalances" mb
                WHERE mb."MedicineId" = m."Id";

                UPDATE "Medicines"
                SET "StockVersion" = 1
                WHERE "StockVersion" = 0;

                -- 11. Unified InventoryTransactions & InventoryTransactionLines (Supply + Medicine)
                DO $$
                DECLARE
                    rec RECORD;
                    new_tx_id INT;
                BEGIN
                    FOR rec IN SELECT * FROM "StockReceipts" ORDER BY "Id" LOOP
                        INSERT INTO "InventoryTransactions" (
                            "ItemDomain", "TransactionType", "RequestId", "PayloadHash", "SupplierName",
                            "Notes", "ConfirmedByUserId", "ConfirmedAt"
                        )
                        VALUES (
                            'Supply', 'Receipt', rec."RequestId", rec."PayloadHash", rec."SupplierName",
                            rec."Notes", rec."ConfirmedByUserId", rec."ConfirmedAt"
                        )
                        RETURNING "Id" INTO new_tx_id;

                        INSERT INTO "InventoryTransactionLines" (
                            "TransactionId", "ItemId", "ItemCode", "ItemName", "Unit", "Quantity"
                        )
                        SELECT new_tx_id, l."MedicalSupplyId", l."SupplyCode", l."SupplyName", l."Unit", l."Quantity"
                        FROM "StockReceiptLines" l
                        WHERE l."StockReceiptId" = rec."Id"
                        ORDER BY l."Id";
                    END LOOP;

                    FOR rec IN SELECT * FROM "StockIssues" ORDER BY "Id" LOOP
                        INSERT INTO "InventoryTransactions" (
                            "ItemDomain", "TransactionType", "RequestId", "PayloadHash", "VisitId",
                            "Notes", "ConfirmedByUserId", "ConfirmedAt"
                        )
                        VALUES (
                            'Supply', 'Issue', rec."RequestId", rec."PayloadHash", rec."VisitId",
                            rec."Notes", rec."ConfirmedByUserId", rec."ConfirmedAt"
                        )
                        RETURNING "Id" INTO new_tx_id;

                        INSERT INTO "InventoryTransactionLines" (
                            "TransactionId", "ItemId", "ItemCode", "ItemName", "Unit", "Quantity"
                        )
                        SELECT new_tx_id, l."MedicalSupplyId", l."SupplyCode", l."SupplyName", l."Unit", l."Quantity"
                        FROM "StockIssueLines" l
                        WHERE l."StockIssueId" = rec."Id"
                        ORDER BY l."Id";
                    END LOOP;

                    FOR rec IN SELECT * FROM "MedicineStockReceipts" ORDER BY "Id" LOOP
                        INSERT INTO "InventoryTransactions" (
                            "ItemDomain", "TransactionType", "RequestId", "PayloadHash", "SupplierName",
                            "Notes", "ConfirmedByUserId", "ConfirmedAt"
                        )
                        VALUES (
                            'Medicine', 'Receipt', rec."RequestId", rec."PayloadHash", rec."SupplierName",
                            rec."Notes", rec."ConfirmedByUserId", rec."ConfirmedAt"
                        )
                        RETURNING "Id" INTO new_tx_id;

                        INSERT INTO "InventoryTransactionLines" (
                            "TransactionId", "ItemId", "ItemCode", "ItemName", "Unit", "Quantity"
                        )
                        VALUES (
                            new_tx_id, rec."MedicineId", rec."MedicineCode", rec."MedicineName", rec."Unit", rec."Quantity"
                        );
                    END LOOP;

                    FOR rec IN
                        SELECT DISTINCT "PrescriptionId", "DispenseEventId", "IssuedByUserId", "IssuedAt"
                        FROM "MedicineStockIssues"
                        ORDER BY "IssuedAt", "PrescriptionId", "DispenseEventId"
                    LOOP
                        INSERT INTO "InventoryTransactions" (
                            "ItemDomain", "TransactionType", "PrescriptionId", "DispenseEventId",
                            "ConfirmedByUserId", "ConfirmedAt"
                        )
                        VALUES (
                            'Medicine', 'Issue', rec."PrescriptionId", rec."DispenseEventId",
                            rec."IssuedByUserId", rec."IssuedAt"
                        )
                        RETURNING "Id" INTO new_tx_id;

                        INSERT INTO "InventoryTransactionLines" (
                            "TransactionId", "ItemId", "PrescriptionItemId", "ItemCode", "ItemName", "Unit", "Quantity"
                        )
                        SELECT new_tx_id, m."MedicineId", m."PrescriptionItemId", m."MedicineCode", m."MedicineName", m."Unit", m."Quantity"
                        FROM "MedicineStockIssues" m
                        WHERE m."PrescriptionId" = rec."PrescriptionId"
                          AND m."DispenseEventId" = rec."DispenseEventId"
                          AND m."IssuedByUserId" = rec."IssuedByUserId"
                          AND m."IssuedAt" = rec."IssuedAt"
                        ORDER BY m."Id";
                    END LOOP;
                END $$;

                -- 12. DiscountPresets -> SystemConfigs + remap Invoices.DiscountPresetId
                DO $$
                DECLARE
                    dp RECORD;
                    new_cfg_id INT;
                BEGIN
                    CREATE TEMP TABLE _dp_map (old_id INT PRIMARY KEY, new_id INT NOT NULL) ON COMMIT DROP;

                    FOR dp IN SELECT * FROM "DiscountPresets" ORDER BY "Id" LOOP
                        SELECT "Id" INTO new_cfg_id
                        FROM "SystemConfigs"
                        WHERE "ConfigKey" = 'Billing:DiscountPreset:' || dp."Code";

                        IF new_cfg_id IS NULL THEN
                            INSERT INTO "SystemConfigs" ("ConfigKey", "ConfigValue", "Description", "DataType")
                            VALUES (
                                'Billing:DiscountPreset:' || dp."Code",
                                jsonb_build_object(
                                    'Code', dp."Code",
                                    'Name', dp."Name",
                                    'DiscountType', dp."DiscountType",
                                    'Value', dp."Value",
                                    'IsActive', dp."IsActive"
                                )::text,
                                dp."Name",
                                'DiscountPreset'
                            )
                            RETURNING "Id" INTO new_cfg_id;
                        END IF;

                        INSERT INTO _dp_map (old_id, new_id) VALUES (dp."Id", new_cfg_id);
                    END LOOP;

                    UPDATE "Invoices" i
                    SET "DiscountPresetId" = m.new_id
                    FROM _dp_map m
                    WHERE i."DiscountPresetId" = m.old_id;
                END $$;

                -- 13. InvoiceRefundItems + InvoiceRefundProposals + InvoiceRefundProposalItems -> InvoiceRefunds
                UPDATE "InvoiceRefunds" r
                SET "Status" = 'Approved',
                    "ItemsJson" = COALESCE(ri.payload, '[]'::jsonb)
                FROM (
                    SELECT
                        "InvoiceRefundId",
                        jsonb_agg(
                            jsonb_build_object(
                                'Id', "Id",
                                'InvoiceRefundId', "InvoiceRefundId",
                                'InvoiceItemId', "InvoiceItemId",
                                'Quantity', "Quantity"
                            )
                            ORDER BY "Id"
                        ) AS payload
                    FROM "InvoiceRefundItems"
                    GROUP BY "InvoiceRefundId"
                ) ri
                WHERE r."Id" = ri."InvoiceRefundId";

                UPDATE "InvoiceRefunds" r
                SET "Snapshot" = COALESCE(NULLIF(r."Snapshot", ''), p."Snapshot"),
                    "ReviewerId" = COALESCE(p."ReviewerId", r."ActorId"),
                    "ActorId" = p."CreatorId",
                    "DecidedAt" = p."DecidedAt",
                    "DecisionNotes" = p."DecisionNotes"
                FROM "InvoiceRefundProposals" p
                WHERE p."RefundId" = r."Id";

                INSERT INTO "InvoiceRefunds" (
                    "InvoiceId", "Revision", "RequestId", "PayloadHash", "Snapshot", "Status",
                    "ActorId", "CreatedAt", "ReviewerId", "DecidedAt", "DecisionNotes",
                    "PaymentTransactionId", "RefundAmount", "DebtReduction",
                    "TotalBefore", "DiscountBefore", "PaidBefore",
                    "TotalAfter", "DiscountAfter", "PaidAfter",
                    "ReasonCode", "ReasonNotes", "PaymentMethod",
                    "LateApproval", "ReserveConfirmed", "ItemsJson"
                )
                SELECT
                    p."InvoiceId", 0, p."RequestId", '', p."Snapshot", p."Status",
                    p."CreatorId", p."CreatedAt", p."ReviewerId", p."DecidedAt", p."DecisionNotes",
                    NULL, 0, 0,
                    0, 0, 0,
                    0, 0, 0,
                    p."ReasonCode", p."ReasonNotes", 'Cash',
                    FALSE, FALSE,
                    COALESCE((
                        SELECT jsonb_agg(
                            jsonb_build_object(
                                'Id', pi."Id",
                                'InvoiceRefundId', 0,
                                'InvoiceItemId', pi."InvoiceItemId",
                                'Quantity', pi."Quantity"
                            )
                            ORDER BY pi."Id"
                        )
                        FROM "InvoiceRefundProposalItems" pi
                        WHERE pi."ProposalId" = p."Id"
                    ), '[]'::jsonb)
                FROM "InvoiceRefundProposals" p
                WHERE p."RefundId" IS NULL
                  AND NOT EXISTS (SELECT 1 FROM "InvoiceRefunds" r WHERE r."RequestId" = p."RequestId");

                -- 14. BookingRequests -> Appointments
                UPDATE "Appointments" a
                SET "Source" = 'Online',
                    "ClientRequestId" = br."ClientRequestId",
                    "IntentDigest" = br."IntentDigest",
                    "GuestFullName" = br."FullName",
                    "GuestPhone" = br."Phone",
                    "GuestDateOfBirth" = br."DateOfBirth",
                    "GuestGender" = br."Gender",
                    "GuestEmail" = br."Email",
                    "SubmittedByUserId" = br."SubmittedByUserId",
                    "IsReturning" = br."IsReturning",
                    "Version" = GREATEST(1, br."Version"),
                    "ReviewedByUserId" = br."ReviewedByUserId",
                    "RejectionReason" = br."RejectionReason",
                    "CreatedAccount" = br."CreatedAccount"
                FROM "BookingRequests" br
                WHERE br."ApprovedAppointmentId" = a."AppointmentId";

                INSERT INTO "Appointments" (
                    "PatientId", "AppointmentDate", "SlotTime", "SlotDurationMinutes",
                    "Status", "Source", "Notes", "RescheduleCount", "IsEmergency",
                    "CreatedByUserId", "CreatedAt", "UpdatedAt",
                    "ClientRequestId", "IntentDigest", "GuestFullName", "GuestPhone",
                    "GuestDateOfBirth", "GuestGender", "GuestEmail", "SubmittedByUserId",
                    "IsReturning", "Version", "ReviewedByUserId", "RejectionReason", "CreatedAccount"
                )
                SELECT
                    br."VerifiedPatientId", br."AppointmentDate", br."SlotTime", br."SlotDurationMinutes",
                    br."Status", 'Online', br."Notes", 0, FALSE,
                    NULL, br."CreatedAt", br."UpdatedAt",
                    br."ClientRequestId", br."IntentDigest", br."FullName", br."Phone",
                    br."DateOfBirth", br."Gender", br."Email", br."SubmittedByUserId",
                    br."IsReturning", GREATEST(1, br."Version"), br."ReviewedByUserId", br."RejectionReason", br."CreatedAccount"
                FROM "BookingRequests" br
                WHERE br."ApprovedAppointmentId" IS NULL;
                """);

            // Step D: Drop all 25 consolidated/retired tables now that data has been migrated.
            migrationBuilder.DropTable(
                name: "BookingRequests");

            migrationBuilder.DropTable(
                name: "DentalImageClinicalAddenda");

            migrationBuilder.DropTable(
                name: "DiscountPresets");

            migrationBuilder.DropTable(
                name: "InvoiceNumberCounters");

            migrationBuilder.DropTable(
                name: "InvoiceRefundItems");

            migrationBuilder.DropTable(
                name: "InvoiceRefundProposalItems");

            migrationBuilder.DropTable(
                name: "MedicalHistoryItems");

            migrationBuilder.DropTable(
                name: "MedicineStockBalances");

            migrationBuilder.DropTable(
                name: "MedicineStockIssues");

            migrationBuilder.DropTable(
                name: "MedicineStockReceipts");

            migrationBuilder.DropTable(
                name: "NotificationLogs");

            migrationBuilder.DropTable(
                name: "PatientPhoneChangeRequests");

            migrationBuilder.DropTable(
                name: "PostTreatmentNotes");

            migrationBuilder.DropTable(
                name: "PrescriptionNumberCounters");

            migrationBuilder.DropTable(
                name: "QueueStatusHistories");

            migrationBuilder.DropTable(
                name: "ServicePrices");

            migrationBuilder.DropTable(
                name: "StaffWorkspaces");

            migrationBuilder.DropTable(
                name: "StockBalances");

            migrationBuilder.DropTable(
                name: "StockIssueLines");

            migrationBuilder.DropTable(
                name: "StockReceiptLines");

            migrationBuilder.DropTable(
                name: "VisitUnlockRecords");

            migrationBuilder.DropTable(
                name: "InvoiceRefundProposals");

            migrationBuilder.DropTable(
                name: "PrescriptionDispenseEvents");

            migrationBuilder.DropTable(
                name: "StockIssues");

            migrationBuilder.DropTable(
                name: "StockReceipts");

            // Step E: Create new indexes, check constraints, and foreign keys.
            migrationBuilder.CreateIndex(
                name: "IX_Users_WorkspaceDentistId",
                table: "Users",
                column: "WorkspaceDentistId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_WorkspaceRoomId",
                table: "Users",
                column: "WorkspaceRoomId",
                unique: true,
                filter: "\"WorkspaceRoomId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_WorkspaceUpdatedByUserId",
                table: "Users",
                column: "WorkspaceUpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_QueueEntries_PublicCalledByUserId",
                table: "QueueEntries",
                column: "PublicCalledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_QueueEntries_PublicCalls",
                table: "QueueEntries",
                columns: new[] { "QueueDate", "Status", "PublicCalledAtUtc" },
                filter: "\"PublicCalledAtUtc\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Medicines_OnHand",
                table: "Medicines",
                sql: "\"OnHand\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MedicalSupplies_OnHand",
                table: "MedicalSupplies",
                sql: "\"OnHand\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRefunds_InvoiceId_Revision",
                table: "InvoiceRefunds",
                columns: new[] { "InvoiceId", "Revision" },
                unique: true,
                filter: "\"Status\" = 'Approved'");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRefunds_PaymentTransactionId",
                table: "InvoiceRefunds",
                column: "PaymentTransactionId",
                unique: true,
                filter: "\"PaymentTransactionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRefunds_ReviewerId",
                table: "InvoiceRefunds",
                column: "ReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRefunds_Status_CreatedAt",
                table: "InvoiceRefunds",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvoiceRefunds_Payment",
                table: "InvoiceRefunds",
                sql: "(\"Status\" <> 'Approved' AND \"PaymentTransactionId\" IS NULL AND \"RefundAmount\" = 0) OR (\"Status\" = 'Approved' AND ((\"RefundAmount\" = 0 AND \"PaymentTransactionId\" IS NULL) OR (\"RefundAmount\" > 0 AND \"PaymentTransactionId\" IS NOT NULL)))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvoiceRefunds_Status",
                table: "InvoiceRefunds",
                sql: "\"Status\" IN ('Pending','Approved','Rejected','Stale')");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_ClientRequestId",
                table: "Appointments",
                column: "ClientRequestId",
                unique: true,
                filter: "\"ClientRequestId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_ReviewedByUserId",
                table: "Appointments",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_Status_AppointmentDate_SlotTime",
                table: "Appointments",
                columns: new[] { "Status", "AppointmentDate", "SlotTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_SubmittedByUserId",
                table: "Appointments",
                column: "SubmittedByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Appointments_Version",
                table: "Appointments",
                sql: "\"Version\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactionLines_ItemId",
                table: "InventoryTransactionLines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactionLines_PrescriptionItemId",
                table: "InventoryTransactionLines",
                column: "PrescriptionItemId",
                unique: true,
                filter: "\"PrescriptionItemId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactionLines_TransactionId_ItemId",
                table: "InventoryTransactionLines",
                columns: new[] { "TransactionId", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_ConfirmedByUserId",
                table: "InventoryTransactions",
                column: "ConfirmedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_ItemDomain_TransactionType_Id",
                table: "InventoryTransactions",
                columns: new[] { "ItemDomain", "TransactionType", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_ItemDomain_TransactionType_RequestId",
                table: "InventoryTransactions",
                columns: new[] { "ItemDomain", "TransactionType", "RequestId" },
                unique: true,
                filter: "\"RequestId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_PrescriptionId",
                table: "InventoryTransactions",
                column: "PrescriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_VisitId",
                table: "InventoryTransactions",
                column: "VisitId");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_Users_ReviewedByUserId",
                table: "Appointments",
                column: "ReviewedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_Users_SubmittedByUserId",
                table: "Appointments",
                column: "SubmittedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceRefunds_Users_ReviewerId",
                table: "InvoiceRefunds",
                column: "ReviewerId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QueueEntries_Users_PublicCalledByUserId",
                table: "QueueEntries",
                column: "PublicCalledByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_ClinicRooms_WorkspaceRoomId",
                table: "Users",
                column: "WorkspaceRoomId",
                principalTable: "ClinicRooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Users_WorkspaceDentistId",
                table: "Users",
                column: "WorkspaceDentistId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Users_WorkspaceUpdatedByUserId",
                table: "Users",
                column: "WorkspaceUpdatedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_Users_ReviewedByUserId",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_Users_SubmittedByUserId",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceRefunds_Users_ReviewerId",
                table: "InvoiceRefunds");

            migrationBuilder.DropForeignKey(
                name: "FK_QueueEntries_Users_PublicCalledByUserId",
                table: "QueueEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_ClinicRooms_WorkspaceRoomId",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Users_WorkspaceDentistId",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Users_WorkspaceUpdatedByUserId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "InventoryTransactionLines");

            migrationBuilder.DropTable(
                name: "InventoryTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Users_WorkspaceDentistId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_WorkspaceRoomId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_WorkspaceUpdatedByUserId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_QueueEntries_PublicCalledByUserId",
                table: "QueueEntries");

            migrationBuilder.DropIndex(
                name: "IX_QueueEntries_PublicCalls",
                table: "QueueEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Medicines_OnHand",
                table: "Medicines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MedicalSupplies_OnHand",
                table: "MedicalSupplies");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceRefunds_InvoiceId_Revision",
                table: "InvoiceRefunds");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceRefunds_PaymentTransactionId",
                table: "InvoiceRefunds");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceRefunds_ReviewerId",
                table: "InvoiceRefunds");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceRefunds_Status_CreatedAt",
                table: "InvoiceRefunds");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InvoiceRefunds_Payment",
                table: "InvoiceRefunds");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InvoiceRefunds_Status",
                table: "InvoiceRefunds");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_ClientRequestId",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_ReviewedByUserId",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_Status_AppointmentDate_SlotTime",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_SubmittedByUserId",
                table: "Appointments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Appointments_Version",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "PostTreatmentNotesJson",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "UnlockHistoryJson",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "PendingPhoneChangeJson",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "WorkspaceDate",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "WorkspaceDentistId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "WorkspaceRoomId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "WorkspaceUpdatedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "WorkspaceUpdatedByUserId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "WorkspaceVersion",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PublicCalledAtUtc",
                table: "QueueEntries");

            migrationBuilder.DropColumn(
                name: "PublicCalledByUserId",
                table: "QueueEntries");

            migrationBuilder.DropColumn(
                name: "ReturnedFromImaging",
                table: "QueueEntries");

            migrationBuilder.DropColumn(
                name: "ReturnedFromSkipped",
                table: "QueueEntries");

            migrationBuilder.DropColumn(
                name: "StatusHistoryJson",
                table: "QueueEntries");

            migrationBuilder.DropColumn(
                name: "DispenseHistoryJson",
                table: "Prescriptions");

            migrationBuilder.DropColumn(
                name: "OnHand",
                table: "Medicines");

            migrationBuilder.DropColumn(
                name: "StockUpdatedAt",
                table: "Medicines");

            migrationBuilder.DropColumn(
                name: "StockVersion",
                table: "Medicines");

            migrationBuilder.DropColumn(
                name: "OnHand",
                table: "MedicalSupplies");

            migrationBuilder.DropColumn(
                name: "StockUpdatedAt",
                table: "MedicalSupplies");

            migrationBuilder.DropColumn(
                name: "StockVersion",
                table: "MedicalSupplies");

            migrationBuilder.DropColumn(
                name: "ItemsJson",
                table: "MedicalHistoryRecords");

            migrationBuilder.DropColumn(
                name: "DecidedAt",
                table: "InvoiceRefunds");

            migrationBuilder.DropColumn(
                name: "DecisionNotes",
                table: "InvoiceRefunds");

            migrationBuilder.DropColumn(
                name: "ItemsJson",
                table: "InvoiceRefunds");

            migrationBuilder.DropColumn(
                name: "ReviewerId",
                table: "InvoiceRefunds");

            migrationBuilder.DropColumn(
                name: "Snapshot",
                table: "InvoiceRefunds");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "InvoiceRefunds");

            migrationBuilder.DropColumn(
                name: "PriceEffectiveFrom",
                table: "DentalServices");

            migrationBuilder.DropColumn(
                name: "PriceHistoryJson",
                table: "DentalServices");

            migrationBuilder.DropColumn(
                name: "UnitPrice",
                table: "DentalServices");

            migrationBuilder.DropColumn(
                name: "ClinicalAddendaJson",
                table: "DentalImages");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "CreatedAccount",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "GuestDateOfBirth",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "GuestEmail",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "GuestFullName",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "GuestGender",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "GuestPhone",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "IntentDigest",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "IsReturning",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "SubmittedByUserId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Appointments");

            migrationBuilder.AlterColumn<int>(
                name: "PatientId",
                table: "Appointments",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CreatedByUserId",
                table: "Appointments",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "BookingRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AppointmentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ApprovedAppointmentId = table.Column<int>(type: "integer", nullable: true),
                    ClientRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAccount = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FullName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Gender = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    IntentDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsReturning = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReviewedByUserId = table.Column<int>(type: "integer", nullable: true),
                    SlotDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    SlotTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SubmittedByUserId = table.Column<int>(type: "integer", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerifiedPatientId = table.Column<int>(type: "integer", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingRequests", x => x.Id);
                    table.CheckConstraint("CK_BookingRequests_Duration", "\"SlotDurationMinutes\" BETWEEN 1 AND 1440");
                    table.CheckConstraint("CK_BookingRequests_Version", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_BookingRequests_Appointments_ApprovedAppointmentId",
                        column: x => x.ApprovedAppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "AppointmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookingRequests_Patients_VerifiedPatientId",
                        column: x => x.VerifiedPatientId,
                        principalTable: "Patients",
                        principalColumn: "PatientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookingRequests_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookingRequests_Users_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DentalImageClinicalAddenda",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AuthorName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AuthorRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    DentalImageId = table.Column<int>(type: "integer", nullable: false),
                    ImageVersion = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DentalImageClinicalAddenda", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DentalImageClinicalAddenda_DentalImages_DentalImageId",
                        column: x => x.DentalImageId,
                        principalTable: "DentalImages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DentalImageClinicalAddenda_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DiscountPresets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DiscountType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscountPresets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceNumberCounters",
                columns: table => new
                {
                    InvoiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LastNumber = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceNumberCounters", x => x.InvoiceDate);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceRefundItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceItemId = table.Column<int>(type: "integer", nullable: false),
                    InvoiceRefundId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceRefundItems", x => x.Id);
                    table.CheckConstraint("CK_InvoiceRefundItems_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_InvoiceRefundItems_InvoiceItems_InvoiceItemId",
                        column: x => x.InvoiceItemId,
                        principalTable: "InvoiceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvoiceRefundItems_InvoiceRefunds_InvoiceRefundId",
                        column: x => x.InvoiceRefundId,
                        principalTable: "InvoiceRefunds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceRefundProposals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    InvoiceId = table.Column<int>(type: "integer", nullable: false),
                    ReviewerId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReasonCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ReasonNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RefundId = table.Column<int>(type: "integer", nullable: true),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Snapshot = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceRefundProposals", x => x.Id);
                    table.CheckConstraint("CK_InvoiceRefundProposals_Status", "\"Status\" IN ('Pending','Approved','Rejected','Stale')");
                    table.ForeignKey(
                        name: "FK_InvoiceRefundProposals_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvoiceRefundProposals_Users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvoiceRefundProposals_Users_ReviewerId",
                        column: x => x.ReviewerId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MedicalHistoryItems",
                columns: table => new
                {
                    ItemId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecordId = table.Column<int>(type: "integer", nullable: false),
                    Detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsCritical = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalHistoryItems", x => x.ItemId);
                    table.ForeignKey(
                        name: "FK_MedicalHistoryItems_MedicalHistoryRecords_RecordId",
                        column: x => x.RecordId,
                        principalTable: "MedicalHistoryRecords",
                        principalColumn: "RecordId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MedicineStockBalances",
                columns: table => new
                {
                    MedicineId = table.Column<int>(type: "integer", nullable: false),
                    OnHand = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicineStockBalances", x => x.MedicineId);
                    table.CheckConstraint("CK_MedicineStockBalance_Nonnegative", "\"OnHand\" >= 0");
                    table.ForeignKey(
                        name: "FK_MedicineStockBalances_Medicines_MedicineId",
                        column: x => x.MedicineId,
                        principalTable: "Medicines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MedicineStockReceipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConfirmedByUserId = table.Column<int>(type: "integer", nullable: false),
                    MedicineCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MedicineId = table.Column<int>(type: "integer", nullable: false),
                    MedicineName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicineStockReceipts", x => x.Id);
                    table.CheckConstraint("CK_MedicineStockReceipt_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_MedicineStockReceipts_Medicines_MedicineId",
                        column: x => x.MedicineId,
                        principalTable: "Medicines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicineStockReceipts_Users_ConfirmedByUserId",
                        column: x => x.ConfirmedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotificationLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AppointmentId = table.Column<int>(type: "integer", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    AppointmentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MessageContent = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    NotificationType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RecipientPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SlotTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationLogs_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "AppointmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotificationLogs_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "PatientId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientPhoneChangeRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppointmentId = table.Column<int>(type: "integer", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CurrentPhone = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    NewPhone = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TokenVersion = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientPhoneChangeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientPhoneChangeRequests_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "AppointmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientPhoneChangeRequests_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientPhoneChangeRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PostTreatmentNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    VisitId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostTreatmentNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostTreatmentNotes_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PostTreatmentNotes_Visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "Visits",
                        principalColumn: "VisitId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PrescriptionDispenseEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ExpiryOverridden = table.Column<bool>(type: "boolean", nullable: false),
                    PerformedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PerformedBy = table.Column<int>(type: "integer", nullable: false),
                    PrescriptionId = table.Column<int>(type: "integer", nullable: false),
                    PrescriptionVersion = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReceivedByProxy = table.Column<bool>(type: "boolean", nullable: false),
                    ReceiverName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReceiverPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrescriptionDispenseEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrescriptionDispenseEvents_Prescriptions_PrescriptionId",
                        column: x => x.PrescriptionId,
                        principalTable: "Prescriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrescriptionDispenseEvents_Users_PerformedBy",
                        column: x => x.PerformedBy,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PrescriptionNumberCounters",
                columns: table => new
                {
                    PrescriptionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LastNumber = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrescriptionNumberCounters", x => x.PrescriptionDate);
                });

            migrationBuilder.CreateTable(
                name: "QueueStatusHistories",
                columns: table => new
                {
                    QueueStatusHistoryId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChangedByUserId = table.Column<int>(type: "integer", nullable: true),
                    QueueEntryId = table.Column<int>(type: "integer", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    FromStatus = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ToStatus = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueueStatusHistories", x => x.QueueStatusHistoryId);
                    table.ForeignKey(
                        name: "FK_QueueStatusHistories_QueueEntries_QueueEntryId",
                        column: x => x.QueueEntryId,
                        principalTable: "QueueEntries",
                        principalColumn: "QueueEntryId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QueueStatusHistories_Users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServicePrices",
                columns: table => new
                {
                    ServicePriceId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ServiceId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,0)", precision: 18, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServicePrices", x => x.ServicePriceId);
                    table.ForeignKey(
                        name: "FK_ServicePrices_DentalServices_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "DentalServices",
                        principalColumn: "DentalServiceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServicePrices_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffWorkspaces",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DentistId = table.Column<int>(type: "integer", nullable: true),
                    RoomId = table.Column<int>(type: "integer", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffWorkspaces", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_StaffWorkspaces_ClinicRooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "ClinicRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffWorkspaces_Users_DentistId",
                        column: x => x.DentistId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffWorkspaces_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffWorkspaces_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockBalances",
                columns: table => new
                {
                    MedicalSupplyId = table.Column<int>(type: "integer", nullable: false),
                    OnHand = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockBalances", x => x.MedicalSupplyId);
                    table.CheckConstraint("CK_StockBalance_OnHand", "\"OnHand\" >= 0");
                    table.ForeignKey(
                        name: "FK_StockBalances_MedicalSupplies_MedicalSupplyId",
                        column: x => x.MedicalSupplyId,
                        principalTable: "MedicalSupplies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockIssues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConfirmedByUserId = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    VisitId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockIssues_Users_ConfirmedByUserId",
                        column: x => x.ConfirmedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockIssues_Visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "Visits",
                        principalColumn: "VisitId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockReceipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConfirmedByUserId = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockReceipts_Users_ConfirmedByUserId",
                        column: x => x.ConfirmedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VisitUnlockRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActorUserId = table.Column<int>(type: "integer", nullable: false),
                    CancelledInvoiceId = table.Column<int>(type: "integer", nullable: true),
                    VisitId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitUnlockRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VisitUnlockRecords_Invoices_CancelledInvoiceId",
                        column: x => x.CancelledInvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VisitUnlockRecords_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VisitUnlockRecords_Visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "Visits",
                        principalColumn: "VisitId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceRefundProposalItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceItemId = table.Column<int>(type: "integer", nullable: false),
                    ProposalId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceRefundProposalItems", x => x.Id);
                    table.CheckConstraint("CK_InvoiceRefundProposalItems_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_InvoiceRefundProposalItems_InvoiceItems_InvoiceItemId",
                        column: x => x.InvoiceItemId,
                        principalTable: "InvoiceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvoiceRefundProposalItems_InvoiceRefundProposals_ProposalId",
                        column: x => x.ProposalId,
                        principalTable: "InvoiceRefundProposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MedicineStockIssues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DispenseEventId = table.Column<int>(type: "integer", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IssuedByUserId = table.Column<int>(type: "integer", nullable: false),
                    MedicineCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MedicineId = table.Column<int>(type: "integer", nullable: false),
                    MedicineName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PrescriptionId = table.Column<int>(type: "integer", nullable: false),
                    PrescriptionItemId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicineStockIssues", x => x.Id);
                    table.CheckConstraint("CK_MedicineStockIssue_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_MedicineStockIssues_Medicines_MedicineId",
                        column: x => x.MedicineId,
                        principalTable: "Medicines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicineStockIssues_PrescriptionDispenseEvents_DispenseEven~",
                        column: x => x.DispenseEventId,
                        principalTable: "PrescriptionDispenseEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicineStockIssues_PrescriptionItems_PrescriptionItemId",
                        column: x => x.PrescriptionItemId,
                        principalTable: "PrescriptionItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicineStockIssues_Prescriptions_PrescriptionId",
                        column: x => x.PrescriptionId,
                        principalTable: "Prescriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicineStockIssues_Users_IssuedByUserId",
                        column: x => x.IssuedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockIssueLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StockIssueId = table.Column<int>(type: "integer", nullable: false),
                    MedicalSupplyId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    SupplyCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SupplyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockIssueLines", x => x.Id);
                    table.CheckConstraint("CK_StockIssueLine_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_StockIssueLines_MedicalSupplies_MedicalSupplyId",
                        column: x => x.MedicalSupplyId,
                        principalTable: "MedicalSupplies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockIssueLines_StockIssues_StockIssueId",
                        column: x => x.StockIssueId,
                        principalTable: "StockIssues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockReceiptLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StockReceiptId = table.Column<int>(type: "integer", nullable: false),
                    MedicalSupplyId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    SupplyCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SupplyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockReceiptLines", x => x.Id);
                    table.CheckConstraint("CK_StockReceiptLine_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_StockReceiptLines_MedicalSupplies_MedicalSupplyId",
                        column: x => x.MedicalSupplyId,
                        principalTable: "MedicalSupplies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockReceiptLines_StockReceipts_StockReceiptId",
                        column: x => x.StockReceiptId,
                        principalTable: "StockReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VisitServices_ServicePriceId",
                table: "VisitServices",
                column: "ServicePriceId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_DiscountPresetId",
                table: "Invoices",
                column: "DiscountPresetId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRefunds_InvoiceId_Revision",
                table: "InvoiceRefunds",
                columns: new[] { "InvoiceId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRefunds_PaymentTransactionId",
                table: "InvoiceRefunds",
                column: "PaymentTransactionId",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvoiceRefunds_Payment",
                table: "InvoiceRefunds",
                sql: "(\"RefundAmount\" = 0 AND \"PaymentTransactionId\" IS NULL) OR (\"RefundAmount\" > 0 AND \"PaymentTransactionId\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_BookingRequests_ApprovedAppointmentId",
                table: "BookingRequests",
                column: "ApprovedAppointmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingRequests_ClientRequestId",
                table: "BookingRequests",
                column: "ClientRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingRequests_ReviewedByUserId",
                table: "BookingRequests",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingRequests_Status_AppointmentDate_SlotTime",
                table: "BookingRequests",
                columns: new[] { "Status", "AppointmentDate", "SlotTime" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingRequests_SubmittedByUserId",
                table: "BookingRequests",
                column: "SubmittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingRequests_VerifiedPatientId",
                table: "BookingRequests",
                column: "VerifiedPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_DentalImageClinicalAddenda_CreatedByUserId",
                table: "DentalImageClinicalAddenda",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DentalImageClinicalAddenda_DentalImageId_Id",
                table: "DentalImageClinicalAddenda",
                columns: new[] { "DentalImageId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_DentalImageClinicalAddenda_RequestId",
                table: "DentalImageClinicalAddenda",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscountPresets_Code",
                table: "DiscountPresets",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRefundItems_InvoiceItemId",
                table: "InvoiceRefundItems",
                column: "InvoiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRefundItems_InvoiceRefundId_InvoiceItemId",
                table: "InvoiceRefundItems",
                columns: new[] { "InvoiceRefundId", "InvoiceItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRefundProposalItems_InvoiceItemId",
                table: "InvoiceRefundProposalItems",
                column: "InvoiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRefundProposalItems_ProposalId_InvoiceItemId",
                table: "InvoiceRefundProposalItems",
                columns: new[] { "ProposalId", "InvoiceItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRefundProposals_CreatorId",
                table: "InvoiceRefundProposals",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRefundProposals_InvoiceId",
                table: "InvoiceRefundProposals",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRefundProposals_RequestId",
                table: "InvoiceRefundProposals",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRefundProposals_ReviewerId",
                table: "InvoiceRefundProposals",
                column: "ReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRefundProposals_Status_CreatedAt",
                table: "InvoiceRefundProposals",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MedicalHistoryItems_RecordId",
                table: "MedicalHistoryItems",
                column: "RecordId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineStockIssues_DispenseEventId",
                table: "MedicineStockIssues",
                column: "DispenseEventId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineStockIssues_IssuedByUserId",
                table: "MedicineStockIssues",
                column: "IssuedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineStockIssues_MedicineId",
                table: "MedicineStockIssues",
                column: "MedicineId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineStockIssues_PrescriptionId_MedicineId",
                table: "MedicineStockIssues",
                columns: new[] { "PrescriptionId", "MedicineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MedicineStockIssues_PrescriptionItemId",
                table: "MedicineStockIssues",
                column: "PrescriptionItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MedicineStockReceipts_ConfirmedByUserId",
                table: "MedicineStockReceipts",
                column: "ConfirmedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineStockReceipts_MedicineId_Id",
                table: "MedicineStockReceipts",
                columns: new[] { "MedicineId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_MedicineStockReceipts_RequestId",
                table: "MedicineStockReceipts",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_AppointmentId_NotificationType",
                table: "NotificationLogs",
                columns: new[] { "AppointmentId", "NotificationType" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_PatientId",
                table: "NotificationLogs",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_ReminderOccurrence",
                table: "NotificationLogs",
                columns: new[] { "AppointmentId", "NotificationType", "AppointmentDate", "SlotTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientPhoneChangeRequests_AppointmentId",
                table: "PatientPhoneChangeRequests",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientPhoneChangeRequests_CreatedByUserId",
                table: "PatientPhoneChangeRequests",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientPhoneChangeRequests_UserId_TokenVersion",
                table: "PatientPhoneChangeRequests",
                columns: new[] { "UserId", "TokenVersion" },
                unique: true,
                filter: "\"CompletedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PostTreatmentNotes_CreatedByUserId",
                table: "PostTreatmentNotes",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PostTreatmentNotes_RequestId",
                table: "PostTreatmentNotes",
                column: "RequestId",
                unique: true,
                filter: "\"RequestId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PostTreatmentNotes_VisitId_CreatedAt",
                table: "PostTreatmentNotes",
                columns: new[] { "VisitId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionDispenseEvents_PerformedBy",
                table: "PrescriptionDispenseEvents",
                column: "PerformedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionDispenseEvents_PrescriptionId_PrescriptionVersi~",
                table: "PrescriptionDispenseEvents",
                columns: new[] { "PrescriptionId", "PrescriptionVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QueueStatusHistories_ChangedByUserId",
                table: "QueueStatusHistories",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_QueueStatusHistories_QueueEntryId_ChangedAt",
                table: "QueueStatusHistories",
                columns: new[] { "QueueEntryId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ServicePrices_CreatedByUserId",
                table: "ServicePrices",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ServicePrices_ServiceId_EffectiveFrom_ServicePriceId",
                table: "ServicePrices",
                columns: new[] { "ServiceId", "EffectiveFrom", "ServicePriceId" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffWorkspaces_DentistId",
                table: "StaffWorkspaces",
                column: "DentistId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffWorkspaces_RoomId",
                table: "StaffWorkspaces",
                column: "RoomId",
                unique: true,
                filter: "\"RoomId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StaffWorkspaces_UpdatedByUserId",
                table: "StaffWorkspaces",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockIssueLines_MedicalSupplyId",
                table: "StockIssueLines",
                column: "MedicalSupplyId");

            migrationBuilder.CreateIndex(
                name: "IX_StockIssueLines_StockIssueId_MedicalSupplyId",
                table: "StockIssueLines",
                columns: new[] { "StockIssueId", "MedicalSupplyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockIssues_ConfirmedByUserId",
                table: "StockIssues",
                column: "ConfirmedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockIssues_RequestId",
                table: "StockIssues",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockIssues_VisitId_Id",
                table: "StockIssues",
                columns: new[] { "VisitId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_StockReceiptLines_MedicalSupplyId",
                table: "StockReceiptLines",
                column: "MedicalSupplyId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReceiptLines_StockReceiptId_MedicalSupplyId",
                table: "StockReceiptLines",
                columns: new[] { "StockReceiptId", "MedicalSupplyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockReceipts_ConfirmedByUserId",
                table: "StockReceipts",
                column: "ConfirmedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReceipts_RequestId",
                table: "StockReceipts",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VisitUnlockRecords_ActorUserId",
                table: "VisitUnlockRecords",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitUnlockRecords_CancelledInvoiceId",
                table: "VisitUnlockRecords",
                column: "CancelledInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitUnlockRecords_VisitId",
                table: "VisitUnlockRecords",
                column: "VisitId");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_DiscountPresets_DiscountPresetId",
                table: "Invoices",
                column: "DiscountPresetId",
                principalTable: "DiscountPresets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_VisitServices_ServicePrices_ServicePriceId",
                table: "VisitServices",
                column: "ServicePriceId",
                principalTable: "ServicePrices",
                principalColumn: "ServicePriceId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
