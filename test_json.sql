DECLARE @input VARCHAR(MAX) = '{""CardNo"":""22F66""}';
DECLARE @CardNo VARCHAR(100);
SET @CardNo = JSON_VALUE(@input, '$.CardNo');
PRINT 'CardNo: ' + ISNULL(@CardNo, 'NULL');
SELECT ClassName FROM ClassName WHERE Type=N'巡更点' AND Memo=@CardNo;