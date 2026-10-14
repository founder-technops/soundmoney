/*Drop Table [dbo].[StockValuations];

CREATE TABLE [dbo].[StockValuations] (
    [Symbol]           VARCHAR (20)    NOT NULL,
    [CompanyName]      VARCHAR (500)   NOT NULL,
    [Sector]           VARCHAR (100)   NOT NULL,
    [PrimaryMethod]    VARCHAR (100)   NOT NULL,
    [SecondaryMethod]  VARCHAR (100)   NOT NULL,
    [CurrentPrice]     DECIMAL (18, 2) NOT NULL,
    [IntrinsicValue]   DECIMAL (18, 2) NOT NULL,
    [MarginOfSafety]   DECIMAL (18, 2) NOT NULL,
    [DividendYieldPercent]   DECIMAL (18, 2) NOT NULL,
    [IsDividendConsistent] bit not null default 0,
    [Verdict]          VARCHAR (50)    NOT NULL,
    [SoundScore]       DECIMAL (18, 2) NOT NULL,
    [SoundScoreRating] VARCHAR (50)    NOT NULL,
    [FetchedAt]        DATETIME2 (7)   NOT NULL,
    [UpdatedAt]        DATETIME2 (7)   NULL,
    [ErrorMessage]      VARCHAR (4000)  NULL,
    PRIMARY KEY CLUSTERED ([Symbol] ASC)
); 

-- Create the ScrapedFinancialCaches table
CREATE TABLE dbo.ScrapedFinancialCaches (
    Symbol                    VARCHAR(20) NOT NULL,
    StockValuationJson        NVARCHAR(MAX) NOT NULL,
    CurrentFinancialJson      NVARCHAR(MAX) NOT NULL,
    HistoricalFinancialsJson  NVARCHAR(MAX) NOT NULL,
    ScrapedAt                 DATETIME2 NOT NULL,
    
    CONSTRAINT PK_ScrapedFinancialCaches PRIMARY KEY CLUSTERED (Symbol)
);
GO

-- Create the index on the ScrapedAt column
CREATE INDEX IX_ScrapedFinancialCaches_ScrapedAt 
ON dbo.ScrapedFinancialCaches (ScrapedAt);
GO

-- Create index on the FetchedAt column
CREATE INDEX IX_StockValuations_FetchedAt 
ON dbo.StockValuations (FetchedAt);
GO

-- Create index on the Symbol column
CREATE INDEX IX_StockValuations_Symbol 
ON dbo.StockValuations (Symbol);
GO

UPDATE StockValuations 
 SET currentprice=0, intrinsicvalue=0,MarginOfSafety=0, Verdict='',
 SoundScore=0,SoundScoreRating='', UpdatedAt = NULL where symbol ='ARCHIES';

SELECT * 
FROM StockValuations 
WHERE UpdatedAt > '2026-09-11 00:00:00';

select count(symbol) from stockvaluations where updatedat is not null;

select * from stockvaluations where symbol in ('MAHABANK');

select * from stockvaluations where verdict in ('BUY','STRONG BUY') 
and SoundScoreRating in ('STRONG SOUND','SOUND') order by soundscore desc,marginofsafety desc;

Select distinct sector from stockvaluations;


 
 select SYSDATETIME(), GETUTCDATE();

 select * from ScrapedFinancialCaches;
 */

