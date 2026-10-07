-- Synthetic data only, for the isolated CI backup database.
INSERT INTO "DropsLedgerEntries" ("Id", "UserId", "Amount", "EntryType", "ReferenceType", "ReferenceId", "IdempotencyKey", "RuleVersion", "CreatedAt")
SELECT md5('backup-drops-' || "Id")::uuid, "Id", 100, 'achievement', 'achievement', 'backup-probe', 'backup-drops-' || "Id", 1, CURRENT_TIMESTAMP
FROM "AspNetUsers" WHERE "Id" LIKE 'load-user-%';

INSERT INTO "PrestigeLedgerEntries" ("Id", "UserId", "Amount", "EntryType", "ReferenceType", "ReferenceId", "IdempotencyKey", "RuleVersion", "CreatedAt")
SELECT md5('backup-prestige-' || "Id")::uuid, "Id", 10, 'achievement', 'achievement', 'backup-probe', 'backup-prestige-' || "Id", 1, CURRENT_TIMESTAMP
FROM "AspNetUsers" WHERE "Id" LIKE 'load-user-%';
