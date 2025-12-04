DECLARE @out VARCHAR(MAX);
EXEC get_card @InputValue='{"CardNo":"22F66"}', @OutputValue=@out OUTPUT;
SELECT @out AS Result;