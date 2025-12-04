IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'insert_address')
    DROP PROCEDURE insert_address;
GO

CREATE PROCEDURE insert_address
    @DataGridID INT = 0,
    @UsersID INT = 0,
    @InputValue VARCHAR(MAX),
    @OutputValue VARCHAR(MAX) = '' OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @CardNo VARCHAR(100), @LocationName VARCHAR(500);
    
    -- 从 InputValue JSON 中解析参数
    SELECT @CardNo = JSON_VALUE(@InputValue, '$.CardNo'),
           @LocationName = JSON_VALUE(@InputValue, '$.LocationName');
    
    -- 插入新的巡更点记录
    INSERT INTO ClassName (ClassName, Type, Memo, BeginTime, State)
    VALUES (@LocationName, N'巡更点', @CardNo, GETDATE(), 1);
    
    DECLARE @NewID INT;
    SET @NewID = SCOPE_IDENTITY();
    
    -- 返回 JSON 格式结果
    SET @OutputValue = CONCAT(
        '{"Result":"0","Message":"',
        @LocationName,
        '","ClassNameID":"',
        CAST(@NewID AS VARCHAR(20)),
        '"}'
    );
END
