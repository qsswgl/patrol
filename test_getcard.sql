DECLARE @out VARCHAR(MAX), @in VARCHAR(MAX);
SET @in = '{"CardNo":"22F66"}';
EXEC get_card @InputValue=@in, @OutputValue=@out OUTPUT;
PRINT 'Output: ' + ISNULL(@out, 'NULL');
