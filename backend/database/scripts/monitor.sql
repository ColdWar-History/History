-- Table growth overview per service schema.
SELECT
    n.nspname AS schema_name,
    c.relname AS table_name,
    pg_size_pretty(pg_total_relation_size(c.oid)) AS total_size,
    c.reltuples::bigint AS estimated_rows
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relkind = 'r'
  AND n.nspname IN ('auth', 'content', 'game', 'progress')
ORDER BY pg_total_relation_size(c.oid) DESC;

-- Index usage (tables with potentially underused indexes will show low idx_scan).
SELECT
    schemaname,
    relname AS table_name,
    indexrelname AS index_name,
    idx_scan,
    idx_tup_read,
    idx_tup_fetch
FROM pg_stat_user_indexes
WHERE schemaname IN ('auth', 'content', 'game', 'progress')
ORDER BY idx_scan ASC, schemaname, relname;

-- Long-running active queries.
SELECT
    pid,
    usename,
    application_name,
    now() - query_start AS duration,
    state,
    query
FROM pg_stat_activity
WHERE state <> 'idle'
  AND query_start IS NOT NULL
ORDER BY duration DESC;
