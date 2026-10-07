SELECT json_build_object(
    'migrations', (SELECT count(*) FROM "__EFMigrationsHistory"),
    'accounts', (SELECT count(*) FROM "AspNetUsers"),
    'scores', (SELECT count(*) FROM "ContestDailyScores"),
    'points', (SELECT coalesce(sum("Score"), 0) FROM "ContestDailyScores"),
    'dropsEntries', (SELECT count(*) FROM "DropsLedgerEntries"),
    'dropsAmount', (SELECT coalesce(sum("Amount"), 0) FROM "DropsLedgerEntries"),
    'prestigeEntries', (SELECT count(*) FROM "PrestigeLedgerEntries"),
    'prestigeAmount', (SELECT coalesce(sum("Amount"), 0) FROM "PrestigeLedgerEntries"));
