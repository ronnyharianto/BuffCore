namespace BuffCore.Data.Constants
{
    /// <summary>
    /// Common collation names for SQL Server and PostgreSQL.
    /// </summary>
    public static class CollationConstant
    {
        /// <summary>
        /// SQL Server collation for case-insensitive comparisons.
        /// </summary>
        public const string SQL_Latin1_General_CP1_CI_AS = "SQL_Latin1_General_CP1_CI_AS";

        /// <summary>
        /// SQL Server collation for case-sensitive comparisons.
        /// </summary>
        public const string SQL_Latin1_General_CP1_CS_AS = "SQL_Latin1_General_CP1_CS_AS";

        /// <summary>
        /// PostgreSQL collation using en_US.UTF-8 (generally case-insensitive, locale-based).
        /// </summary>
        public const string PG_English_UnitedStates_CI = "en_US.UTF-8";

        /// <summary>
        /// PostgreSQL POSIX collation (binary sort, case-sensitive).
        /// </summary>
        public const string PG_POSIX_C = "C";
    }
}
