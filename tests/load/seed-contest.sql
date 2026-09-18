\if :{?participant_count}
\else
\set participant_count 1000
\endif

BEGIN;

DELETE FROM "Contests" WHERE "Id" = '60000000-0000-0000-0000-000000000001';
DELETE FROM "AspNetUsers" WHERE "Id" LIKE 'load-user-%';

INSERT INTO "Contests" (
    "Id", "Name", "StartsOn", "EndsOn", "DurationDays", "ScoringRuleVersion",
    "RewardRuleVersion", "DailyScoreCap", "CreatedAt")
VALUES (
    '60000000-0000-0000-0000-000000000001', 'Leaderboard load test', CURRENT_DATE,
    CURRENT_DATE + 7, 7, 1, 1, 100, CURRENT_TIMESTAMP);

INSERT INTO "AspNetUsers" (
    "Id", "UserName", "NormalizedUserName", "Email", "NormalizedEmail", "EmailConfirmed",
    "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount",
    "SecurityStamp", "ConcurrencyStamp")
SELECT
    'load-user-' || LPAD(value::text, 6, '0'),
    'load-user-' || LPAD(value::text, 6, '0'),
    'LOAD-USER-' || LPAD(value::text, 6, '0'),
    'load-user-' || LPAD(value::text, 6, '0') || '@example.invalid',
    'LOAD-USER-' || LPAD(value::text, 6, '0') || '@EXAMPLE.INVALID',
    FALSE, FALSE, FALSE, FALSE, 0, md5('security-' || value), md5('concurrency-' || value)
FROM generate_series(1, :participant_count) AS value;

INSERT INTO "ContestParticipants" (
    "Id", "ContestId", "UserId", "ClientOperationId", "JoinedAt", "EligibleFrom")
SELECT
    md5('participant-' || value)::uuid,
    '60000000-0000-0000-0000-000000000001',
    'load-user-' || LPAD(value::text, 6, '0'),
    md5('operation-' || value)::uuid,
    CURRENT_TIMESTAMP,
    CURRENT_DATE
FROM generate_series(1, :participant_count) AS value;

INSERT INTO "ContestDailyScores" (
    "Id", "ContestId", "UserId", "LocalDate", "DailyTargetMl", "HydrationMl", "Score",
    "RuleVersion", "IsFinal", "CalculatedAt", "FinalizedAt")
SELECT
    md5('score-' || value)::uuid,
    '60000000-0000-0000-0000-000000000001',
    'load-user-' || LPAD(value::text, 6, '0'),
    CURRENT_DATE,
    2000,
    (value % 101) * 20,
    value % 101,
    1,
    FALSE,
    CURRENT_TIMESTAMP,
    NULL
FROM generate_series(1, :participant_count) AS value;

COMMIT;
