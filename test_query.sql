DECLARE @in VARCHAR(MAX);
SET @in = '{"CardNo":"22F66"}';
SELECT ClassNameID, ClassName AS LocationName, Type, Memo AS CardNo, BeginTime
FROM ClassName 
WHERE Type = N'巡更点' AND Memo = JSON_VALUE(@in, '$.CardNo');