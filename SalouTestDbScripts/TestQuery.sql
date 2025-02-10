USE [SalouTestDB]
GO

DECLARE	@return_value Int,
		@product_count int

EXEC	@return_value = [dbo].[SeaCostumers]
		@sea = N'Fun',
		@product_count = @product_count OUTPUT

SELECT	@product_count as N'@product_count'

SELECT	@return_value as 'Return Value'

GO
