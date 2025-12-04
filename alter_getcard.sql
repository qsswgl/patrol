ALTER PROCEDURE [dbo].[Get_Card]
    @InputValue VARCHAR(MAX),
    @OutputValue VARCHAR(MAX) = '' OUTPUT
AS
BEGIN
    DECLARE @NewID INT;
    INSERT rizhi(TableName,ProcedureName,Message) select N'获取巡更点记录',OBJECT_NAME(@@PROCID),'exec '+OBJECT_NAME(@@PROCID)+' '''+@InputValue+''''
    SET @NewID = SCOPE_IDENTITY();
    SET NOCOUNT ON;
    
    DECLARE @CardNo VARCHAR(100), @LocationName NVARCHAR(100) = N'';
    SET @CardNo = JSON_VALUE(@InputValue, '$.CardNo');
    
    SELECT TOP 1 @LocationName = ClassName
    FROM ClassName WHERE Type = N'巡更点' AND Memo = @CardNo;
    
    IF @LocationName IS NULL OR @LocationName = N''
    BEGIN
        SET @OutputValue = '{"Result":"-1","Message":"卡未登记"}';
    END
    ELSE
    BEGIN
        SET @OutputValue = '{"Result":"0","Message":"' + @LocationName + '"}';
    END
    
    UPDATE RIZHI SET result = @OutputValue, EndTime = GETDATE() WHERE rizhiid = @NewID;
END