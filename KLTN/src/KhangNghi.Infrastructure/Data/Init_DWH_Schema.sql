-- Legacy SQL is preserved under docs/review-backup-20261015.
-- Fact_Order/TimeID schema is incompatible with the current SSIS pipeline.
THROW 51000, 'Legacy schema disabled. Review 000_ETL_Schema_EmptyDatabase.sql and 001_Etl_Control.sql on TEST.', 1;
