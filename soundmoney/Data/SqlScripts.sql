CREATE TABLE [dbo].[StockValuations] (
    [Symbol]           VARCHAR (20)    NOT NULL,
    [CompanyName]      VARCHAR (500)   NOT NULL,
    [Sector]           VARCHAR (100)   NOT NULL,
    [PrimaryMethod]    VARCHAR (100)   NOT NULL,
    [SecondaryMethod]  VARCHAR (100)   NOT NULL,
    [CurrentPrice]     DECIMAL (18, 2) NOT NULL,
    [IntrinsicValue]   DECIMAL (18, 2) NOT NULL,
    [MarginOfSafety]   DECIMAL (18, 2) NOT NULL,
    [Verdict]          VARCHAR (50)    NOT NULL,
    [SoundScore]       DECIMAL (18, 2) NOT NULL,
    [SoundScoreRating] VARCHAR (50)    NOT NULL,
    [FetchedAt]        DATETIME2 (7)   NOT NULL,
    [UpdatedAt]        DATETIME2 (7)   NULL,
    PRIMARY KEY CLUSTERED ([Symbol] ASC)
);

SELECT COUNT(Sector) 
FROM StockValuations 
WHERE UpdatedAt < '2026-08-16 15:00:00';

select count(sector) from stockvaluations where sector is not null;

select * from stockvaluations where symbol ='ALEMBICLTD';


Select distinct sector from stockvaluations;

/* UPDATE StockValuations 
 SET currentprice=0, intrinsicvalue=0,MarginOfSafety=0, Verdict='',
 SoundScore=0,SoundScoreRating='', UpdatedAt = GETDATE() - 2; */
 



