DELETE FROM progress.crypto_operations
WHERE processed_at < now() - make_interval(days => (
    SELECT operations_retention_days
    FROM progress.retention_policy
    WHERE id = 1
));
