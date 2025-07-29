USE [DB_GENESIS_CENTRAL]
GO

/****** Object:  Table [dbo].[ML_Productos]    Script Date: 10/04/2025 11:03:05 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[ML_Productos](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[MisspelledWord] [nvarchar](64) NOT NULL,
	[CorrectWord] [nvarchar](64) NOT NULL,
	[Bias] [tinyint] NULL,
	[Status] [varchar](4) NULL
) ON [PRIMARY]
GO


