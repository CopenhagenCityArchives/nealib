namespace HardHorn.Archiving
{
    public enum DataType
    {
        // Text / string / hexadecimal types
        CHARACTER,
        NATIONAL_CHARACTER,
        CHARACTER_VARYING,
        NATIONAL_CHARACTER_VARYING,
        // Integer types
        INTEGER,
        SMALL_INTEGER,
        // Decimal types
        NUMERIC,
        DECIMAL,
        FLOAT,
        DOUBLE_PRECISION,
        REAL,
        // Boolean types
        BOOLEAN,
        // Date / time types
        DATE,
        TIME,
        TIME_WITH_TIME_ZONE,
        TIMESTAMP,
        TIMESTAMP_WITH_TIME_ZONE,
        INTERVAL,
        UNDEFINED,
    }
}
