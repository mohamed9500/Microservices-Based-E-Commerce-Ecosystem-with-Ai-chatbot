-- Optional: pre-create the per-service databases (services also do this automatically).
DECLARE @dbs TABLE (name sysname);
INSERT INTO @dbs VALUES ('nexus_identity'),('nexus_catalog'),('nexus_inventory'),('nexus_cart'),('nexus_orders'),
  ('nexus_payments'),('nexus_notifications'),('nexus_reviews'),('nexus_assistant');
DECLARE @n sysname, @sql nvarchar(400);
DECLARE c CURSOR FOR SELECT name FROM @dbs;
OPEN c; FETCH NEXT FROM c INTO @n;
WHILE @@FETCH_STATUS = 0
BEGIN
  IF DB_ID(@n) IS NULL BEGIN SET @sql = N'CREATE DATABASE [' + @n + N']'; EXEC (@sql); END
  FETCH NEXT FROM c INTO @n;
END
CLOSE c; DEALLOCATE c;
