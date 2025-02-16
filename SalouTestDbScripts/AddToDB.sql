use SalouTestDB
go
CREATE PROCEDURE [dbo].SeaCostumers
	@sea nvarchar,
	@product_count INT OUTPUT
AS
	SELECT @product_count =COUNT(*) FROM Customers WHERE cust_name LIKE '%' + @sea + '%';
	select * from Customers where cust_name LIKE '%' + @sea + '%';
RETURN 111
go
GRANT EXECUTE ON [dbo].SeaCostumers to salou;

go
