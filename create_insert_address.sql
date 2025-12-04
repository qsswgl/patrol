CREATE PROCEDURE insert_address
    @DataGridID INT = 0,
    @UsersID INT = 0,
    @InputValue VARCHAR(MAX),
    @OutputValue VARCHAR(MAX) = '' OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @CardNo VARCHAR(100), @LocationName VARCHAR(500);
    
    SELECT @CardNo = JSON_VALUE(@InputValue, '$.CardNo'),
           @LocationName = JSON_VALUE(@InputValue, '$.LocationName');
    
    INSERT INTO ClassName (ClassName, Type, Memo, BeginTime, State)
    VALUES (@LocationName, N'巡更点', @CardNo, GETDATE(), 1);
    
    DECLARE @NewID INT;
    SET @NewID = SCOPE_IDENTITY();
    
    SET @OutputValue = '{"Result":"0","Message":"' + @LocationName + '"}';
END
