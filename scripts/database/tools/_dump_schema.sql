SET NOCOUNT ON;
WITH cols AS (
    SELECT
        c.TABLE_SCHEMA,
        c.TABLE_NAME,
        c.ORDINAL_POSITION,
        c.COLUMN_NAME,
        c.IS_NULLABLE,
        CASE
            WHEN c.DATA_TYPE IN ('varchar', 'nvarchar', 'char', 'nchar')
                THEN c.DATA_TYPE + '(' + CASE WHEN c.CHARACTER_MAXIMUM_LENGTH = -1 THEN 'max' ELSE CAST(c.CHARACTER_MAXIMUM_LENGTH AS varchar(20)) END + ')'
            WHEN c.DATA_TYPE IN ('decimal', 'numeric') AND c.NUMERIC_PRECISION IS NOT NULL
                THEN c.DATA_TYPE + '(' + CAST(c.NUMERIC_PRECISION AS varchar(10)) + ',' + CAST(c.NUMERIC_SCALE AS varchar(10)) + ')'
            WHEN c.DATA_TYPE IN ('datetime2', 'datetimeoffset', 'time') AND c.DATETIME_PRECISION IS NOT NULL
                THEN c.DATA_TYPE + '(' + CAST(c.DATETIME_PRECISION AS varchar(10)) + ')'
            ELSE c.DATA_TYPE
        END AS TypeStr
    FROM INFORMATION_SCHEMA.COLUMNS c
    INNER JOIN INFORMATION_SCHEMA.TABLES t
        ON c.TABLE_SCHEMA = t.TABLE_SCHEMA AND c.TABLE_NAME = t.TABLE_NAME
    WHERE t.TABLE_TYPE = 'BASE TABLE'
),
blocks AS (
    SELECT
        TABLE_SCHEMA,
        TABLE_NAME,
        CAST(
            '[' + TABLE_SCHEMA + '].[' + TABLE_NAME + ']' + CHAR(10)
            + STRING_AGG(
                CAST(
                    '  - ' + COLUMN_NAME + ' (' + TypeStr + ', Nullable: ' + CASE IS_NULLABLE WHEN 'YES' THEN 'YES' ELSE 'NO' END + ')'
                    AS NVARCHAR(MAX)
                ),
                CHAR(10)
            ) WITHIN GROUP (ORDER BY ORDINAL_POSITION)
            AS NVARCHAR(MAX)
        ) AS block_text
    FROM cols
    GROUP BY TABLE_SCHEMA, TABLE_NAME
)
SELECT CAST(
    STRING_AGG(CAST(block_text AS NVARCHAR(MAX)), CHAR(10) + CHAR(10)) WITHIN GROUP (ORDER BY TABLE_SCHEMA, TABLE_NAME)
    AS NVARCHAR(MAX)
)
FROM blocks;
